(() => {
    const station = document.getElementById("cameraStation");
    if (!station) return;

    const video = document.getElementById("cameraVideo");
    const roiGuide = document.getElementById("cameraRoiGuide");
    const placeholder = document.getElementById("cameraPlaceholder");
    const cameraBadge = document.getElementById("cameraStatusBadge");
    const cameraState = document.getElementById("cameraState");
    const visionState = document.getElementById("visionState");
    const detection = document.getElementById("detectionStatus");
    const classification = document.getElementById("lastClassification");
    const confidence = document.getElementById("lastConfidence");
    const message = document.getElementById("cameraMessage");
    const retryButton = document.getElementById("retryCamera");
    const canvas = document.getElementById("captureCanvas");
    const canvasContext = canvas.getContext("2d", { willReadFrequently: true });

    const frameIntervalMs = Math.max(100, Number(station.dataset.frameIntervalMs) || 300);
    const stableFramesRequired = Math.max(1, Number(station.dataset.stableFrames) || 2);
    const presenceThreshold = Math.min(1, Math.max(0.01, Number(station.dataset.presenceThreshold) || 0.10));
    const roiRatio = Math.min(1, Math.max(0.1, Number(station.dataset.roiRatio) || 0.85));
    const sampleSize = 32;
    const sampleCanvas = document.createElement("canvas");
    sampleCanvas.width = sampleSize;
    sampleCanvas.height = sampleSize;
    const sampleContext = sampleCanvas.getContext("2d", { willReadFrequently: true });

    let stream = null;
    let background = null;
    let heldSample = null;
    let heldRetryAt = 0;
    let changeDetected = false;
    let pendingCandidate = false;
    let clearFrames = 0;
    let registeredLatch = false;
    let requestActive = false;
    let pageClosing = false;
    let retryTimer = null;
    let visionTimer = null;
    let apiAbortController = null;

    const setMessage = (text, isError = false) => {
        message.textContent = text;
        message.classList.toggle("camera-message-error", isError);
    };

    const readToken = () => document.querySelector("#cameraAntiforgery input[name='__RequestVerificationToken']")?.value;

    const setCameraBadge = (text, active) => {
        cameraBadge.textContent = text;
        cameraBadge.classList.toggle("status-ready", active);
        cameraBadge.classList.toggle("status-empty", !active);
    };

    const sampleFrame = () => {
        const side = Math.max(1, Math.round(Math.min(video.videoWidth, video.videoHeight) * roiRatio));
        const sx = Math.floor((video.videoWidth - side) / 2);
        const sy = Math.floor((video.videoHeight - side) / 2);
        sampleContext.drawImage(video, sx, sy, side, side, 0, 0, sampleSize, sampleSize);
        return sampleContext.getImageData(0, 0, sampleSize, sampleSize).data;
    };

    const updateRoiGuide = () => {
        if (!video.videoWidth || !video.videoHeight) return;
        const frame = document.querySelector(".camera-frame");
        const frameWidth = frame.clientWidth;
        const frameHeight = frame.clientHeight;
        const scale = Math.min(frameWidth / video.videoWidth, frameHeight / video.videoHeight);
        const renderedWidth = video.videoWidth * scale;
        const renderedHeight = video.videoHeight * scale;
        const side = Math.round(Math.min(video.videoWidth, video.videoHeight) * roiRatio);
        const left = ((frameWidth - renderedWidth) / 2 + (video.videoWidth - side) / 2 * scale) / frameWidth * 100;
        const top = ((frameHeight - renderedHeight) / 2 + (video.videoHeight - side) / 2 * scale) / frameHeight * 100;
        roiGuide.style.setProperty("--roi-left", `${left}%`);
        roiGuide.style.setProperty("--roi-top", `${top}%`);
        roiGuide.style.setProperty("--roi-width", `${side * scale / frameWidth * 100}%`);
        roiGuide.style.setProperty("--roi-height", `${side * scale / frameHeight * 100}%`);
    };

    const foregroundRatio = (current, reference) => {
        if (!reference) return 0;
        let changed = 0;
        const pixels = sampleSize * sampleSize;
        for (let i = 0; i < current.length; i += 4) {
            const difference = (Math.abs(current[i] - reference[i]) +
                Math.abs(current[i + 1] - reference[i + 1]) +
                Math.abs(current[i + 2] - reference[i + 2])) / 3;
            if (difference > 32) changed++;
        }
        return changed / pixels;
    };

    const blendBackground = (current, ratio = 0.025) => {
        if (!background) {
            background = new Uint8ClampedArray(current);
            return;
        }
        for (let i = 0; i < current.length; i += 1) {
            background[i] = Math.round(background[i] * (1 - ratio) + current[i] * ratio);
        }
    };

    const makeJpeg = async () => {
        const captureStarted = performance.now();
        const scale = Math.min(1, 640 / video.videoWidth);
        canvas.width = Math.max(1, Math.round(video.videoWidth * scale));
        canvas.height = Math.max(1, Math.round(video.videoHeight * scale));
        canvasContext.drawImage(video, 0, 0, canvas.width, canvas.height);
        const captureMs = performance.now() - captureStarted;
        const jpegStarted = performance.now();
        const blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", 0.82));
        return { blob, captureMs, jpegMs: performance.now() - jpegStarted };
    };

    const pollVisionStatus = async () => {
        try {
            const response = await fetch(station.dataset.statusUrl, { cache: "no-store" });
            if (!response.ok) throw new Error("No se pudo consultar el servicio de visión.");
            const status = await response.json();
            visionState.textContent = status.iaActiva ? "ACTIVA" : (status.mensaje ?? "CARGANDO MODELO");
            if (status.iaActiva && !visionTimer) {
                setMessage("IA activa. Esperando que aparezca un residuo.");
                visionTimer = window.setInterval(pollVisionStatus, 2000);
                return;
            }
            if (status.iaActiva) return;
            if (visionTimer) {
                window.clearInterval(visionTimer);
                visionTimer = null;
            }
        } catch {
            visionState.textContent = "NO DISPONIBLE";
        }
        window.setTimeout(pollVisionStatus, 1000);
    };

    const sendFrame = async () => {
        if (requestActive || !stream || video.readyState < HTMLMediaElement.HAVE_CURRENT_DATA) return;
        requestActive = true;
        const requestStarted = performance.now();
        let timeoutId = null;
        let timedOut = false;
        detection.textContent = "Analizando residuo…";
        try {
            const { blob, captureMs, jpegMs } = await makeJpeg();
            if (!blob) throw new Error("No se pudo capturar el frame de análisis.");

            const formData = new FormData();
            formData.append("imagen", blob, "frame.jpg");
            formData.append("__RequestVerificationToken", readToken());
            apiAbortController = new AbortController();
            timeoutId = window.setTimeout(() => {
                timedOut = true;
                apiAbortController?.abort();
            }, 50000);
            const response = await fetch(station.dataset.classifyUrl, {
                method: "POST",
                body: formData,
                headers: { "X-Client-Capture-Ms": captureMs.toFixed(1), "X-Client-Jpeg-Ms": jpegMs.toFixed(1) },
                signal: apiAbortController.signal
            });
            console.debug("EcoBox camera timings", { captureMs, jpegMs, requestMs: performance.now() - requestStarted });
            const result = await response.json();
            if (!response.ok) throw new Error(result.mensaje ?? "No se pudo procesar el frame.");

            if (result.categoria) classification.textContent = result.categoria.replaceAll("_", " ");
            if (result.confianza != null) confidence.textContent = `${(result.confianza * 100).toFixed(1)} %`;
            setMessage(result.mensaje, result.mensaje?.toLowerCase().includes("insuficiente") ?? false);

            if (result.registrada) {
                registeredLatch = true;
                pendingCandidate = false;
                clearFrames = 0;
                detection.textContent = "Residuo detectado · registrado";
                setMessage(`${result.mensaje} Retíralo para habilitar la siguiente detección.`);
            } else if (result.estado === "esperando_retiro") {
                registeredLatch = true;
                pendingCandidate = false;
                detection.textContent = "Residuo registrado · esperando retiro";
                setMessage("Este residuo ya fue registrado. Retíralo para detectar el siguiente.");
            } else if (result.estado === "primer_frame" || result.estado === "esperando_confirmacion") {
                pendingCandidate = true;
                detection.textContent = result.estado === "primer_frame" ? "Primera predicción · esperando frame 2/2" : "Segunda predicción · confirmando estabilidad";
            } else if (result.estado === "confirmacion_rechazada") {
                pendingCandidate = true;
                detection.textContent = `Confirmación rechazada · ${result.razon ?? "predicciones distintas"}`;
            } else if (result.estado === "baja_confianza") {
                classification.textContent = "Confianza insuficiente";
                pendingCandidate = false;
                detection.textContent = "Esperando residuo o cambio de encuadre…";
                heldSample = sampleFrame();
                heldRetryAt = 0;
            } else if (result.estado === "error_registro") {
                pendingCandidate = false;
                detection.textContent = "Esperando residuo · el registro se reintentará";
                heldSample = sampleFrame();
                heldRetryAt = Date.now() + 2000;
            } else if (result.estado === "error") {
                pendingCandidate = false;
                detection.textContent = "Esperando residuo…";
                heldSample = sampleFrame();
                heldRetryAt = Date.now() + 3000;
            } else if (registeredLatch) {
                detection.textContent = "Residuo ya registrado · retíralo para continuar";
            } else {
                detection.textContent = "Esperando residuo…";
            }
        } catch (error) {
            if (error.name !== "AbortError" || (timedOut && !pageClosing)) {
                heldSample = stream && video.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA ? sampleFrame() : null;
                heldRetryAt = Date.now() + 3000;
                pendingCandidate = false;
                const errorMessage = timedOut ? "El análisis excedió 50 segundos y se canceló." : error.message;
                setMessage(errorMessage, true);
                detection.textContent = "Esperando residuo…";
            }
        } finally {
            if (timeoutId) window.clearTimeout(timeoutId);
            apiAbortController = null;
            requestActive = false;
        }
    };

    const rearmDetection = async (currentFrame) => {
        try {
            const formData = new FormData();
            formData.append("__RequestVerificationToken", readToken());
            const response = await fetch(station.dataset.rearmUrl, { method: "POST", body: formData });
            if (!response.ok) throw new Error("No se pudo rearmar la detección.");
            registeredLatch = false;
            heldSample = null;
            heldRetryAt = 0;
            pendingCandidate = false;
            clearFrames = 0;
            background = new Uint8ClampedArray(currentFrame);
            classification.textContent = "Sin detecciones";
            confidence.textContent = "—";
            detection.textContent = "Esperando el siguiente residuo…";
            setMessage("Cámara libre. Esperando el siguiente residuo.");
        } catch (error) {
            setMessage(error.message, true);
        }
    };

    const analyzeLoop = async () => {
        if (pageClosing || !stream) return;
        if (video.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA && video.videoWidth > 0) {
            const current = sampleFrame();
            if (!background) {
                background = new Uint8ClampedArray(current);
                detection.textContent = "Esperando residuo…";
                setMessage("Fondo de cámara preparado. Coloca un residuo frente a la cámara.");
            }

            const changedRatio = foregroundRatio(current, background);
            if (registeredLatch) {
                if (changedRatio < presenceThreshold * 0.65) clearFrames++;
                else clearFrames = 0;

                detection.textContent = "Residuo registrado · retíralo para continuar";
                if (clearFrames >= stableFramesRequired) await rearmDetection(current);
            } else if (changedRatio >= presenceThreshold) {
                if (!changeDetected) {
                    console.info("CAMBIO_DETECTADO: cambio relevante dentro de la ROI.");
                    changeDetected = true;
                }
                if (heldSample && foregroundRatio(current, heldSample) < presenceThreshold &&
                    (heldRetryAt === 0 || Date.now() < heldRetryAt)) {
                    detection.textContent = "Esperando un cambio para volver a analizar…";
                } else {
                    heldSample = null;
                    heldRetryAt = 0;
                    detection.textContent = "Cambio detectado · analizando…";
                    await sendFrame();
                }
            } else {
                changeDetected = false;
                blendBackground(current);
                heldSample = null;
                heldRetryAt = 0;
                if (pendingCandidate) {
                    await rearmDetection(current);
                }
                if (visionState.textContent === "ACTIVA") {
                    detection.textContent = "Esperando residuo…";
                    setMessage("IA activa. Esperando que aparezca un residuo.");
                }
            }
        }
        window.setTimeout(analyzeLoop, frameIntervalMs);
    };

    const scheduleRecovery = (delayMs = 3000) => {
        if (pageClosing || retryTimer) return;
        retryTimer = window.setTimeout(() => {
            retryTimer = null;
            startCamera(true);
        }, delayMs);
    };

    const cameraDisconnected = () => {
        if (pageClosing) return;
        stream?.getTracks().forEach(track => track.stop());
        stream = null;
        if (!registeredLatch) background = null;
        heldSample = null;
        placeholder.hidden = false;
        placeholder.querySelector("p").textContent = "Cámara desconectada";
        cameraState.textContent = "Cámara desconectada";
        setCameraBadge("DESCONECTADA", false);
        setMessage("Cámara desconectada. Intentando recuperarla…", true);
        scheduleRecovery();
    };

    const startCamera = async (isRecovery = false) => {
        if (pageClosing || stream) return;
        if (!navigator.mediaDevices?.getUserMedia) {
            cameraState.textContent = "Permiso de cámara requerido";
            placeholder.querySelector("p").textContent = "Permiso de cámara requerido";
            retryButton.hidden = false;
            setMessage("Permiso de cámara requerido. Abre esta estación en localhost o HTTPS y permite el acceso.", true);
            setCameraBadge("SIN PERMISO", false);
            return;
        }

        try {
            cameraState.textContent = isRecovery ? "Reconectando…" : "Solicitando permiso…";
            const newStream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment" }, audio: false });
            if (pageClosing) {
                newStream.getTracks().forEach(track => track.stop());
                return;
            }
            stream = newStream;
            video.srcObject = stream;
            await video.play();
            updateRoiGuide();
            video.addEventListener("loadedmetadata", updateRoiGuide);
            window.addEventListener("resize", updateRoiGuide);
            placeholder.hidden = true;
            retryButton.hidden = true;
            cameraState.textContent = "ACTIVA";
            setCameraBadge("ACTIVA", true);
            setMessage("Cámara activa. Preparando el análisis automático…");
            const [track] = stream.getVideoTracks();
            track?.addEventListener("ended", cameraDisconnected, { once: true });
            void analyzeLoop();
        } catch (error) {
            if (error.name === "NotAllowedError" || error.name === "SecurityError") {
                cameraState.textContent = "Permiso de cámara requerido";
                placeholder.querySelector("p").textContent = "Permiso de cámara requerido";
                retryButton.hidden = false;
                setMessage("Permiso de cámara requerido. Autoriza el acceso desde el navegador y reintenta.", true);
                setCameraBadge("SIN PERMISO", false);
                return;
            }

            cameraDisconnected();
        }
    };

    retryButton.addEventListener("click", () => startCamera());
    navigator.mediaDevices?.addEventListener?.("devicechange", () => {
        if (!stream) scheduleRecovery(500);
    });
    document.addEventListener("visibilitychange", () => {
        if (!document.hidden && !stream && cameraState.textContent !== "Permiso de cámara requerido") {
            scheduleRecovery(500);
        }
    });

    window.addEventListener("pagehide", () => {
        pageClosing = true;
        if (retryTimer) window.clearTimeout(retryTimer);
        if (visionTimer) window.clearInterval(visionTimer);
        apiAbortController?.abort();
        stream?.getTracks().forEach(track => track.stop());
        stream = null;
    }, { once: true });

    void pollVisionStatus();
    void startCamera();
})();

(() => {
    const station = document.getElementById("cameraStation");
    const video = document.getElementById("cameraVideo");
    const placeholder = document.getElementById("cameraPlaceholder");
    const badge = document.getElementById("cameraStatusBadge");
    const detection = document.getElementById("detectionStatus");
    const classification = document.getElementById("lastClassification");
    const confidence = document.getElementById("lastConfidence");
    const message = document.getElementById("cameraMessage");
    const startButton = document.getElementById("startCamera");
    const captureButton = document.getElementById("captureFrame");
    const stopButton = document.getElementById("stopCamera");
    const canvas = document.getElementById("captureCanvas");
    let stream = null;

    const setMessage = (text, isError = false) => {
        message.textContent = text;
        message.classList.toggle("camera-message-error", isError);
    };

    const stopCamera = () => {
        if (stream) {
            stream.getTracks().forEach(track => track.stop());
            stream = null;
        }

        video.srcObject = null;
        placeholder.hidden = false;
        badge.textContent = "APAGADA";
        badge.classList.remove("status-ready");
        badge.classList.add("status-empty");
        startButton.disabled = false;
        captureButton.disabled = true;
        stopButton.disabled = true;
    };

    startButton.addEventListener("click", async () => {
        if (!navigator.mediaDevices?.getUserMedia) {
            setMessage("Este navegador no permite acceder a la cámara. Usa HTTPS o localhost.", true);
            return;
        }

        startButton.disabled = true;
        setMessage("Solicitando permiso para usar la cámara...");
        try {
            stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
            video.srcObject = stream;
            await video.play();
            placeholder.hidden = true;
            badge.textContent = "ACTIVA";
            badge.classList.remove("status-empty");
            badge.classList.add("status-ready");
            captureButton.disabled = false;
            stopButton.disabled = false;
            detection.textContent = "Esperando residuo...";
            setMessage("Cámara activa. La detección automática todavía no está integrada.");
        } catch (error) {
            stopCamera();
            setMessage(`No se pudo iniciar la cámara: ${error.message}`, true);
        }
    });

    stopButton.addEventListener("click", () => {
        stopCamera();
        detection.textContent = "Cámara detenida";
        setMessage("La cámara se detuvo.");
    });

    captureButton.addEventListener("click", async () => {
        if (!stream || video.videoWidth === 0) {
            setMessage("Inicia la cámara antes de capturar una imagen.", true);
            return;
        }

        captureButton.disabled = true;
        detection.textContent = "Procesando captura...";
        setMessage("Enviando una captura temporal al servicio de visión...");
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;

        try {
            const blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", 0.9));
            if (!blob) throw new Error("No se pudo crear la captura.");

            const formData = new FormData();
            formData.append("imagen", blob, "captura.jpg");
            const token = document.querySelector("#cameraAntiforgery input[name='__RequestVerificationToken']").value;
            formData.append("__RequestVerificationToken", token);

            const response = await fetch(station.dataset.classifyUrl, { method: "POST", body: formData });
            const result = await response.json();
            if (!response.ok) throw new Error(result.mensaje ?? "No se pudo procesar la captura.");

            classification.textContent = result.categoria
                ? result.categoria.replaceAll("_", " ")
                : result.mensaje?.toLowerCase().includes("confianza insuficiente") ? "Confianza insuficiente" : "Sin clasificación";
            confidence.textContent = result.confianza == null ? "—" : `${(result.confianza * 100).toFixed(1)} %`;
            detection.textContent = result.registrada ? "Residuo clasificado" : "Esperando residuo...";
            setMessage(result.mensaje, !result.exito);
        } catch (error) {
            detection.textContent = "Esperando residuo...";
            setMessage(error.message, true);
        } finally {
            captureButton.disabled = !stream;
        }
    });

    window.addEventListener("pagehide", stopCamera);
})();

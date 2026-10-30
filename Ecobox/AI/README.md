# Visión artificial EcoBox

`/Camara` es una estación independiente del Dashboard. Al abrirla solicita acceso a `navigator.mediaDevices.getUserMedia()` y empieza a analizar automáticamente. Mantén esa pestaña abierta y activa durante la demostración; los navegadores pueden reducir el trabajo de una pestaña en segundo plano.

La webcam conserva un frame de fondo y compara una muestra de 32×32 tomada dentro de la ROI cada `Vision:FrameIntervalMs` (200 ms por defecto). Al aparecer un cambio relevante envía un frame JPEG; no transmite video continuo. La guía central de `/Camara` y Python usan `Vision:RoiSizeRatio` (0.85 por defecto). Python recorta un cuadrado centrado del frame antes del preprocesamiento. ASP.NET solicita la inferencia al worker persistente de `AI/Python/vision_server.py`; TensorFlow carga una vez el modelo y `clases.json` al iniciar el proceso. El pipeline compartido con `classify.py` conserva BGR→RGB, resize 224×224, `float32` y dimensión batch; el orden de clases se valida como `aprovechable`, `no_aprovechable`.

```powershell
& .\Ecobox\AI\Python\.venv\Scripts\python.exe `
  .\Ecobox\AI\Python\classify.py `
  .\Ecobox\AI\Python\test_images\botella.jpeg
```

Una categoría debe superar `Vision:ConfidenceThreshold` (0.70) y coincidir durante `Vision:StableFramesRequired` (2) frames. Un hash perceptual de la ROI comprueba que ambos frames sean visualmente compatibles; `Vision:StableFrameHashDistance` configura la distancia máxima. Tras registrar una vez, la estación queda enclavada hasta que la ROI vuelve al fondo libre; `Vision:DetectionCooldownMs` añade una pausa antes del siguiente registro. Arduino solo recibe la categoría después del registro confirmado.

El diagnóstico de timings del navegador aparece en su consola; ASP.NET registra tamaño de frame, tiempos de captura/JPEG y duración total. El worker informa coordenadas y tamaño de ROI, tiempo de preprocesamiento e inferencia. Para guardar temporalmente hasta 8 recortes 224×224 (máximo configurable de 10), establece `Vision:SaveDebugFrames` en `true`; por defecto está desactivado. Se guardan en `%TEMP%/EcoBoxVisionDebugFrames` o en `Vision:DebugFrameDirectory` si se configura una ruta. Los siguientes frames reemplazan los anteriores dentro del límite.

Los valores de Python, worker, modelo, clases, umbral, frecuencia, estabilidad y cooldown se configuran en `appsettings.json`. En este equipo `appsettings.Development.json` apunta al entorno virtual local `AI/Python/.venv`.

Para el prototipo físico final, la captura deberá ejecutarse como proceso local independiente del navegador. Una pestaña de navegador no garantiza procesamiento continuo cuando está oculta o suspendida.

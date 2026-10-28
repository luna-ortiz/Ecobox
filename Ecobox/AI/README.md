# Visión artificial EcoBox

La ruta `/Camara` usa la webcam del computador con `navigator.mediaDevices.getUserMedia()`. La vista ofrece iniciar/detener la cámara y enviar una captura temporal para clasificar. Python ejecuta `AI/Python/classify.py` con TensorFlow/Keras y el modelo `AI/Python/ecobox_modelo.keras`; el preprocesamiento conserva BGR→RGB y resize a 224×224. Una confianza menor que el umbral no registra residuos ni envía comandos al Arduino.

Instala las dependencias indicadas en `AI/Python/requirements.txt` en el entorno de Python configurado en `appsettings.json`. Las rutas de script y modelo, el ejecutable Python y el umbral se configuran en `Vision`. El servicio C# guarda cada captura temporalmente para inferencia y la elimina al terminar. La cámara no clasifica en continuo.

# Visión artificial EcoBox

La ruta `/Camara` usa la webcam del computador con `navigator.mediaDevices.getUserMedia()`. La vista es independiente del dashboard y ofrece iniciar/detener la cámara y enviar una captura temporal para clasificar. El navegador debe conceder permiso de cámara.

La detección automática de presencia y el modelo TensorFlow/Keras todavía no están integrados. `VisionService` responde `Modelo no disponible`; no guarda la imagen ni registra cantidades. El modelo se entrenará externamente en Google Colab y su formato/runtime se definirá al integrarlo. No se entrena dentro de ASP.NET.

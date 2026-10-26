# EcoBox

Prototipo ASP.NET Core MVC en C# y .NET 10 para clasificar residuos en `APROVECHABLE` y `NO_APROVECHABLE`.

## Ejecutar

Desde la raíz del repositorio:

```powershell
dotnet restore .\Ecobox.slnx
dotnet run --project .\Ecobox\Ecobox.csproj
```

En Visual Studio, selecciona `Ecobox` como proyecto de inicio y ejecútalo. Al iniciar se crea `Ecobox/Data/ecobox.db` si todavía no existe.

## Páginas

- `/Dashboard`: totales acumulados por categoría.
- `/Historial`: filas agregadas por fecha y caneca. `/Residuo` se conserva como alias.
- `/Camara`: abre en otra pestaña desde la navegación y permite iniciar/detener la webcam y capturar una imagen para procesarla.

La cámara requiere permiso del navegador y solo está activa mientras se use esta página. La detección automática de presencia y el modelo TensorFlow/Keras todavía no están integrados; en ese estado una captura muestra `Modelo no disponible` y no escribe registros ni imágenes.

El Arduino se configura en `appsettings.json`. `Arduino:Port` está vacío hasta definir el puerto real; no hay pines ni servos fijados en el código.

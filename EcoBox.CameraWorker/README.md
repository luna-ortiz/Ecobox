# EcoBox.CameraWorker

Programa auxiliar local. EcoBox MVC ya abre la aplicación Cámara de Windows automáticamente al iniciar en modo `Development`; este programa puede abrirla manualmente si hace falta.

## Requisitos

- Windows con la aplicación Cámara instalada.
- .NET 10 SDK.

## Ejecutar

Desde la raíz del repositorio:

```powershell
dotnet run --project .\EcoBox.CameraWorker\EcoBox.CameraWorker.csproj
```

La vista previa permanece activa mientras la aplicación Cámara de Windows esté abierta. Cierra esa aplicación para detenerla. EcoBox no graba video ni guarda imágenes. Aún no clasifica ni registra residuos porque el proyecto no tiene un modelo entrenado.

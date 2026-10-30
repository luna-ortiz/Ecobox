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
- `/Camara`: estación independiente que abre en otra pestaña e inicia automáticamente la webcam y el análisis.

La cámara requiere permiso del navegador. Cambios de escena detectados en frames muestreados activan inferencia; Python permanece como proceso persistente con el modelo cargado una vez. La clasificación necesita confianza suficiente y estabilidad en varios frames; cada residuo se registra una vez y el sistema espera a que se retire antes de rearmar. Consulta `Ecobox/AI/README.md` para dependencias y configuración.

El Arduino se configura en `appsettings.json`. `Arduino:Port` está vacío hasta definir el puerto real; el sketch `Ecobox/Arduino/EcoBoxArduino/EcoBoxArduino.ino` recibe `A` y `N`. La clasificación y el registro funcionan aunque el puerto no esté configurado.

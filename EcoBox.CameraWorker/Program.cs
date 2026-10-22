using System.Diagnostics;

try
{
    var cameraApp = new ProcessStartInfo("microsoft.windows.camera:")
    {
        UseShellExecute = true
    };

    Process.Start(cameraApp);
    Console.WriteLine("Se abrió la aplicación Cámara de Windows. Cierra esa ventana para detener la vista previa.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"No se pudo abrir la aplicación Cámara de Windows: {exception.Message}");
    return 1;
}

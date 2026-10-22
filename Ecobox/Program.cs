using Ecobox.Configuration;
using Ecobox.Data;
using Ecobox.Models;
using Ecobox.Services;
using Ecobox.Services.Interfaces;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<ContenedorSettings>(builder.Configuration.GetSection(ContenedorSettings.SectionName));

var connectionString = builder.Configuration.GetConnectionString("EcoBox");
builder.Services.AddDbContext<EcoBoxDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
});

builder.Services.AddScoped<IDatabaseStatusService, DatabaseStatusService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IContenedorService, ContenedorService>();
builder.Services.AddScoped<IResiduoService, ResiduoService>();
builder.Services.AddScoped<IArduinoService, ArduinoService>();

var app = builder.Build();

if (app.Environment.IsDevelopment() && OperatingSystem.IsWindows() && Environment.UserInteractive)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("EcoBox.Camera");
    try
    {
        Process.Start(new ProcessStartInfo("microsoft.windows.camera:")
        {
            UseShellExecute = true
        });
        logger.LogInformation("Se abrió la aplicación Cámara de Windows junto con EcoBox.");
    }
    catch (Exception exception)
    {
        logger.LogWarning(exception, "No se pudo abrir automáticamente la aplicación Cámara de Windows.");
    }
}

if (!string.IsNullOrWhiteSpace(connectionString))
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("EcoBox.Database");
    try
    {
        await DatabaseInitializer.InitializeAsync(
            scope.ServiceProvider.GetRequiredService<EcoBoxDbContext>(),
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ContenedorSettings>>().Value);
    }
    catch (Exception exception)
    {
        logger.LogWarning(exception, "No fue posible inicializar PostgreSQL. La aplicación seguirá disponible sin conexión a la base de datos.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

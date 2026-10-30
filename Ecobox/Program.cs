using Ecobox.Configuration;
using Ecobox.Data;
using Ecobox.Services;
using Ecobox.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    // Keep local diagnostics in the terminal instead of attempting to write to Windows Event Log.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
}

builder.Services.AddControllersWithViews();

var databaseDirectory = Path.Combine(builder.Environment.ContentRootPath, "Data");
Directory.CreateDirectory(databaseDirectory);
var databasePath = Path.Combine(databaseDirectory, "ecobox.db");

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(databaseDirectory, "DataProtection-Keys")))
    .SetApplicationName("EcoBox");

builder.Services.AddDbContext<EcoBoxDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

builder.Services.Configure<VisionSettings>(builder.Configuration.GetSection(VisionSettings.SectionName));
builder.Services.Configure<ArduinoSettings>(builder.Configuration.GetSection(ArduinoSettings.SectionName));

builder.Services.AddScoped<IRegistroResiduoService, RegistroResiduoService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddSingleton<VisionService>();
builder.Services.AddSingleton<IVisionService>(services => services.GetRequiredService<VisionService>());
builder.Services.AddSingleton<IHostedService>(services => services.GetRequiredService<VisionService>());
builder.Services.AddScoped<IArduinoService, ArduinoService>();
builder.Services.AddScoped<IPresenceDetectionService, PresenceDetectionService>();
builder.Services.AddSingleton<IDetectionGateService, DetectionGateService>();
builder.Services.AddScoped<IClasificacionResiduoService, ClasificacionResiduoService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EcoBoxDbContext>();
    await DatabaseInitializer.InitializeAsync(dbContext);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Lifetime.ApplicationStarted.Register(() =>
{
    foreach (var address in app.Urls)
    {
        Console.WriteLine($"EcoBox disponible en: {address}");
    }
});

app.Run();

using Ecobox.Configuration;
using Ecobox.Data;
using Ecobox.Models;
using Ecobox.Services;
using Ecobox.Services.Interfaces;
using Ecobox.Services.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

if (args.Length == 2 && args[0] == "--migrate-existing")
{
    var options = OptionsFor(args[1]);
    await using var db = new EcoBoxDbContext(options);
    await DatabaseInitializer.InitializeAsync(db);
    Console.WriteLine($"Migración aditiva aplicada: {Path.GetFullPath(args[1])}");
    return;
}
if (args.Length == 3 && args[0] == "--backup-existing")
{
    var sourcePath = Path.GetFullPath(args[1]);
    var backupPath = Path.GetFullPath(args[2]);
    if (File.Exists(backupPath)) throw new IOException($"El respaldo ya existe: {backupPath}");
    Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
    await using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly }.ToString());
    await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath }.ToString());
    await source.OpenAsync();
    await destination.OpenAsync();
    await using var summary = source.CreateCommand();
    summary.CommandText = "SELECT COUNT(*), COALESCE(SUM(Cantidad),0) FROM RegistroResiduo";
    await using var reader = await summary.ExecuteReaderAsync();
    await reader.ReadAsync();
    Console.WriteLine($"ANTES: filas historial={reader.GetInt64(0)}; unidades históricas={reader.GetInt64(1)}");
    await reader.DisposeAsync();
    source.BackupDatabase(destination);
    Console.WriteLine($"Respaldo SQLite creado: {backupPath}");
    return;
}
if (args.Length == 2 && args[0] == "--verify-existing")
{
    var path = Path.GetFullPath(args[1]);
    await using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString());
    await db.OpenAsync();
    await using var history = db.CreateCommand();
    history.CommandText = "SELECT COUNT(*), COALESCE(SUM(Cantidad),0) FROM RegistroResiduo";
    await using var rows = await history.ExecuteReaderAsync();
    await rows.ReadAsync();
    Console.WriteLine($"DESPUÉS: filas historial={rows.GetInt64(0)}; unidades históricas={rows.GetInt64(1)}");
    await rows.DisposeAsync();
    await using var occupancy = db.CreateCommand();
    occupancy.CommandText = """
        SELECT e.Caneca, e.UnidadesActuales, e.CicloActualId, COUNT(r.Id), COALESCE(SUM(r.Cantidad),0)
        FROM EstadoCaneca e LEFT JOIN RegistroResiduo r ON r.CicloId = e.CicloActualId
        GROUP BY e.Caneca, e.UnidadesActuales, e.CicloActualId ORDER BY e.Caneca
        """;
    await using var states = await occupancy.ExecuteReaderAsync();
    while (await states.ReadAsync()) Console.WriteLine($"{states.GetString(0)}={states.GetInt32(1)}/50; ciclo={states.GetInt32(2)}; filas actuales={states.GetInt64(3)}; unidades actuales={states.GetInt64(4)}");
    await states.DisposeAsync();
    await using var cycles = db.CreateCommand();
    cycles.CommandText = """
        SELECT c.Id, c.Caneca, c.IniciadoEn, c.FinalizadoEn, COALESCE(SUM(r.Cantidad),0)
        FROM CicloCaneca c LEFT JOIN RegistroResiduo r ON r.CicloId = c.Id
        GROUP BY c.Id ORDER BY c.Id
        """;
    await using var cycleRows = await cycles.ExecuteReaderAsync();
    while (await cycleRows.ReadAsync()) Console.WriteLine($"ciclo #{cycleRows.GetInt32(0)} {cycleRows.GetString(1)} inicio={cycleRows.GetString(2)} cierre={(cycleRows.IsDBNull(3) ? "actual" : cycleRows.GetString(3))} unidades={cycleRows.GetInt64(4)}");
    return;
}

var cases = new (string Name, Func<Task> Run)[]
{
    ("inicia vacía y suma una unidad", InitialAndIncrement),
    ("conserva una ocupación previa", ExistingOccupancy),
    ("avisa al llegar a 45/50", NearFull),
    ("acepta exactamente la unidad 50", ReachesCapacity),
    ("rechaza clasificación llena sin historial ni Arduino", FullStopsPipeline),
    ("mapea clasificaciones a las líneas seriales A y N", ArduinoCommandMapping),
    ("envía un único comando solo tras confirmar y registrar", AcceptedRegistrationSendsOneCommand),
    ("no envía comando si falla SQLite", RegistrationFailureDoesNotSend),
    ("un error serial conserva el registro confirmado", ArduinoFailurePreservesRegistration),
    ("rechaza cantidades que sobrepasan el límite", RejectsOverflow),
    ("cada caneca tiene capacidad independiente", IndependentBins),
    ("vaciar conserva y cierra el ciclo anterior", EmptyPreservesHistory),
    ("vaciar una caneca no altera la otra", EmptyOnlySelectedBin),
    ("un ciclo nuevo acepta la misma fecha sin mezclar registros", NewCycleCanRecordSameDate),
    ("el total del dashboard suma solo los ciclos actuales", DashboardTotalsByCurrentCycle),
    ("migra el historial legado sin borrar filas", MigratesLegacyHistory),
    ("persistencia de ocupación tras reabrir SQLite", PersistsAfterReopen),
    ("serializa dos intentos simultáneos al llegar al límite", ConcurrentFinalUnit)
};

var failures = 0;
foreach (var test in cases)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception}");
    }
}
Console.WriteLine($"{cases.Length - failures}/{cases.Length} pruebas correctas.");
if (failures > 0) Environment.ExitCode = 1;

static async Task InitialAndIncrement() => await WithDb(async (db, service) =>
{
    Equal(0, (await Occupancy(service, CategoriaResiduo.APROVECHABLE)).UnidadesActuales);
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE);
    Equal(1, (await Occupancy(service, CategoriaResiduo.APROVECHABLE)).UnidadesActuales);
});

static async Task ExistingOccupancy() => await WithDb(async (db, service) =>
{
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE, 20);
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE);
    Equal(21, (await Occupancy(service, CategoriaResiduo.APROVECHABLE)).UnidadesActuales);
});

static async Task NearFull() => await WithDb(async (db, service) =>
{
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE, 44);
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE);
    var state = await Occupancy(service, CategoriaResiduo.APROVECHABLE);
    Equal(45, state.UnidadesActuales);
    Equal(90, state.PorcentajeOcupacion);
    Equal("Próxima a llenarse", state.Estado);
});

static async Task ReachesCapacity() => await WithDb(async (db, service) =>
{
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE, 49);
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE);
    var state = await Occupancy(service, CategoriaResiduo.APROVECHABLE);
    Equal(50, state.UnidadesActuales);
    Equal("Llena", state.Estado);
});

static async Task FullStopsPipeline() => await WithDb(async (db, service) =>
{
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE, 50);
    var historyCount = (await service.ObtenerHistorialAsync()).Count;
    var vision = new FixedVision();
    var arduino = new CountingArduino();
    var settings = Options.Create(new VisionSettings { ConfidenceThreshold = 0.7, StableFramesRequired = 2 });
    var gate = new DetectionGateService(settings, NullLogger<DetectionGateService>.Instance);
    var classifier = new ClasificacionResiduoService(vision, service, arduino, settings, gate, NullLogger<ClasificacionResiduoService>.Instance);
    await using var first = new MemoryStream([1]);
    await classifier.ProcesarImagenAsync(first);
    Equal(0, arduino.Calls);
    await using var second = new MemoryStream([1]);
    var response = await classifier.ProcesarImagenAsync(second);
    Equal("caneca_llena", response.Estado);
    Equal(50, (await Occupancy(service, CategoriaResiduo.APROVECHABLE)).UnidadesActuales);
    Equal(historyCount, (await service.ObtenerHistorialAsync()).Count);
    Equal(0, arduino.Calls);
});

static Task ArduinoCommandMapping()
{
    Equal("A\n", ArduinoService.MapearComandoSerial("APROVECHABLE")!);
    Equal("A\n", ArduinoService.MapearComandoSerial("aprovechable")!);
    Equal("A\n", ArduinoService.MapearComandoSerial("A")!);
    Equal("N\n", ArduinoService.MapearComandoSerial("NO_APROVECHABLE")!);
    Equal("N\n", ArduinoService.MapearComandoSerial("NO APROVECHABLE")!);
    Equal("N\n", ArduinoService.MapearComandoSerial("no_aprovechable")!);
    Equal("N\n", ArduinoService.MapearComandoSerial("N")!);
    True(ArduinoService.MapearComandoSerial("OTRA") is null, "Se aceptó una categoría no válida.");
    return Task.CompletedTask;
}

static async Task AcceptedRegistrationSendsOneCommand() => await WithDb(async (db, service) =>
{
    var settings = Options.Create(new VisionSettings { ConfidenceThreshold = 0.7, StableFramesRequired = 2 });
    var arduino = new CountingArduino();
    var gate = new DetectionGateService(settings, NullLogger<DetectionGateService>.Instance);
    var classifier = new ClasificacionResiduoService(new FixedVision(), service, arduino, settings, gate, NullLogger<ClasificacionResiduoService>.Instance);
    await using var first = new MemoryStream([1]);
    var pending = await classifier.ProcesarImagenAsync(first);
    Equal("primer_frame", pending.Estado);
    Equal(0, arduino.Calls);
    await using var second = new MemoryStream([1]);
    var accepted = await classifier.ProcesarImagenAsync(second);
    True(accepted.RegistroGuardado, "El residuo confirmado no se registró.");
    Equal(1, arduino.Calls);
    Equal(1, (await service.ObtenerHistorialAsync()).Sum(row => row.Cantidad));
});

static async Task RegistrationFailureDoesNotSend()
{
    var settings = Options.Create(new VisionSettings { ConfidenceThreshold = 0.7, StableFramesRequired = 2 });
    var arduino = new CountingArduino();
    var gate = new DetectionGateService(settings, NullLogger<DetectionGateService>.Instance);
    var classifier = new ClasificacionResiduoService(new FixedVision(), new FailingRegistrationService(), arduino, settings, gate, NullLogger<ClasificacionResiduoService>.Instance);
    await using var first = new MemoryStream([1]);
    await classifier.ProcesarImagenAsync(first);
    await using var second = new MemoryStream([1]);
    var failed = await classifier.ProcesarImagenAsync(second);
    Equal("error_registro", failed.Estado);
    Equal(0, arduino.Calls);
}

static async Task ArduinoFailurePreservesRegistration() => await WithDb(async (db, service) =>
{
    var settings = Options.Create(new VisionSettings { ConfidenceThreshold = 0.7, StableFramesRequired = 2 });
    var gate = new DetectionGateService(settings, NullLogger<DetectionGateService>.Instance);
    var classifier = new ClasificacionResiduoService(new FixedVision(), service, new UnavailableArduino(), settings, gate, NullLogger<ClasificacionResiduoService>.Instance);
    await using var first = new MemoryStream([1]);
    await classifier.ProcesarImagenAsync(first);
    await using var second = new MemoryStream([1]);
    var result = await classifier.ProcesarImagenAsync(second);
    True(result.RegistroGuardado, "El resultado reportó perdido el registro después del fallo serial.");
    Equal(1, (await service.ObtenerHistorialAsync()).Sum(row => row.Cantidad));
    Equal(1, (await Occupancy(service, CategoriaResiduo.APROVECHABLE)).UnidadesActuales);
});

static async Task RejectsOverflow() => await WithDb(async (db, service) =>
{
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE, 49);
    await Throws<CanecaLlenaException>(() => service.RegistrarAsync(CategoriaResiduo.APROVECHABLE, 2));
    Equal(49, (await Occupancy(service, CategoriaResiduo.APROVECHABLE)).UnidadesActuales);
});

static async Task IndependentBins() => await WithDb(async (db, service) =>
{
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE, 50);
    await service.RegistrarAsync(CategoriaResiduo.NO_APROVECHABLE, 10);
    await service.RegistrarAsync(CategoriaResiduo.NO_APROVECHABLE);
    Equal(50, (await Occupancy(service, CategoriaResiduo.APROVECHABLE)).UnidadesActuales);
    Equal(11, (await Occupancy(service, CategoriaResiduo.NO_APROVECHABLE)).UnidadesActuales);
});

static async Task EmptyPreservesHistory() => await WithDb(async (db, service) =>
{
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE, 8);
    var before = (await service.ObtenerHistorialAsync()).Single();
    var cycleBefore = (await Occupancy(service, CategoriaResiduo.APROVECHABLE)).CicloActualId;
    await service.VaciarAsync(CategoriaResiduo.APROVECHABLE);
    var state = await Occupancy(service, CategoriaResiduo.APROVECHABLE);
    Equal(0, state.UnidadesActuales);
    Equal(0, state.UnidadesCicloActuales);
    True(state.UltimoVaciadoEn.HasValue, "No quedó fecha de vaciado.");
    True(state.CicloActualId != cycleBefore, "El vaciado no abrió un ciclo nuevo.");
    var historyAfter = await service.ObtenerHistorialAsync();
    Equal(1, historyAfter.Count);
    Equal(before.Cantidad, historyAfter.Single().Cantidad);
    Equal(cycleBefore, historyAfter.Single().CicloId);
    var cycles = await service.ObtenerCiclosAsync();
    True(cycles.Single(cycle => cycle.Id == cycleBefore).FinalizadoEn.HasValue, "El ciclo anterior no se cerró.");
    True(cycles.Single(cycle => cycle.Id == state.CicloActualId).FinalizadoEn is null, "El ciclo nuevo no quedó activo.");
});

static async Task EmptyOnlySelectedBin() => await WithDb(async (db, service) =>
{
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE, 4);
    await service.RegistrarAsync(CategoriaResiduo.NO_APROVECHABLE, 7);
    var otherCycle = (await Occupancy(service, CategoriaResiduo.NO_APROVECHABLE)).CicloActualId;
    await service.VaciarAsync(CategoriaResiduo.APROVECHABLE);
    Equal(0, (await Occupancy(service, CategoriaResiduo.APROVECHABLE)).UnidadesActuales);
    var other = await Occupancy(service, CategoriaResiduo.NO_APROVECHABLE);
    Equal(7, other.UnidadesActuales);
    Equal(7, other.UnidadesCicloActuales);
    Equal(otherCycle, other.CicloActualId);
});

static async Task NewCycleCanRecordSameDate() => await WithDb(async (db, service) =>
{
    var date = DateOnly.FromDateTime(DateTime.UtcNow);
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE, 2, date);
    var previousCycle = (await Occupancy(service, CategoriaResiduo.APROVECHABLE)).CicloActualId;
    await service.VaciarAsync(CategoriaResiduo.APROVECHABLE);
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE, 1, date);
    var history = await service.ObtenerHistorialAsync();
    Equal(2, history.Count);
    Equal(3, history.Sum(item => item.Cantidad));
    True(history.Select(item => item.CicloId).Distinct().Count() == 2, "Los ciclos se mezclaron en el historial.");
    Equal(previousCycle, history.Single(item => item.CicloId == previousCycle).CicloId);
    Equal(1, (await Occupancy(service, CategoriaResiduo.APROVECHABLE)).UnidadesCicloActuales);
});

static async Task DashboardTotalsByCurrentCycle() => await WithDb(async (db, service) =>
{
    await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE, 2);
    await service.RegistrarAsync(CategoriaResiduo.NO_APROVECHABLE, 3);
    var dashboard = new DashboardService(db, service);
    Equal(5, (await dashboard.GetDashboardAsync()).TotalResiduos);
    await service.VaciarAsync(CategoriaResiduo.APROVECHABLE);
    var updated = await dashboard.GetDashboardAsync();
    Equal(3, updated.TotalResiduos);
    Equal(0, updated.TotalAprovechables);
    Equal(3, updated.TotalNoAprovechables);
});

static async Task MigratesLegacyHistory()
{
    var path = TempDbPath();
    try
    {
        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE "RegistroResiduo" ("Id" INTEGER NOT NULL CONSTRAINT "PK_RegistroResiduo" PRIMARY KEY AUTOINCREMENT, "Fecha" TEXT NOT NULL, "Caneca" TEXT NOT NULL, "Cantidad" INTEGER NOT NULL);
                CREATE UNIQUE INDEX "IX_RegistroResiduo_Fecha_Caneca" ON "RegistroResiduo" ("Fecha", "Caneca");
                INSERT INTO "RegistroResiduo" ("Fecha", "Caneca", "Cantidad") VALUES ('2026-10-30', 'APROVECHABLE', 5), ('2026-10-30', 'NO_APROVECHABLE', 3);
                CREATE TABLE "EstadoCaneca" ("Caneca" TEXT NOT NULL CONSTRAINT "PK_EstadoCaneca" PRIMARY KEY, "UnidadesActuales" INTEGER NOT NULL, "ActualizadoEn" TEXT NOT NULL, "UltimoVaciadoEn" TEXT NULL, CONSTRAINT "CK_EstadoCaneca_UnidadesActuales" CHECK ("UnidadesActuales" >= 0 AND "UnidadesActuales" <= 50));
                INSERT INTO "EstadoCaneca" VALUES ('APROVECHABLE', 5, '2026-10-30T00:00:00+00:00', NULL), ('NO_APROVECHABLE', 3, '2026-10-30T00:00:00+00:00', NULL);
                """;
            await command.ExecuteNonQueryAsync();
        }

        await using (var db = new EcoBoxDbContext(OptionsFor(path)))
        {
            await DatabaseInitializer.InitializeAsync(db);
            var service = new RegistroResiduoService(db);
            Equal(2, (await service.ObtenerHistorialAsync()).Count);
            Equal(8, (await service.ObtenerHistorialAsync()).Sum(item => item.Cantidad));
            var cycles = await service.ObtenerCiclosAsync();
            Equal(4, cycles.Count);
            Equal(2, cycles.Count(cycle => cycle.FinalizadoEn is null));
            True((await service.ObtenerHistorialAsync()).All(item => item.CicloId > 0), "Quedaron registros sin ciclo asignado.");
            await service.RegistrarAsync(CategoriaResiduo.APROVECHABLE);
            Equal(6, (await service.ObtenerHistorialAsync()).Where(item => item.Caneca == CategoriaResiduo.APROVECHABLE).Sum(item => item.Cantidad));
        }
    }
    finally { DeleteDb(path); }
}

static async Task PersistsAfterReopen()
{
    var path = TempDbPath();
    try
    {
        await using (var db = new EcoBoxDbContext(OptionsFor(path)))
        {
            await DatabaseInitializer.InitializeAsync(db);
            await new RegistroResiduoService(db).RegistrarAsync(CategoriaResiduo.NO_APROVECHABLE, 17);
        }
        await using (var reopened = new EcoBoxDbContext(OptionsFor(path)))
            Equal(17, (await Occupancy(new RegistroResiduoService(reopened), CategoriaResiduo.NO_APROVECHABLE)).UnidadesActuales);
    }
    finally { DeleteDb(path); }
}

static async Task ConcurrentFinalUnit()
{
    var path = TempDbPath();
    try
    {
        await using (var db = new EcoBoxDbContext(OptionsFor(path)))
        {
            await DatabaseInitializer.InitializeAsync(db);
            await new RegistroResiduoService(db).RegistrarAsync(CategoriaResiduo.APROVECHABLE, 49);
        }
        async Task<bool> Attempt()
        {
            await using var context = new EcoBoxDbContext(OptionsFor(path));
            try { await new RegistroResiduoService(context).RegistrarAsync(CategoriaResiduo.APROVECHABLE); return true; }
            catch (CanecaLlenaException) { return false; }
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        Equal(1, results.Count(result => result));
        await using (var verify = new EcoBoxDbContext(OptionsFor(path)))
        {
            Equal(50, (await Occupancy(new RegistroResiduoService(verify), CategoriaResiduo.APROVECHABLE)).UnidadesActuales);
            Equal(1, (await verify.RegistrosResiduo.SingleAsync()).Cantidad - 49);
        }
    }
    finally { DeleteDb(path); }
}

static async Task WithDb(Func<EcoBoxDbContext, RegistroResiduoService, Task> test)
{
    var path = TempDbPath();
    try
    {
        await using (var db = new EcoBoxDbContext(OptionsFor(path)))
        {
            await DatabaseInitializer.InitializeAsync(db);
            await test(db, new RegistroResiduoService(db));
        }
    }
    finally { DeleteDb(path); }
}

static DbContextOptions<EcoBoxDbContext> OptionsFor(string path) => new DbContextOptionsBuilder<EcoBoxDbContext>()
    .UseSqlite($"Data Source={path};Default Timeout=30;Pooling=False")
    .Options;

static string TempDbPath() => Path.Combine(Path.GetTempPath(), $"ecobox-capacity-{Guid.NewGuid():N}.db");
static void DeleteDb(string path) { foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix); }
static async Task<CanecaOcupacionResult> Occupancy(RegistroResiduoService service, CategoriaResiduo caneca) =>
    (await service.ObtenerOcupacionesAsync()).Single(state => state.Caneca == caneca);
static void True(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
static void Equal<T>(T expected, T actual) where T : notnull { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Esperado {expected}; obtenido {actual}."); }
static async Task Throws<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T) { return; } throw new InvalidOperationException($"Se esperaba {typeof(T).Name}."); }

sealed class FixedVision : IVisionService
{
    private int _calls;
    public bool IsReady => true;
    public string StatusMessage => "test";
    public Task<VisionResult> ClasificarAsync(Stream imagen, CancellationToken cancellationToken = default) => Task.FromResult(new VisionResult
    {
        Exito = true, Categoria = nameof(CategoriaResiduo.APROVECHABLE), Confianza = 0.95m,
        RoiHash = "0000000000000000", Mensaje = "ok", Prediccion = $"test-{++_calls}"
    });
}

sealed class CountingArduino : IArduinoService
{
    public int Calls { get; private set; }
    public Task<ArduinoResult> EnviarClasificacionAsync(string clasificacion, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(new ArduinoResult { Exito = true });
    }
}

sealed class FailingRegistrationService : IRegistroResiduoService
{
    public Task<IReadOnlyList<RegistroResiduo>> ObtenerHistorialAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RegistroResiduo>>([]);
    public Task<RegistroResiduo> RegistrarAsync(CategoriaResiduo caneca, int cantidad = 1, DateOnly? fecha = null, CancellationToken cancellationToken = default) =>
        Task.FromException<RegistroResiduo>(new InvalidOperationException("SQLite simulated failure"));
    public Task<IReadOnlyList<CanecaOcupacionResult>> ObtenerOcupacionesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CanecaOcupacionResult>>([]);
    public Task<IReadOnlyList<CicloCanecaResult>> ObtenerCiclosAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CicloCanecaResult>>([]);
    public Task VaciarAsync(CategoriaResiduo caneca, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class UnavailableArduino : IArduinoService
{
    public Task<ArduinoResult> EnviarClasificacionAsync(string clasificacion, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ArduinoResult { Exito = false, Mensaje = "Puerto ocupado o ESP32 desconectado." });
}

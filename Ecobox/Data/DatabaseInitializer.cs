using System.Data.Common;
using Ecobox.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Ecobox.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(EcoBoxDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "EstadoCaneca" (
                "Caneca" TEXT NOT NULL CONSTRAINT "PK_EstadoCaneca" PRIMARY KEY,
                "UnidadesActuales" INTEGER NOT NULL,
                "ActualizadoEn" TEXT NOT NULL,
                "UltimoVaciadoEn" TEXT NULL,
                "CicloActualId" INTEGER NOT NULL DEFAULT 0,
                CONSTRAINT "CK_EstadoCaneca_UnidadesActuales"
                    CHECK ("UnidadesActuales" >= 0 AND "UnidadesActuales" <= 50)
            );
            CREATE TABLE IF NOT EXISTS "CicloCaneca" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_CicloCaneca" PRIMARY KEY AUTOINCREMENT,
                "Caneca" TEXT NOT NULL,
                "IniciadoEn" TEXT NOT NULL,
                "FinalizadoEn" TEXT NULL
            );
            """,
            cancellationToken);

        if (!await HasColumnAsync(dbContext, "RegistroResiduo", "CicloId", cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"RegistroResiduo\" ADD COLUMN \"CicloId\" INTEGER NOT NULL DEFAULT 0;",
                cancellationToken);
        }
        if (!await HasColumnAsync(dbContext, "EstadoCaneca", "CicloActualId", cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"EstadoCaneca\" ADD COLUMN \"CicloActualId\" INTEGER NOT NULL DEFAULT 0;",
                cancellationToken);
        }

        var ahora = DateTimeOffset.UtcNow;
        foreach (var caneca in Enum.GetValues<CategoriaResiduo>())
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT OR IGNORE INTO \"EstadoCaneca\" (\"Caneca\", \"UnidadesActuales\", \"ActualizadoEn\", \"UltimoVaciadoEn\", \"CicloActualId\") VALUES ({caneca.ToString()}, 0, {ahora}, NULL, 0);",
                cancellationToken);

            var legacyStart = await ScalarStringAsync(dbContext,
                $"SELECT MIN(\"Fecha\") FROM \"RegistroResiduo\" WHERE \"Caneca\" = '{caneca}' AND \"CicloId\" = 0;",
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(legacyStart))
            {
                var startDate = DateOnly.Parse(legacyStart, System.Globalization.CultureInfo.InvariantCulture);
                var legacyStartedAt = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO \"CicloCaneca\" (\"Caneca\", \"IniciadoEn\", \"FinalizadoEn\") VALUES ({caneca.ToString()}, {legacyStartedAt}, {ahora});",
                    cancellationToken);
                var legacyCycleId = await ScalarIntAsync(dbContext, "SELECT last_insert_rowid();", cancellationToken);
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"RegistroResiduo\" SET \"CicloId\" = {legacyCycleId} WHERE \"Caneca\" = {caneca.ToString()} AND \"CicloId\" = 0;",
                    cancellationToken);
            }

            var currentCycleId = await ScalarIntAsync(dbContext,
                $"SELECT \"CicloActualId\" FROM \"EstadoCaneca\" WHERE \"Caneca\" = '{caneca}';",
                cancellationToken);
            var currentCycleExists = currentCycleId > 0 && await ScalarIntAsync(dbContext,
                $"SELECT COUNT(*) FROM \"CicloCaneca\" WHERE \"Id\" = {currentCycleId} AND \"Caneca\" = '{caneca}' AND \"FinalizadoEn\" IS NULL;",
                cancellationToken) > 0;
            if (!currentCycleExists)
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO \"CicloCaneca\" (\"Caneca\", \"IniciadoEn\", \"FinalizadoEn\") VALUES ({caneca.ToString()}, {ahora}, NULL);",
                    cancellationToken);
                currentCycleId = await ScalarIntAsync(dbContext, "SELECT last_insert_rowid();", cancellationToken);
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"EstadoCaneca\" SET \"CicloActualId\" = {currentCycleId} WHERE \"Caneca\" = {caneca.ToString()};",
                    cancellationToken);
            }
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DROP INDEX IF EXISTS "IX_RegistroResiduo_Fecha_Caneca";
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RegistroResiduo_CicloId_Fecha_Caneca"
                ON "RegistroResiduo" ("CicloId", "Fecha", "Caneca");
            CREATE INDEX IF NOT EXISTS "IX_CicloCaneca_Caneca_FinalizadoEn"
                ON "CicloCaneca" ("Caneca", "FinalizadoEn");
            """,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<bool> HasColumnAsync(EcoBoxDbContext dbContext, string table, string column, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static async Task<string?> ScalarStringAsync(EcoBoxDbContext dbContext, string sql, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(dbContext, sql);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<int> ScalarIntAsync(EcoBoxDbContext dbContext, string sql, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(dbContext, sql);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static DbCommand CreateCommand(EcoBoxDbContext dbContext, string sql)
    {
        var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = sql;
        return command;
    }
}

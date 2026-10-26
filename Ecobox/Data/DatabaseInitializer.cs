using Microsoft.EntityFrameworkCore;

namespace Ecobox.Data;

public static class DatabaseInitializer
{
    public static Task InitializeAsync(EcoBoxDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.Database.EnsureCreatedAsync(cancellationToken);
}

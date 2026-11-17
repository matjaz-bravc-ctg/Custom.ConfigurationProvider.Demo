using Custom.Configuration.Provider.Demo.Data;
using Custom.Configuration.Provider.Demo.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Custom.ConfigurationProvider.Demo.Tests;

public sealed class DatabaseSetupTests : IDisposable
{
  private readonly TestDatabase _database = new();

  private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

  public void Dispose() => _database.Dispose();

  [Fact]
  public async Task Migrate_CreatesDatabase_ThatMatchesTheModel()
  {
    await DatabaseSetup.MigrateAsync(_database.ConnectionString, CancellationToken);

    await using DatabaseContext context = _database.CreateContext();
    Assert.Equal(context.Database.GetMigrations(), await context.Database.GetAppliedMigrationsAsync(CancellationToken));
    Assert.False(context.Database.HasPendingModelChanges(), "The EF Core model has changes that are not in a migration.");
  }

  [Fact]
  public async Task Migrate_EnablesWriteAheadLogging()
  {
    // Without it, a save can fail with "database is locked" while the configuration provider reads
    await DatabaseSetup.MigrateAsync(_database.ConnectionString, CancellationToken);

    await using DatabaseContext context = _database.CreateContext();
    string journalMode = await context.Database.SqlQueryRaw<string>("SELECT journal_mode AS Value FROM pragma_journal_mode").SingleAsync(CancellationToken);
    Assert.Equal("wal", journalMode);
  }

  [Fact]
  public async Task Migrate_AdoptsDatabaseCreatedWithEnsureCreated()
  {
    // Earlier versions created the database with EnsureCreated, which leaves no migrations history
    await using (DatabaseContext context = _database.CreateContext())
    {
      await context.Database.EnsureCreatedAsync(CancellationToken);
      context.AppSettingsCustomItems.Add(new AppSettingsCustomEntity { CustomSettingA = "Kept", Default = true });
      await context.SaveChangesAsync(CancellationToken);
    }

    await DatabaseSetup.MigrateAsync(_database.ConnectionString, CancellationToken);

    await using DatabaseContext check = _database.CreateContext();
    Assert.Equal(check.Database.GetMigrations(), await check.Database.GetAppliedMigrationsAsync(CancellationToken));
    Assert.Equal("Kept", (await check.AppSettingsCustomItems.SingleAsync(CancellationToken)).CustomSettingA);
  }

  [Fact]
  public void ResolveSqliteConnectionString_MakesPathAbsoluteToContentRoot_AndCreatesFolder()
  {
    string contentRoot = Path.Combine(Path.GetTempPath(), $"custom-config-provider-root-{Guid.NewGuid():N}");
    try
    {
      string connectionString = DatabaseSetup.ResolveSqliteConnectionString("Data Source=./Database/Demo.db", contentRoot);

      Assert.Equal($"Data Source={Path.Combine(contentRoot, "Database", "Demo.db")}", connectionString);
      Assert.True(Directory.Exists(Path.Combine(contentRoot, "Database")));
    }
    finally
    {
      Directory.Delete(contentRoot, recursive: true);
    }
  }
}

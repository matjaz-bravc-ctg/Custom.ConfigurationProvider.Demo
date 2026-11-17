using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Custom.Configuration.Provider.Demo.Data;

public static class DatabaseSetup
{
  private const string SettingsTable = "AppCustomSettings";

  /// <summary>
  /// Makes a relative SQLite file path absolute (against the content root) and creates its folder,
  /// so the database ends up in the same place whatever the current directory is
  /// </summary>
  public static string ResolveSqliteConnectionString(string? connectionString, string contentRootPath)
  {
    if (string.IsNullOrWhiteSpace(connectionString))
    {
      throw new InvalidOperationException("Connection string 'DemoDatabase' is missing.");
    }

    SqliteConnectionStringBuilder builder = new(connectionString);
    if (builder.Mode != SqliteOpenMode.Memory && builder.DataSource is not ("" or ":memory:"))
    {
      builder.DataSource = Path.GetFullPath(builder.DataSource, contentRootPath);
      Directory.CreateDirectory(Path.GetDirectoryName(builder.DataSource)!);
    }
    return builder.ToString();
  }

  /// <summary>
  /// Creates the database or applies pending migrations
  /// </summary>
  public static async Task MigrateAsync(string connectionString, CancellationToken cancellationToken = default)
  {
    await using DatabaseContext context = new(new DbContextOptionsBuilder<DatabaseContext>().UseSqlite(connectionString).Options);

    // Write-ahead logging lets readers (such as the configuration provider) and a writer work at the same time.
    // The setting is stored in the database file.
    await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = 'wal';", cancellationToken);

    await AdoptDatabaseCreatedWithoutMigrationsAsync(context, cancellationToken);
    await context.Database.MigrateAsync(cancellationToken);
  }

  /// <summary>
  /// Earlier versions of this demo created the database with EnsureCreated, which leaves no migrations history.
  /// Such a database already has the table of the initial migration, so it is recorded as applied.
  /// </summary>
  private static async Task AdoptDatabaseCreatedWithoutMigrationsAsync(DatabaseContext context, CancellationToken cancellationToken)
  {
    IHistoryRepository history = context.GetService<IHistoryRepository>();
    if (await history.ExistsAsync(cancellationToken) || !await TableExistsAsync(context, SettingsTable, cancellationToken))
    {
      return;
    }

    string initialMigration = context.Database.GetMigrations().First();
    await context.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), cancellationToken);
    await context.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(initialMigration, ProductInfo.GetVersion())), cancellationToken);
  }

  private static async Task<bool> TableExistsAsync(DatabaseContext context, string table, CancellationToken cancellationToken) =>
    await context.Database
      .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = {table}")
      .SingleAsync(cancellationToken) > 0;
}

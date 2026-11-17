using Custom.Configuration.Provider.Demo.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Custom.ConfigurationProvider.Demo.Tests;

/// <summary>
/// A SQLite database in a temporary file, deleted on dispose
/// </summary>
public sealed class TestDatabase : IDisposable
{
  private readonly string _path = Path.Combine(Path.GetTempPath(), $"custom-config-provider-tests-{Guid.NewGuid():N}.db");

  public string ConnectionString => $"Data Source={_path}";

  public DatabaseContext CreateContext() =>
    new(new DbContextOptionsBuilder<DatabaseContext>().UseSqlite(ConnectionString).Options);

  public void Dispose()
  {
    SqliteConnection.ClearAllPools();
    foreach (string suffix in new[] { "", "-shm", "-wal" })
    {
      File.Delete(_path + suffix);
    }
  }
}

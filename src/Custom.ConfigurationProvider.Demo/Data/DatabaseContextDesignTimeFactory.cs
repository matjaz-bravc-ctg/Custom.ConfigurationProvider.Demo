using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Custom.Configuration.Provider.Demo.Data;

/// <summary>
/// Lets the EF Core tools (for example dotnet ef migrations add) create the context without starting the application
/// </summary>
public sealed class DatabaseContextDesignTimeFactory : IDesignTimeDbContextFactory<DatabaseContext>
{
  public DatabaseContext CreateDbContext(string[] args) =>
    new(new DbContextOptionsBuilder<DatabaseContext>().UseSqlite("Data Source=./Database/DemoDatabase.db").Options);
}

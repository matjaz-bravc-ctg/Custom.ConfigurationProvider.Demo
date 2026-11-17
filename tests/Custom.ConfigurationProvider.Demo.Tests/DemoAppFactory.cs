using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Custom.ConfigurationProvider.Demo.Tests;

/// <summary>
/// Runs the real application in memory on its own temporary database
/// </summary>
public sealed class DemoAppFactory : WebApplicationFactory<Program>
{
  private readonly TestDatabase _database = new();

  protected override void ConfigureWebHost(IWebHostBuilder builder) =>
    builder.UseSetting("ConnectionStrings:DemoDatabase", _database.ConnectionString);

  public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  public override async ValueTask DisposeAsync()
  {
    await base.DisposeAsync();
    _database.Dispose();
  }
}

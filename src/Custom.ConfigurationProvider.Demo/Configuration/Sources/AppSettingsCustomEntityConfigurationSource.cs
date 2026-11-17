using Custom.Configuration.Provider.Demo.Configuration.Providers;
using Microsoft.EntityFrameworkCore;

namespace Custom.Configuration.Provider.Demo.Configuration.Sources;

public sealed class AppSettingsCustomEntityConfigurationSource : IConfigurationSource
{
  /// <summary>
  /// Configures the database the provider reads from
  /// </summary>
  public required Action<DbContextOptionsBuilder> OptionsAction { get; init; }

  /// <summary>
  /// Reload the values whenever a DatabaseContext saves changes to AppSettingsCustomEntity
  /// </summary>
  public bool ReloadOnChange { get; init; } = true;

  /// <summary>
  /// Called when a reload fails. The provider keeps the values from the last successful load.
  /// </summary>
  public Action<Exception>? OnReloadException { get; init; }

  public IConfigurationProvider Build(IConfigurationBuilder builder) => new AppSettingsCustomEntityConfigurationProvider(this);
}

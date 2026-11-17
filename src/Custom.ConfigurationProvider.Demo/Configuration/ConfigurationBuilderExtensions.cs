using Custom.Configuration.Provider.Demo.Configuration.Sources;
using Microsoft.EntityFrameworkCore;

namespace Custom.Configuration.Provider.Demo.Configuration;

public static class ConfigurationBuilderExtensions
{
  /// <summary>
  /// Adds the default AppSettingsCustomEntity from the database as configuration.
  /// Add it after appsettings.json, so its values override the ones from the file.
  /// </summary>
  public static IConfigurationBuilder AddAppSettingsCustomEntityConfiguration(
    this IConfigurationBuilder builder,
    Action<DbContextOptionsBuilder> optionsAction,
    bool reloadOnChange = true,
    Action<Exception>? onReloadException = null) =>
    builder.Add(new AppSettingsCustomEntityConfigurationSource
    {
      OptionsAction = optionsAction,
      ReloadOnChange = reloadOnChange,
      OnReloadException = onReloadException
    });
}

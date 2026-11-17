namespace Custom.Configuration.Provider.Demo.Configuration;

/// <summary>
/// Settings with defaults in appsettings.json, overridden by the default configuration stored in the database
/// </summary>
public sealed class AppSettingsCustom
{
  public const string SectionName = nameof(AppSettingsCustom);

  public string? CustomSettingA { get; set; }

  public string? CustomSettingB { get; set; }
}

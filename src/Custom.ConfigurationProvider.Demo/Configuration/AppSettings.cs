namespace Custom.Configuration.Provider.Demo.Configuration;

/// <summary>
/// Static settings, read only from appsettings.json
/// </summary>
public sealed class AppSettings
{
  public const string SectionName = nameof(AppSettings);

  public string? SettingA { get; set; }

  public string? SettingB { get; set; }
}

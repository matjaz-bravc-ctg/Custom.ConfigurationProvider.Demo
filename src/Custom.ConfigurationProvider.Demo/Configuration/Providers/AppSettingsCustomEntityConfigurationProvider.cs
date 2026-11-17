using Custom.Configuration.Provider.Demo.Configuration.Sources;
using Custom.Configuration.Provider.Demo.Data;
using Custom.Configuration.Provider.Demo.Data.Entities;
using Custom.Configuration.Provider.Demo.Data.Entities.Common;
using Microsoft.EntityFrameworkCore;

namespace Custom.Configuration.Provider.Demo.Configuration.Providers;

/// <summary>
/// Exposes the default AppSettingsCustomEntity row as the AppSettingsCustom configuration section
/// </summary>
/// <remarks>
/// Configuration is a flat list of keys, where a colon (:) separates the levels of the hierarchy.
/// Keys that this provider does not set fall through to the sources added before it (appsettings.json).
/// </remarks>
public sealed class AppSettingsCustomEntityConfigurationProvider : ConfigurationProvider, IDisposable
{
  private static readonly string CustomSettingAKey = ConfigurationPath.Combine(AppSettingsCustom.SectionName, nameof(AppSettingsCustom.CustomSettingA));
  private static readonly string CustomSettingBKey = ConfigurationPath.Combine(AppSettingsCustom.SectionName, nameof(AppSettingsCustom.CustomSettingB));

  private readonly AppSettingsCustomEntityConfigurationSource _source;
  private readonly Lock _reloadLock = new();

  public AppSettingsCustomEntityConfigurationProvider(AppSettingsCustomEntityConfigurationSource source)
  {
    _source = source;
    if (_source.ReloadOnChange)
    {
      EntityChangeObserver.Instance.Changed += OnEntitiesChanged;
    }
  }

  /// <summary>
  /// Loads the values at startup. Errors are not caught: the application cannot run without its database.
  /// </summary>
  public override void Load() => Data = ReadSettings();

  public void Dispose() => EntityChangeObserver.Instance.Changed -= OnEntitiesChanged;

  private void OnEntitiesChanged(object? sender, EntityChangeEventArgs e)
  {
    if (!e.EntityTypes.Contains(typeof(AppSettingsCustomEntity)))
    {
      return;
    }

    try
    {
      // One reload at a time, so an older read can never overwrite a newer one.
      // The new values are read into their own dictionary and then swapped in,
      // so readers never see a half-filled dictionary.
      lock (_reloadLock)
      {
        Data = ReadSettings();
      }
    }
    catch (Exception exception)
    {
      _source.OnReloadException?.Invoke(exception);
      return;
    }

    // Tells IOptionsMonitor<T> and other change token consumers that the values changed
    OnReload();
  }

  private Dictionary<string, string?> ReadSettings()
  {
    DbContextOptionsBuilder<DatabaseContext> builder = new();
    _source.OptionsAction(builder);
    using DatabaseContext context = new(builder.Options);

    AppSettingsCustomEntity? settings = context.AppSettingsCustomItems
      .AsNoTracking()
      .Where(entity => entity.Default)
      .OrderBy(entity => entity.Id)
      .FirstOrDefault();

    Dictionary<string, string?> data = new(StringComparer.OrdinalIgnoreCase);
    if (settings?.CustomSettingA is { } customSettingA)
    {
      data[CustomSettingAKey] = customSettingA;
    }
    if (settings?.CustomSettingB is { } customSettingB)
    {
      data[CustomSettingBKey] = customSettingB;
    }
    return data;
  }
}

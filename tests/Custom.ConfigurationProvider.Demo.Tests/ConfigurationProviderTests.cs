using Custom.Configuration.Provider.Demo.Configuration;
using Custom.Configuration.Provider.Demo.Data;
using Custom.Configuration.Provider.Demo.Data.Entities;
using Custom.Configuration.Provider.Demo.Data.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Custom.ConfigurationProvider.Demo.Tests;

/// <summary>
/// The configuration provider on its own, without the web application
/// </summary>
public sealed class ConfigurationProviderTests : IAsyncLifetime
{
  private const string KeyA = "AppSettingsCustom:CustomSettingA";
  private const string KeyB = "AppSettingsCustom:CustomSettingB";

  private readonly TestDatabase _database = new();

  public async ValueTask InitializeAsync() =>
    await DatabaseSetup.MigrateAsync(_database.ConnectionString, TestContext.Current.CancellationToken);

  public ValueTask DisposeAsync()
  {
    _database.Dispose();
    return ValueTask.CompletedTask;
  }

  [Fact]
  public void EmptyDatabase_FallsBackToEarlierSources()
  {
    using ConfigurationRoot configuration = BuildConfiguration();

    Assert.Equal("File A", configuration[KeyA]);
    Assert.Equal("File B", configuration[KeyB]);
  }

  [Fact]
  public void DefaultRow_OverridesEarlierSources()
  {
    AddSettings(("Other A", "Other B", false), ("Database A", "Database B", true));

    using ConfigurationRoot configuration = BuildConfiguration();

    Assert.Equal("Database A", configuration[KeyA]);
    Assert.Equal("Database B", configuration[KeyB]);
  }

  [Fact]
  public void NullColumn_FallsBackForThatSettingOnly()
  {
    AddSettings((null, "Database B", true));

    using ConfigurationRoot configuration = BuildConfiguration();

    Assert.Equal("File A", configuration[KeyA]);
    Assert.Equal("Database B", configuration[KeyB]);
  }

  [Fact]
  public void SavedChange_IsVisibleImmediately_AndSignalsReload()
  {
    using ConfigurationRoot configuration = BuildConfiguration();
    bool reloaded = false;
    using IDisposable subscription = ChangeToken.OnChange(configuration.GetReloadToken, () => reloaded = true);

    // No delay: the provider reloads synchronously after the save
    AddSettings(("New A", "New B", true));

    Assert.Equal("New A", configuration[KeyA]);
    Assert.Equal("New B", configuration[KeyB]);
    Assert.True(reloaded);
  }

  [Fact]
  public void ReloadOnChangeDisabled_KeepsStartupValues()
  {
    using ConfigurationRoot configuration = BuildConfiguration(reloadOnChange: false);

    AddSettings(("New A", "New B", true));

    Assert.Equal("File A", configuration[KeyA]);
  }

  [Fact]
  public async Task ConcurrentSaves_NeverExposeMissingValues()
  {
    // Concurrent reloads used to fail with "An item with the same key has already been added",
    // and readers could see an emptied dictionary (and so the appsettings.json value) in between
    AddSettings(("Value 0", "B", true));
    using ConfigurationRoot configuration = BuildConfiguration();
    CancellationToken cancellationToken = TestContext.Current.CancellationToken;

    using CancellationTokenSource stopReading = new();
    Task<List<string?>> reader = Task.Run(() =>
    {
      List<string?> unexpected = [];
      while (!stopReading.IsCancellationRequested)
      {
        string? value = configuration[KeyA];
        if (value is null || !value.StartsWith("Value ", StringComparison.Ordinal))
        {
          unexpected.Add(value);
        }
      }
      return unexpected;
    }, cancellationToken);

    // SQLite allows one writer at a time, so the saves are sequential;
    // extra change notifications from other threads make the reloads overlap
    Task writer = Task.Run(async () =>
    {
      for (int i = 1; i <= 50; i++)
      {
        await using DatabaseContext context = _database.CreateContext();
        AppSettingsCustomEntity settings = await context.AppSettingsCustomItems.SingleAsync(cancellationToken);
        settings.CustomSettingA = $"Value {i}";
        await context.SaveChangesAsync(cancellationToken);
      }
    }, cancellationToken);
    EntityChangeEventArgs change = new(new HashSet<Type> { typeof(AppSettingsCustomEntity) });
    Task[] reloaders = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
    {
      while (!writer.IsCompleted)
      {
        EntityChangeObserver.Instance.OnChanged(change);
      }
    }, cancellationToken)).ToArray();
    await Task.WhenAll([writer, .. reloaders]);
    await stopReading.CancelAsync();

    Assert.Empty(await reader);
    await using DatabaseContext check = _database.CreateContext();
    Assert.Equal((await check.AppSettingsCustomItems.SingleAsync(cancellationToken)).CustomSettingA, configuration[KeyA]);
  }

  [Fact]
  public void FailedReload_KeepsLastValues_AndReportsTheError()
  {
    AddSettings(("Database A", "Database B", true));
    List<Exception> errors = [];
    using ConfigurationRoot configuration = BuildConfiguration(onReloadException: errors.Add);

    using (DatabaseContext context = _database.CreateContext())
    {
      context.Database.ExecuteSqlRaw("DROP TABLE AppCustomSettings");
    }
    EntityChangeObserver.Instance.OnChanged(new EntityChangeEventArgs(new HashSet<Type> { typeof(AppSettingsCustomEntity) }));

    Assert.NotEmpty(errors);
    Assert.Equal("Database A", configuration[KeyA]);
  }

  private ConfigurationRoot BuildConfiguration(bool reloadOnChange = true, Action<Exception>? onReloadException = null) =>
    (ConfigurationRoot)new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?> { [KeyA] = "File A", [KeyB] = "File B" })
      .AddAppSettingsCustomEntityConfiguration(options => options.UseSqlite(_database.ConnectionString), reloadOnChange, onReloadException)
      .Build();

  private void AddSettings(params (string? A, string? B, bool Default)[] rows)
  {
    using DatabaseContext context = _database.CreateContext();
    context.AppSettingsCustomItems.AddRange(rows.Select(row => new AppSettingsCustomEntity
    {
      CustomSettingA = row.A,
      CustomSettingB = row.B,
      Default = row.Default
    }));
    context.SaveChanges();
  }
}

# Custom ConfigurationProvider Demo

Working with .NET is a pure pleasure. The framework architecture is modular, pluggable, and highly extensible. Configuration is a good example of this.
It lets you aggregate many configuration values from multiple different sources, and then access those in a strongly typed fashion using the **IOptions<>** pattern.

I want to use a configuration section from the **appsettings.json** which can be set from the UI as well. Configuration section values are stored in the database and exposed through an **Entity Framework Core DbContext**.

## Getting started
`WebApplication.CreateBuilder` sets up the default configuration for the application: **appsettings.json**, **appsettings.{Environment}.json**, environment variables and command-line arguments. We add our own configuration source after them, so its values win:

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string connectionString = DatabaseSetup.ResolveSqliteConnectionString(
  builder.Configuration.GetConnectionString("DemoDatabase"),
  builder.Environment.ContentRootPath);

// The configuration provider reads from the database, so the database has to be ready first
await DatabaseSetup.MigrateAsync(connectionString);

// Added after appsettings.json, environment variables and command-line arguments,
// so the default configuration from the database overrides their AppSettingsCustom values
WebApplication? app = null;
builder.Configuration.AddAppSettingsCustomEntityConfiguration(
  options => options.UseSqlite(connectionString),
  reloadOnChange: true,
  onReloadException: exception => app?.Logger.LogError(exception, "Reloading AppSettingsCustom from the database failed, keeping the previous values"));

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection(AppSettings.SectionName));
builder.Services.Configure<AppSettingsCustom>(builder.Configuration.GetSection(AppSettingsCustom.SectionName));
```

We will use two configuration classes: **AppSettings** and **AppSettingsCustom**.
Both map to a configuration section in **appsettings.json**. But only the **AppSettingsCustom** values are overridden from the database (**AppSettingsCustomEntity**).

```csharp
public sealed class AppSettingsCustom
{
  public const string SectionName = nameof(AppSettingsCustom);

  public string? CustomSettingA { get; set; }

  public string? CustomSettingB { get; set; }
}
```

```csharp
[Table("AppCustomSettings")]
public class AppSettingsCustomEntity
{
  [Key]
  [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  public int Id { get; set; }

  [Description("CustomSetting A")]
  [Column("CustomSettingA", TypeName = "nvarchar(512)")]
  public string? CustomSettingA { get; set; }

  [Description("CustomSetting B")]
  [Column("CustomSettingB", TypeName = "nvarchar(512)")]
  public string? CustomSettingB { get; set; }

  [Column("Default", TypeName = "bit")]
  public bool Default { get; set; } = true;
}
```

The configuration file **appsettings.json** looks like this:

```json
{
  "ConnectionStrings": {
    "DemoDatabase": "Data Source=./Database/DemoDatabase.db"
  },
  "AppSettings": {
    "SettingA": "This is Setting A",
    "SettingB": "And this is Setting B"
  },
  "AppSettingsCustom": {
    "CustomSettingA": "Custom Setting A",
    "CustomSettingB": "Custom Setting B"
  }
}
```

## Creating a custom configuration provider

To create a custom configuration provider, we implement the **IConfigurationSource** and **IConfigurationProvider** interfaces from the **Microsoft.Extensions.Configuration.Abstractions** package (the provider derives from the **ConfigurationProvider** base class).

The **Load** method loads the data of the provider. It reads the default **AppSettingsCustomEntity** from the database and puts its values into the provider's dictionary.
By using the same keys as **appsettings.json** (`AppSettingsCustom:CustomSettingA`), the values from the database override the ones from the file. A value that is not in the database is simply not set, so the value from **appsettings.json** shows through.

```csharp
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

  // Errors are not caught: the application cannot run without its database
  public override void Load() => Data = ReadSettings();

  public void Dispose() => EntityChangeObserver.Instance.Changed -= OnEntitiesChanged;

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
```
We now have a configuration provider that loads its values from the database. But it does this only once, when the application starts, and we want it to reload when the user changes an **AppSettingsCustomEntity** in the database.

## Configuration source
The source creates the provider and holds its options: how to configure the **DbContext**, whether to reload on changes, and what to do when a reload fails.

```csharp
public sealed class AppSettingsCustomEntityConfigurationSource : IConfigurationSource
{
  public required Action<DbContextOptionsBuilder> OptionsAction { get; init; }

  public bool ReloadOnChange { get; init; } = true;

  public Action<Exception>? OnReloadException { get; init; }

  public IConfigurationProvider Build(IConfigurationBuilder builder) => new AppSettingsCustomEntityConfigurationProvider(this);
}
```
The `AddAppSettingsCustomEntityConfiguration` extension method used in **Program.cs** adds this source to the configuration.

## Reloading configuration on entity changes

To reload the configuration when an **AppSettingsCustomEntity** changes, the configuration provider has to learn about the change. The **DbContext** raises an event on an entity change observer, and the provider listens to it.
The observer is a singleton because the configuration is built before dependency injection exists.

```csharp
public sealed class EntityChangeObserver
{
  private EntityChangeObserver()
  {
  }

  public static EntityChangeObserver Instance { get; } = new();

  public event EventHandler<EntityChangeEventArgs>? Changed;

  // Raised synchronously after the changes are saved, so the next read of the configuration already sees them
  public void OnChanged(EntityChangeEventArgs e) => Changed?.Invoke(this, e);
}
```

## Notify the EntityChangeObserver

The **DbContext** overrides the two **SaveChanges** methods that all other overloads end up in. It notifies the observer once per save, and only after the save succeeded, so the provider always reads the saved data.

```csharp
public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
  public DbSet<AppSettingsCustomEntity> AppSettingsCustomItems => Set<AppSettingsCustomEntity>();

  public override int SaveChanges(bool acceptAllChangesOnSuccess)
  {
    HashSet<Type> changedEntityTypes = GetChangedEntityTypes();
    int result = base.SaveChanges(acceptAllChangesOnSuccess);
    NotifyChanged(changedEntityTypes);
    return result;
  }

  public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
  {
    HashSet<Type> changedEntityTypes = GetChangedEntityTypes();
    int result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    NotifyChanged(changedEntityTypes);
    return result;
  }

  private HashSet<Type> GetChangedEntityTypes() => ChangeTracker.Entries()
    .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
    .Select(entry => entry.Entity.GetType())
    .ToHashSet();

  private static void NotifyChanged(HashSet<Type> changedEntityTypes)
  {
    if (changedEntityTypes.Count > 0)
    {
      EntityChangeObserver.Instance.OnChanged(new EntityChangeEventArgs(changedEntityTypes));
    }
  }
}
```

## Trigger a reload

The provider reloads only when an **AppSettingsCustomEntity** was saved. It reads the new values into a new dictionary and then swaps it in, so concurrent readers never see a half-filled dictionary, and one reload runs at a time.
After the reload, **OnReload** tells the consumers of the configuration's change token (for example **IOptionsMonitor<>**) that the values changed. If the database cannot be read, the provider keeps the previous values and reports the error.

```csharp
private void OnEntitiesChanged(object? sender, EntityChangeEventArgs e)
{
  if (!e.EntityTypes.Contains(typeof(AppSettingsCustomEntity)))
  {
    return;
  }

  try
  {
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

  OnReload();
}
```

## How it works

At startup the application applies the EF Core migrations (creating **Database/DemoDatabase.db** if needed), loads **AppSettings** and **AppSettingsCustom** from **appsettings.json**, and then the **AppSettingsCustomEntityConfigurationSource** overrides the **AppSettingsCustom** values with the default configuration from the database (when there is one).
The user can add, edit and delete configurations and choose the default one; there is always exactly one default while any configuration exists. Every change reloads the values, which are then available to all consumers through **IOptionsSnapshot<AppSettingsCustom>**.

**Important:** use **IOptionsSnapshot<>** (or **IOptionsMonitor<>**), which supports reloading options. **IOptionsSnapshot<>** values are computed once per request and cached for the lifetime of the request.
[Read more](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/options#use-ioptionssnapshot-to-read-updated-data)

This example also demonstrates:
- [Razor Pages in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/tutorials/razor-pages/razor-pages-start?view=aspnetcore-10.0)
- Logging with [Serilog](https://serilog.net/) to the console and to a file (`Log/DemoLog*.txt`)
- Asynchronous repository pattern for entity types
- [SQLite EF Core database provider](https://learn.microsoft.com/ef/core/providers/sqlite/) with migrations
- A health check endpoint (`/health`) that also checks the database

## Running
```bash
dotnet run --project src/Custom.ConfigurationProvider.Demo
```
Then open http://localhost:5000. The database (`src/Custom.ConfigurationProvider.Demo/Database/DemoDatabase.db`) is not part of the repository: it is created at the first start.

## Testing
```bash
dotnet test --solution Custom.ConfigurationProvider.Demo.sln
```
The tests (xUnit v3) cover:
- the configuration provider on its own: falling back to **appsettings.json**, overriding from the database, reloading right after a save, overlapping reloads, and keeping the last values when a reload fails;
- the database setup: migrations that match the model, adopting a database created by earlier versions of this demo, and write-ahead logging;
- the Razor Pages end to end with `WebApplicationFactory`, each test on its own temporary SQLite database.

## Prerequisites
- [Visual Studio](https://visualstudio.microsoft.com/vs/) 2026 or higher (or any editor with the .NET SDK)
- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0)

## Tags & Technologies
- [.NET 10](https://github.com/dotnet/core/blob/main/release-notes/10.0)
- [ASP.NET Core Razor Pages](https://learn.microsoft.com/en-us/aspnet/core/razor-pages/?view=aspnetcore-10.0)
- [Entity Framework Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)

## Web User interface

For a Web Client we used **ASP.NET Core Razor** Pages which can make coding page-focused scenarios easier and more productive than using controllers and views:

![](res/DemoScreen1.jpg)

List of available configurations:

![](res/DemoScreen2.jpg)

Create configuration:

![](res/DemoScreen5.jpg)

Edit configuration:

![](res/DemoScreen3.jpg)

Delete configuration:

![](res/DemoScreen4.jpg)

Enjoy!

## Licence
Licenced under [MIT](http://opensource.org/licenses/mit-license.php).
Contact me on [LinkedIn](https://si.linkedin.com/in/matjazbravc).

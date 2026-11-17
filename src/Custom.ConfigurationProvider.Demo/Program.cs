using Custom.Configuration.Provider.Demo.Configuration;
using Custom.Configuration.Provider.Demo.Data;
using Custom.Configuration.Provider.Demo.Services.Repositories;
using Microsoft.EntityFrameworkCore;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Serilog replaces the default logging providers (sinks are configured in appsettings.json)
builder.Services.AddSerilog((services, logger) => logger
  .ReadFrom.Configuration(builder.Configuration)
  .ReadFrom.Services(services)
  .Enrich.FromLogContext());

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

builder.Services.AddDbContext<DatabaseContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<IAppSettingsCustomRepository, AppSettingsCustomRepository>();
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks()
  .AddDbContextCheck<DatabaseContext>();

app = builder.Build();

// In Development the developer exception page is used
if (!app.Environment.IsDevelopment())
{
  app.UseExceptionHandler("/Error");
  app.UseHsts();
}

app.MapStaticAssets();
app.MapRazorPages()
  .WithStaticAssets();
app.MapHealthChecks("/health");

await app.RunAsync();

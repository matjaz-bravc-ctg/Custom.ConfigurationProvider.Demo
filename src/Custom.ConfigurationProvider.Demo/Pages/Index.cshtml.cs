using Custom.Configuration.Provider.Demo.Configuration;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Custom.Configuration.Provider.Demo.Pages;

// IOptionsSnapshot<T> is computed once per request, so it shows values reloaded from the database.
// IOptions<T> is computed once for the lifetime of the application.
// Read more: https://learn.microsoft.com/aspnet/core/fundamentals/configuration/options#use-ioptionssnapshot-to-read-updated-data
public class IndexModel(IOptions<AppSettings> appSettings, IOptionsSnapshot<AppSettingsCustom> appSettingsCustom) : PageModel
{
  public AppSettings AppSettings { get; } = appSettings.Value;

  public AppSettingsCustom AppSettingsCustom { get; } = appSettingsCustom.Value;
}

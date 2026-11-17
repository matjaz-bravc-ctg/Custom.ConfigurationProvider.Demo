using Custom.Configuration.Provider.Demo.Data.Entities;
using Custom.Configuration.Provider.Demo.Services.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Custom.Configuration.Provider.Demo.Pages.CustomSettings;

public class IndexModel(IAppSettingsCustomRepository repository) : PageModel
{
  public IReadOnlyList<AppSettingsCustomEntity> AppSettingsCustomEntities { get; private set; } = [];

  public async Task OnGetAsync(CancellationToken cancellationToken) =>
    AppSettingsCustomEntities = await repository.GetAllAsync(cancellationToken);

  // Changing the default changes data, so it is a POST with antiforgery validation, not a link
  public async Task<IActionResult> OnPostSetDefaultAsync(int id, CancellationToken cancellationToken) =>
    await repository.SetDefaultAsync(id, cancellationToken) ? RedirectToPage() : NotFound();
}

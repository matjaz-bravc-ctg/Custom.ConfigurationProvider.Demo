using Custom.Configuration.Provider.Demo.Data.Entities;
using Custom.Configuration.Provider.Demo.Services.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Custom.Configuration.Provider.Demo.Pages.CustomSettings;

public class DeleteModel(IAppSettingsCustomRepository repository) : PageModel
{
  public AppSettingsCustomEntity AppSettingsCustom { get; private set; } = new();

  public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
  {
    AppSettingsCustomEntity? settings = await repository.GetByIdAsync(id, cancellationToken);
    if (settings is null)
    {
      return NotFound();
    }
    AppSettingsCustom = settings;
    return Page();
  }

  public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken) =>
    await repository.DeleteAsync(id, cancellationToken)
      ? RedirectToPage("./Index")
      : NotFound();
}

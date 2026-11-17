using Custom.Configuration.Provider.Demo.Data.Entities;
using Custom.Configuration.Provider.Demo.Services.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Custom.Configuration.Provider.Demo.Pages.CustomSettings;

public class CreateModel(IAppSettingsCustomRepository repository) : PageModel
{
  [BindProperty]
  public AppSettingsCustomEntity AppSettingsCustom { get; set; } = new();

  public void OnGet()
  {
  }

  public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
  {
    if (!ModelState.IsValid)
    {
      return Page();
    }
    await repository.AddAsync(AppSettingsCustom, cancellationToken);
    return RedirectToPage("./Index");
  }
}

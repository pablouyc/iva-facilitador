using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using IvaFacilitador.Models;
using IvaFacilitador.Services;

namespace IvaFacilitador.Pages.Onboarding
{
    public class SaveGeneralModel : PageModel
    {
        private readonly ICompanyProfileStore _profiles;
        public SaveGeneralModel(ICompanyProfileStore profiles){ _profiles = profiles; }

        [BindProperty]
        public string RealmId { get; set; } = "";
        [BindProperty]
        public bool UsesProrrata { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            var realm = string.IsNullOrWhiteSpace(RealmId) ? "global" : RealmId;
            var profile = await _profiles.LoadAsync(realm) ?? new CompanyProfile { RealmId = realm };
            profile.UsesProrrata = UsesProrrata;
            await _profiles.SaveAsync(profile);
            return RedirectToPage("Publish", new { realmId = realm });
        }
    }
}

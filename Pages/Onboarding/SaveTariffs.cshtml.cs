using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using IvaFacilitador.Models;
using IvaFacilitador.Services;

namespace IvaFacilitador.Pages.Onboarding
{
    public class SaveTariffsModel : PageModel
    {
        private readonly ICompanyProfileStore _profiles;
        public SaveTariffsModel(ICompanyProfileStore profiles) { _profiles = profiles; }

        [BindProperty]
        public string RealmId { get; set; } = "";
        [BindProperty]
        public string TariffsCsv { get; set; } = "";

        public async Task<IActionResult> OnPostAsync()
        {
            var realm = string.IsNullOrWhiteSpace(RealmId) ? "global" : RealmId;
            var profile = await _profiles.LoadAsync(realm) ?? new CompanyProfile { RealmId = realm };
            profile.SalesTariffs = TariffsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            profile.TariffsReviewedAt = DateTimeOffset.UtcNow;
            await _profiles.SaveAsync(profile);
            return RedirectToPage("General", new { realmId = realm });
        }
    }
}

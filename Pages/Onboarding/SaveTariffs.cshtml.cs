using System;
using System.Collections.Generic;
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
        public string[] DetectedTariffs { get; set; } = System.Array.Empty<string>();

        [BindProperty]
        public bool HasAdditionalTariffs { get; set; }

        [BindProperty]
        public List<string> AdditionalTariffs { get; set; } = new();

        public async Task<IActionResult> OnPostAsync()
        {
            var realm = string.IsNullOrWhiteSpace(RealmId) ? "global" : RealmId;
            var profile = await _profiles.LoadAsync(realm) ?? new CompanyProfile { RealmId = realm };
            var tariffs = DetectedTariffs?.ToList() ?? new List<string>();
            if (HasAdditionalTariffs && AdditionalTariffs != null)
            {
                tariffs.AddRange(AdditionalTariffs.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            profile.SalesTariffs = tariffs.Distinct().ToList();
            profile.TariffsReviewedAt = DateTimeOffset.UtcNow;
            await _profiles.SaveAsync(profile);
            return RedirectToPage("General", new { realmId = realm });
        }
    }
}

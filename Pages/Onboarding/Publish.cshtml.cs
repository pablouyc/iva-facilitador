using System;
using System.Threading.Tasks;
using IvaFacilitador.Models;
using IvaFacilitador.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IvaFacilitador.Pages.Onboarding
{
    public class PublishModel : PageModel
    {
        private readonly ICompanyProfileStore _profiles;
        public PublishModel(ICompanyProfileStore profiles) { _profiles = profiles; }

        public async Task<IActionResult> OnPostAsync(string RealmId)
        {
            var realm = string.IsNullOrWhiteSpace(RealmId) ? "global" : RealmId;
            var profile = await _profiles.LoadAsync(realm) ?? new CompanyProfile { RealmId = realm };
            profile.PublishedAt = DateTimeOffset.UtcNow;
            await _profiles.SaveAsync(profile);
            return Redirect("/Auth/Connect?published=1");
        }
    }
}

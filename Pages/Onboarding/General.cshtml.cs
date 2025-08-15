using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using IvaFacilitador.Services;

namespace IvaFacilitador.Pages.Onboarding
{
    public class GeneralModel : PageModel
    {
        private readonly ICompanyProfileStore _profiles;
        public GeneralModel(ICompanyProfileStore profiles){ _profiles = profiles; }

        [BindProperty(SupportsGet = true)]
        public string RealmId { get; set; } = "";

        [BindProperty]
        public bool UsesProrrata { get; set; }

        public async Task OnGetAsync()
        {
            if (!string.IsNullOrWhiteSpace(RealmId))
            {
                var profile = await _profiles.LoadAsync(RealmId);
                if (profile != null)
                {
                    UsesProrrata = profile.UsesProrrata;
                }
            }
        }
    }
}

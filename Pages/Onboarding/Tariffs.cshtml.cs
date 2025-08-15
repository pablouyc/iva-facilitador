using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using IvaFacilitador.Services;

namespace IvaFacilitador.Pages.Onboarding
{
    public class TariffsModel : PageModel
    {
        private readonly ICompanyProfileStore _profiles;
        private readonly IQuickBooksTariffDetector _detector;

        public TariffsModel(ICompanyProfileStore profiles, IQuickBooksTariffDetector detector)
        {
            _profiles = profiles;
            _detector = detector;
        }

        [BindProperty(SupportsGet = true)]
        public string RealmId { get; set; } = "";

        [BindProperty]
        public string TariffsCsv { get; set; } = "";

        public async Task OnGetAsync()
        {
            if (!string.IsNullOrWhiteSpace(RealmId))
            {
                var profile = await _profiles.LoadAsync(RealmId);
                if (profile != null && profile.SalesTariffs.Count > 0)
                {
                    TariffsCsv = string.Join(",", profile.SalesTariffs);
                }
                else
                {
                    var detected = await _detector.DetectTariffsAsync(RealmId, "");
                    TariffsCsv = string.Join(",", detected);
                }
            }
        }
    }
}

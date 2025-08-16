using System.Collections.Generic;
using System.Linq;
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

        public List<string> DetectedTariffs { get; private set; } = new();

        public string DetectedTariffsDisplay => DetectedTariffs.Count == 0 ? "Sin datos (últimos 6 meses)" : string.Join(", ", DetectedTariffs);

        public async Task OnGetAsync()
        {
            if (!string.IsNullOrWhiteSpace(RealmId))
            {
                var profile = await _profiles.LoadAsync(RealmId);
                if (profile != null && profile.SalesTariffs.Count > 0)
                {
                    DetectedTariffs = profile.SalesTariffs;
                }
                else
                {
                    DetectedTariffs = (await _detector.DetectTariffsAsync(RealmId, "")).ToList();
                }
            }
        }
    }
}

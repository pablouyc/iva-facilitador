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

        [BindProperty] public bool HasCardTerminals { get; set; }
        [BindProperty] public string? CardRetencionRentaAccount { get; set; }
        [BindProperty] public string? CardRetencionIvaAccount { get; set; }
        [BindProperty] public string? CardComisionesAccount { get; set; }
        [BindProperty] public bool IsMEICRegistered { get; set; }
        [BindProperty] public bool IsMAGRegistered { get; set; }
        [BindProperty] public bool? HasProrata { get; set; }
        [BindProperty] public decimal? ProrataPercent { get; set; }
        [BindProperty] public string? ProrataCalcMode { get; set; }
        [BindProperty] public string IvaControlAccount { get; set; } = "";
        [BindProperty] public string IvaPorPagarAccount { get; set; } = "";
        [BindProperty] public string IvaAFavorAccount { get; set; } = "";
        [BindProperty] public bool DoesExports { get; set; }
        [BindProperty] public bool HasCapitalRentals { get; set; }
        [BindProperty] public string? NonDeductibleExpensesAccount { get; set; }
        [BindProperty] public bool InExemptionZone { get; set; }
        [BindProperty] public string? ExemptionNotes { get; set; }

        public async Task OnGetAsync()
        {
            if (!string.IsNullOrWhiteSpace(RealmId))
            {
                var profile = await _profiles.LoadAsync(RealmId);
                if (profile != null)
                {
                    HasCardTerminals = profile.HasCardTerminals;
                    CardRetencionRentaAccount = profile.CardRetencionRentaAccount;
                    CardRetencionIvaAccount = profile.CardRetencionIvaAccount;
                    CardComisionesAccount = profile.CardComisionesAccount;
                    IsMEICRegistered = profile.IsMEICRegistered;
                    IsMAGRegistered = profile.IsMAGRegistered;
                    HasProrata = profile.HasProrata;
                    ProrataPercent = profile.ProrataPercent;
                    ProrataCalcMode = profile.ProrataCalcMode;
                    IvaControlAccount = profile.IvaControlAccount;
                    IvaPorPagarAccount = profile.IvaPorPagarAccount;
                    IvaAFavorAccount = profile.IvaAFavorAccount;
                    DoesExports = profile.DoesExports;
                    HasCapitalRentals = profile.HasCapitalRentals;
                    NonDeductibleExpensesAccount = profile.NonDeductibleExpensesAccount;
                    InExemptionZone = profile.InExemptionZone;
                    ExemptionNotes = profile.ExemptionNotes;
                }
            }
        }
    }
}

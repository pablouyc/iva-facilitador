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

        [BindProperty] public string RealmId { get; set; } = "";
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

        public async Task<IActionResult> OnPostAsync()
        {
            var realm = string.IsNullOrWhiteSpace(RealmId) ? "global" : RealmId;
            var profile = await _profiles.LoadAsync(realm) ?? new CompanyProfile { RealmId = realm };

            profile.HasCardTerminals = HasCardTerminals;
            profile.CardRetencionRentaAccount = CardRetencionRentaAccount;
            profile.CardRetencionIvaAccount = CardRetencionIvaAccount;
            profile.CardComisionesAccount = CardComisionesAccount;
            profile.IsMEICRegistered = IsMEICRegistered;
            profile.IsMAGRegistered = IsMAGRegistered;
            profile.HasProrata = HasProrata;
            profile.ProrataPercent = ProrataPercent;
            profile.ProrataCalcMode = ProrataCalcMode;
            profile.IvaControlAccount = IvaControlAccount;
            profile.IvaPorPagarAccount = IvaPorPagarAccount;
            profile.IvaAFavorAccount = IvaAFavorAccount;
            profile.DoesExports = DoesExports;
            profile.HasCapitalRentals = HasCapitalRentals;
            profile.NonDeductibleExpensesAccount = NonDeductibleExpensesAccount;
            profile.InExemptionZone = InExemptionZone;
            profile.ExemptionNotes = ExemptionNotes;

            await _profiles.SaveAsync(profile);
            return RedirectToPage("Publish", new { realmId = realm });
        }
    }
}

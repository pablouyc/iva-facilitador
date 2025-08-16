using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using IvaFacilitador.Services;
using IvaFacilitador.Models;

namespace IvaFacilitador.Pages.Empresas
{
    public class IndexModel : PageModel
    {
        private readonly ICompanyStore _companyStore;
        private readonly ITokenStore _tokenStore;
        private readonly IQuickBooksAuth _auth;

        public IndexModel(ICompanyStore companyStore, ITokenStore tokenStore, IQuickBooksAuth auth)
        {
            _companyStore = companyStore;
            _tokenStore = tokenStore;
            _auth = auth;
        }

        public List<CompanyConnection> Companies { get; private set; } = new();

        public void OnGet()
        {
            Companies = _companyStore.GetCompaniesForUser().OrderBy(c => c.Name).ToList();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDisconnect(string realmId)
        {
            if (string.IsNullOrWhiteSpace(realmId))
            {
                TempData["Error"] = "RealmId inválido.";
                return RedirectToPage();
            }

            try
            {
                // 1) Revocar refresh_token en Intuit (si existe)
                var token = _tokenStore.Get(realmId);
                if (token != null && !string.IsNullOrEmpty(token.refresh_token))
                {
                    var (ok, error) = await _auth.TryRevokeRefreshTokenAsync(token.refresh_token);
                    if (!ok && !string.IsNullOrWhiteSpace(error))
                    {
                        TempData["Error"] = $"No se pudo revocar en Intuit: {error}. Se eliminará localmente.";
                    }
                }

                // 2) Eliminar localmente
                _tokenStore.Delete(realmId);
                _companyStore.RemoveCompany(realmId);

                TempData["Success"] = $"La empresa ({realmId}) fue desconectada.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al desconectar: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}

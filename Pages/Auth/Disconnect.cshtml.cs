using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using IvaFacilitador.Services;

namespace IvaFacilitador.Pages.Auth
{
    public class DisconnectModel : PageModel
    {
        private readonly ICompanyStore _companyStore;
        private readonly ITokenStore _tokenStore;
        private readonly IQuickBooksAuth _auth;

        public DisconnectModel(ICompanyStore companyStore, ITokenStore tokenStore, IQuickBooksAuth auth)
        {
            _companyStore = companyStore;
            _tokenStore = tokenStore;
            _auth = auth;
        }

        // /Auth/Disconnect?realmId=XXXX
        public async Task<IActionResult> OnGet(string? realmId)
        {
            if (string.IsNullOrWhiteSpace(realmId))
            {
                TempData["Error"] = "RealmId inválido.";
                return RedirectToPage("/Index");
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

            return RedirectToPage("/Index");
        }
    }
}
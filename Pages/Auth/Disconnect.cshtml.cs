using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using IvaFacilitador.Services;
using System.Linq;

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

        // /Auth/Disconnect?realmId=XXXX[&reason=guard]
        public async Task<IActionResult> OnGet(string? realmId, string? reason)
        {
            if (string.IsNullOrWhiteSpace(realmId))
            {
                TempData["Error"] = "RealmId inválido.";
                // Asegurar limpieza de cookie si existiera
                Response.Cookies.Delete("must_param_realm");
                return RedirectToPage("/Index");
            }

            try
            {
                // Obtener el nombre para el mensaje antes de borrar
                var companyName = _companyStore.GetCompaniesForUser()
                    .FirstOrDefault(c => c.RealmId == realmId)?.Name ?? realmId;

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

                // 3) Borrar cookie del guard
                Response.Cookies.Delete("must_param_realm");

                // 4) Mensaje contextual si el guard forzó la desconexión
                if (!string.IsNullOrWhiteSpace(reason) && reason.Equals("guard", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["AutoDisconnected"] = $"Al salirse sin parametrizar, la empresa {companyName} fue desconectada.";
                }
                else
                {
                    TempData["Success"] = $"La empresa ({companyName}) fue desconectada.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al desconectar: {ex.Message}";
                // Asegurar limpieza de cookie en caso de error también
                Response.Cookies.Delete("must_param_realm");
            }

            return RedirectToPage("/Index");
        }
    }
}
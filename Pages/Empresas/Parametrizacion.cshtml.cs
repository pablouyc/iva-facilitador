using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using IvaFacilitador.Models;
using IvaFacilitador.Services;

namespace IvaFacilitador.Pages.Empresas
{
    public class ParametrizacionModel : PageModel
    {
        private readonly ICompanyStore _companyStore;
        private readonly ITokenStore _tokenStore;
        private readonly IQuickBooksAuth _auth;

        public ParametrizacionModel(ICompanyStore companyStore, ITokenStore tokenStore, IQuickBooksAuth auth)
        {
            _companyStore = companyStore;
            _tokenStore = tokenStore;
            _auth = auth;
        }

        public CompanyConnection? PendingCompany { get; set; }

        public IActionResult OnGet()
        {
            PendingCompany = LoadPending();
            if (PendingCompany == null)
            {
                TempData["Error"] = "No hay empresa pendiente de parametrizar.";
                return RedirectToPage("/Empresas/Index");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostConfirm()
        {
            var pending = LoadPending();
            if (pending == null)
            {
                TempData["Error"] = "No hay empresa pendiente de parametrizar.";
                return RedirectToPage("/Empresas/Index");
            }

            // Persistir definitivamente
            _companyStore.AddOrUpdateCompany(new CompanyConnection
            {
                RealmId = pending.RealmId,
                Name = pending.Name
            });

            // Limpiar bandera de pendiente
            HttpContext.Session.Remove("PendingCompany");

            TempData["Success"] = $"Empresa '{pending.Name}' confirmada.";
            return RedirectToPage("/Empresas/Index");
        }

        public async Task<IActionResult> OnPostCancel()
        {
            var pending = LoadPending();
            if (pending == null)
            {
                TempData["Error"] = "No hay empresa pendiente para cancelar.";
                return RedirectToPage("/Empresas/Index");
            }

            // 1) Revocar refresh_token en Intuit (si existe)
            var token = _tokenStore.Get(pending.RealmId);
            if (token != null && !string.IsNullOrWhiteSpace(token.refresh_token))
            {
                var (ok, error) = await _auth.TryRevokeRefreshTokenAsync(token.refresh_token);
                if (!ok)
                {
                    TempData["Error"] = $"No se pudo revocar en Intuit: {error}. Se eliminará localmente.";
                }
            }

            // 2) Borrar localmente
            _tokenStore.Delete(pending.RealmId);
            _companyStore.RemoveCompany(pending.RealmId);

            // 3) Limpiar sesión
            HttpContext.Session.Remove("PendingCompany");

            TempData["Success"] = $"Se canceló la conexión de la empresa {pending.RealmId}.";
            return RedirectToPage("/Empresas/Index");
        }

        private CompanyConnection? LoadPending()
        {
            var raw = HttpContext.Session.GetString("PendingCompany");
            if (string.IsNullOrWhiteSpace(raw)) return null;
            try
            {
                return JsonSerializer.Deserialize<CompanyConnection>(raw);
            }
            catch
            {
                return null;
            }
        }
    }
}

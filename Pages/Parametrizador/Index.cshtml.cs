using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using IvaFacilitador.Services;
using IvaFacilitador.Models;

namespace IvaFacilitador.Pages.Parametrizador
{
    public class IndexModel : PageModel
    {
        private readonly ICompanyProfileStore _profiles;
        private readonly ICompanyStore _companies; // ya existe en tu app

        public IndexModel(ICompanyProfileStore profiles, ICompanyStore companies)
        {
            _profiles = profiles;
            _companies = companies;
        }

        [BindProperty(SupportsGet = true)]
        public string? RealmId { get; set; }

        public string? CompanyName { get; set; }

        [BindProperty]
        public CompanyProfile Input { get; set; } = new CompanyProfile();

        public IActionResult OnGet()
        {
            // Resolver RealmId: querystring o cookie del guard
            if (string.IsNullOrWhiteSpace(RealmId))
            {
                if (Request.Cookies.TryGetValue("must_param_realm", out var cookieRealm) &&
                    !string.IsNullOrWhiteSpace(cookieRealm))
                {
                    RealmId = cookieRealm;
                }
            }

            if (string.IsNullOrWhiteSpace(RealmId))
            {
                TempData["Error"] = "No se identificó la empresa a parametrizar.";
                return RedirectToPage("/Index");
            }

            // Nombre visible (si no se encuentra, mostramos el RealmId)
            CompanyName = _companies.GetCompaniesForUser()
                .FirstOrDefault(c => c.RealmId == RealmId)?.Name ?? RealmId;

            // Cargar perfil existente o preparar uno nuevo
            var existing = _profiles.Get(RealmId);
            if (existing != null)
            {
                Input = existing;
            }
            else
            {
                Input.RealmId = RealmId!;
            }

            return Page();
        }

        public IActionResult OnPostSave()
        {
            TempData.Remove("AutoDisconnected");
            if (string.IsNullOrWhiteSpace(RealmId))
            {
                TempData["Error"] = "Falta RealmId para guardar la parametrización.";
                return RedirectToPage("/Index");
            }

            Input.RealmId = RealmId!;
            _profiles.Upsert(Input);

            TempData["Success"] = "Parametrización guardada.";
            return Redirect("/IVA/Seleccion");
        }

        public IActionResult OnPostCancel()
        {
            // Si no sabemos la empresa, simplemente volvemos
            if (string.IsNullOrWhiteSpace(RealmId))
                return Redirect("/IVA/Seleccion");

            // Intentar obtener nombre legible
            string companyName = _companies.GetCompaniesForUser()
                .FirstOrDefault(c => c.RealmId == RealmId)?.Name ?? RealmId!;

            // Intentar desconectar por reflexión (si existe ICompanyStore.Disconnect(string))
            try
            {
                var mi = _companies.GetType().GetMethod("Disconnect", new Type[] { typeof(string) });
                if (mi != null)
                {
                    mi.Invoke(_companies, new object?[] { RealmId });
                }
            }
            catch
            {
                // noop: si no existe o falla, no detenemos el flujo
            }

            // Limpiar cookie del guard
            try { Response.Cookies.Delete("must_param_realm"); } catch {}

            TempData["AutoDisconnected"] =
                $"Al salirse sin parametrizar, la empresa {companyName} fue desconectada.";

            return Redirect("/IVA/Seleccion");
        }
    }
}
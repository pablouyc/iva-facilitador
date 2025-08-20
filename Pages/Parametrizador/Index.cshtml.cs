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
        private readonly ICompanyStore _companies;

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
            // Resolver RealmId desde querystring o cookie
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

            CompanyName = _companies.GetCompaniesForUser()
                .FirstOrDefault(c => c.RealmId == RealmId)?.Name ?? RealmId;

            var existing = _profiles.Get(RealmId);
            if (existing != null)
            {
                Input = existing;
            }
            else
            {
                Input.RealmId = RealmId;
            }

            return Page();
        }

        public IActionResult OnPostSave()
        {
            if (string.IsNullOrWhiteSpace(RealmId))
            {
                TempData["Error"] = "Falta RealmId para guardar la parametrización.";
                return RedirectToPage("/Index");
            }

            // Persistir perfil
            Input.RealmId = RealmId;
            _profiles.Upsert(Input);

            // Guardando: NO queremos mostrar el toast de salida
            try { TempData.Remove("AutoDisconnected"); } catch { }

            TempData["Success"] = "Parametrización guardada.";
            return Redirect("/IVA/Seleccion");
        }

        public IActionResult OnPostCancel()
        {
            if (string.IsNullOrWhiteSpace(RealmId))
            {
                return Redirect("/IVA/Seleccion");
            }

            string companyName = _companies.GetCompaniesForUser()
                .FirstOrDefault(c => c.RealmId == RealmId)?.Name ?? RealmId;

            // Intentar desconectar (si el store lo expone)
            try
            {
                var method = _companies.GetType().GetMethod("Disconnect");
                if (method != null && method.GetParameters().Length == 1)
                {
                    method.Invoke(_companies, new object?[] { RealmId });
                }
            }
            catch { /* noop */ }

            try { Response.Cookies.Delete("must_param_realm"); } catch { }

            // Cancelando: NO mostrar "guardada", SÍ mostrar toast de salida
            try { TempData.Remove("Success"); } catch { }
            TempData["AutoDisconnected"] =
                $"Al salirse sin parametrizar, la empresa {companyName} fue desconectada.";

            return Redirect("/IVA/Seleccion");
        }
    }
}
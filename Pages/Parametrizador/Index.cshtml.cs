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

        // DTO mínimo por ahora; lo ampliaremos con los campos de cada sección
        [BindProperty]
        public CompanyProfile Input { get; set; } = new CompanyProfile();

        public IActionResult OnGet()
        {
            // Resolver realmId: prioridad querystring, luego cookie del guard
            if (string.IsNullOrWhiteSpace(RealmId))
            {
                if (Request.Cookies.TryGetValue("must_param_realm", out var cookieRealm) && !string.IsNullOrWhiteSpace(cookieRealm))
                    RealmId = cookieRealm;
            }

            if (string.IsNullOrWhiteSpace(RealmId))
            {
                TempData["Error"] = "No se identificó la empresa a parametrizar.";
                return RedirectToPage("/Index");
            }

            // Nombre visible
            CompanyName = _companies.GetCompaniesForUser()
                .FirstOrDefault(c => c.RealmId == RealmId)?.Name ?? RealmId;

            // Cargar perfil si existe
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

            // Asegurar RealmId en el objeto de entrada
            Input.RealmId = RealmId;

            // Validación mínima (se ampliará por sección)
            // Por ahora, guardamos tal cual.
            _profiles.Upsert(Input);

            TempData["Success"] = "Parametrización guardada.";
            return Redirect("/IVA/Seleccion");
        }
    }

        public IActionResult OnPostCancel()
        {
            if (string.IsNullOrWhiteSpace(RealmId))
            {
                return Redirect("/IVA/Seleccion");
            }

            string companyName = _companies.GetCompaniesForUser()
                .FirstOrDefault(c => c.RealmId == RealmId)?.Name ?? RealmId;

            try
            {
                var method = _companies.GetType().GetMethod("Disconnect");
                if (method != null && method.GetParameters().Length == 1)
                {
                    method.Invoke(_companies, new object?[] { RealmId });
                }
            }
            catch { /* noop */ }

            try { Response.Cookies.Delete("must_param_realm"); } catch {}

            TempData["AutoDisconnected"] = $"Al salirse sin parametrizar, la empresa {companyName} fue desconectada.";
            return Redirect("/IVA/Seleccion");
        }
}
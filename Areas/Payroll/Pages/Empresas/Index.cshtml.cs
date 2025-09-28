using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using IvaFacilitador.Areas.Payroll.ModelosPayroll;

namespace IvaFacilitador.Areas.Payroll.Pages.Empresas
{
    public class IndexModel : PageModel
    {
        private readonly PayrollDbContext _db;

        public IndexModel(PayrollDbContext db)
        {
            _db = db;
        }

        [BindProperty] public string? NewName  { get; set; }
        [BindProperty] public string? NewTaxId { get; set; }

        public List<Company> Companies { get; set; } = new();

        public void OnGet()
        {
            Companies = _db.Companies
                           .OrderBy(c => c.Name)
                           .ToList();
        }

        public IActionResult OnPostAddAndConnect()
        {
            var name  = (NewName  ?? string.Empty).Trim();
            var taxId = (NewTaxId ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError(nameof(NewName), "El nombre es obligatorio.");
                OnGet();
                return Page();
            }

            var c = new Company { Name = name, TaxId = taxId };
            _db.Companies.Add(c);
            _db.SaveChanges();

            var returnTo = $"/Payroll/Empresas/Config/{c.Id}";
            var url = $"/Auth/ConnectQboPayroll?companyId={c.Id}&returnTo={Uri.EscapeDataString(returnTo)}";
            return Redirect(url);
        }
    }
}

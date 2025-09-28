using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using IvaFacilitador.Areas.Payroll.ModelosPayroll;

namespace IvaFacilitador.Areas.Payroll.Pages.Empresas
{
    public class IndexModel : PageModel
    {
        private readonly PayrollDbContext _db;
        public IndexModel(PayrollDbContext db) { _db = db; }

        public List<Company> Companies { get; set; } = new();

        [BindProperty]
        public InputCompany CompanyInput { get; set; } = new();

        public class InputCompany
        {
            public string Name { get; set; } = string.Empty;
            public string? TaxId { get; set; }
        }

        public async Task OnGetAsync()
        {
            Companies = await _db.Companies
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(CompanyInput.Name))
            {
                await OnGetAsync();
                return Page();
            }

            var company = new Company
            {
                Name  = CompanyInput.Name.Trim(),
                TaxId = string.IsNullOrWhiteSpace(CompanyInput.TaxId) ? null : CompanyInput.TaxId!.Trim()
            };

            _db.Companies.Add(company);
            await _db.SaveChangesAsync();

            // tras crear, vamos directo al flujo de conexión
            var returnTo = $"/Payroll/Empresas/Config/{company.Id}";
            var url = $"/Auth/ConnectQboPayroll?companyId={company.Id}&returnTo={System.Net.WebUtility.UrlEncode(returnTo)}";
            return Redirect(url);
        }
    }
}

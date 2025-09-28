using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using IvaFacilitador.Areas.Payroll.ModelosPayroll;

namespace IvaFacilitador.Areas.Payroll.Pages.Empresas
{
    public class ConfigModel : PageModel
    {
        private readonly PayrollDbContext _db;

        [BindProperty]
        public Company Company { get; set; } = new Company();

        [BindProperty(SupportsGet = true)]
        public int? Id { get; set; }

        public bool IsNew => !Id.HasValue || Id.Value == 0;

        public ConfigModel(PayrollDbContext db) { _db = db; }

        public async Task<IActionResult> OnGet()
        {
            if (Id.HasValue)
            {
                var found = await _db.Companies.FirstOrDefaultAsync(c => c.Id == Id.Value);
                if (found == null) return NotFound();
                Company = found;
            }
            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            if (!ModelState.IsValid) return Page();

            if (Company.Id == 0)
                _db.Companies.Add(Company);
            else
                _db.Companies.Update(Company);

            await _db.SaveChangesAsync();
            return RedirectToPage("/Empresas/Index");
        }
    }
}

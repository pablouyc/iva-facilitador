using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;

namespace IvaFacilitador.Areas.Payroll.Pages.Empresas
{
    public class ConfigModel : PageModel
    {
        private readonly PayrollDbContext _db;
        public ConfigModel(PayrollDbContext db) => _db = db;

        public Company? Company { get; set; }

        public async Task OnGet(int id)
        {
            Company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == id);
        }
    }
}

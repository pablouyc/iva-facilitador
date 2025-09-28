using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using IvaFacilitador.Areas.Payroll.ModelosPayroll;

namespace IvaFacilitador.Areas.Payroll.Pages.Empresas
{
    public class IndexModel : PageModel
    {
        private readonly PayrollDbContext _db;
        public List<Company> Empresas { get; set; } = new();

        public IndexModel(PayrollDbContext db) { _db = db; }

        public async Task OnGet()
        {
            Empresas = await _db.Companies
                .OrderBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}

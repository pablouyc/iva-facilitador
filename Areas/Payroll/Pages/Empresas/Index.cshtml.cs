using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Payroll.Services;

namespace IvaFacilitador.Areas.Payroll.Pages.Empresas
{
    public class IndexModel : PageModel
    {
        private readonly IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext _db;
        private readonly IPayrollAuthService _auth;

        public IndexModel(
            IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext db,
            IPayrollAuthService auth)
        {
            _db = db;
            _auth = auth;
        }

        public class Row
        {
            public int Id { get; set; }
            public string Nombre { get; set; } = "";
            public string? QboId { get; set; }
        }

        public List<Row> Empresas { get; set; } = new();

        public async Task OnGet()
        {
            Empresas = await _db.Companies
                .Select(c => new Row
                {
                    Id = c.Id,
                    Nombre = c.Name,  // Ajusta si tu columna se llama distinto
                    QboId = c.QboId
                })
                .OrderBy(r => r.Nombre)
                .ToListAsync();
        }

        // Handler del botón "Agregar": redirige a Intuit con "returnTo"
        public IActionResult OnGetAgregar(int companyId = 0)
        {
            // Volveremos aquí después del callback:
            var returnTo = Url.Page("/Empresas/Index", new { area = "Payroll" }) ?? "/Payroll/Empresas";

            var url = _auth.GetAuthorizeUrl(companyId, returnTo);
            return Redirect(url);
        }
    }
}


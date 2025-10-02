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

        public IndexModel(IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext db, IPayrollAuthService auth)
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
                .Select(c => new Row { Id = c.Id, Nombre = c.Name, QboId = c.QboId })
                .OrderBy(r => r.Nombre)
                .ToListAsync();
        }

        // NUEVO: al hacer click en "Agregar", te lleva a Intuit (Payroll) usando IntuitPayrollAuth__*
        public IActionResult OnGetAgregar(string? returnTo)
        {
            var rt = string.IsNullOrWhiteSpace(returnTo)
                ? Url.Page("/Empresas/Index", new { area = "Payroll" }) ?? "/Payroll/Empresas"
                : returnTo;
            var url = _auth.GetAuthorizeUrl(rt);
            return Redirect(url);
        }
    }
}

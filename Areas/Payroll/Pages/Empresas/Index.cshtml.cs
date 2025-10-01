using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace IvaFacilitador.Areas.Payroll.Pages.Empresas
{
    public class IndexModel : PageModel
    {
        private readonly IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext _db;

        public IndexModel(IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext db)
        {
            _db = db;
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
            // Solo lectura para UI: Id, Name, QboId (ajusta "Name" si tu entidad usa otro nombre)
            Empresas = await _db.Companies
                .Select(c => new Row
                {
                    Id = c.Id,
                    Nombre = c.Name,     // <-- si tu modelo usa otro campo (p.ej. RazonSocial), cámbialo aquí
                    QboId = c.QboId
                })
                .OrderBy(r => r.Nombre)
                .ToListAsync();
        }

        // Stub: botón "Agregar" del Topbar aterriza aquí por ahora (sin lógica de Intuit todavía).
        public IActionResult OnGetAgregar()
        {
            TempData["Empresas_ShowWizard"] = "1";
            return Page();
        }
    }
}

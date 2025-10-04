using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;

namespace IvaFacilitador.Areas.Payroll.Pages.Empresas
{
    public class IndexModel : PageModel
    {
        private readonly PayrollDbContext _db;
        public IndexModel(PayrollDbContext db) => _db = db;

        public List<Row> Empresas { get; set; } = new();

        public class Row
        {
            public int Id { get; set; }
            public string? Nombre { get; set; }
            public string? QboId { get; set; }
            public string Status { get; set; } = "Sin conexión";
        }

        public async Task OnGet()
        {
            var data = await _db.Companies
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.QboId,
                    c.PayPolicy,
                    HasTokens = _db.PayrollQboTokens.Any(t => t.CompanyId == c.Id)
                })
                .ToListAsync();

            Empresas = data.Select(x => new Row
            {
                Id = x.Id,
                Nombre = x.Name,
                QboId = x.QboId,
                Status = !x.HasTokens ? "Sin conexión" : (IsParametrized(x.PayPolicy) ? "Listo" : "Pendiente")
            }).ToList();
        }

        // Soporta enlace: /Payroll/Empresas?handler=Agregar
        public IActionResult OnGetAgregar()
        {
            TempData["Empresas_ShowWizard"] = "1";
            return Page();
        }

        // Soporta botón dentro de <form> con asp-page-handler="Agregar"
        public IActionResult OnPostAgregar()
        {
            TempData["Empresas_ShowWizard"] = "1";
            return RedirectToPage();
        }

        private static bool IsParametrized(string? payPolicy)
        {
            if (string.IsNullOrWhiteSpace(payPolicy)) return false;
            try
            {
                using var doc = JsonDocument.Parse(payPolicy);
                var root = doc.RootElement;
                var acc = root.TryGetProperty("defaultExpenseAccountId", out var a) ? a.GetString() : null;
                var wi  = root.TryGetProperty("defaultWageItemId", out var w) ? w.GetString() : null;
                return !string.IsNullOrWhiteSpace(acc) && !string.IsNullOrWhiteSpace(wi);
            }
            catch { return false; }
        }
    }
}

using System.Text.Json;
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
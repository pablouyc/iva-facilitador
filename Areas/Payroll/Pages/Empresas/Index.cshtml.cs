using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;

namespace IvaFacilitador.Areas.Payroll.Pages.Empresas
{
    public class IndexModel : PageModel
    {
        private readonly PayrollDbContext _db;
        private readonly IConfiguration _cfg;

        public IndexModel(PayrollDbContext db, IConfiguration cfg)
        {
            _db = db;
            _cfg = cfg;
        }

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
                Status = !x.HasTokens
                    ? "Sin conexión"
                    : (IsParametrized(x.PayPolicy) ? "Listo" : "Pendiente")
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

        // ========= Handlers: Agregar =========

        // GET: /Payroll/Empresas?handler=Agregar
        public IActionResult OnGetAgregar()
        {
            return RedirectToIntuit();
        }

        // POST de respaldo si alguna vez se usa un <form>
        public IActionResult OnPostAgregar()
        {
            return RedirectToIntuit();
        }

        private IActionResult RedirectToIntuit()
        {
            var clientId    = _cfg["IntuitPayrollAuth:ClientId"]    ?? _cfg["IntuitPayrollAuth__ClientId"];
            var redirectUri = _cfg["IntuitPayrollAuth:RedirectUri"] ?? _cfg["IntuitPayrollAuth__RedirectUri"];
            var scopes      = _cfg["IntuitPayrollAuth:Scopes"]      ?? _cfg["IntuitPayrollAuth__Scopes"] ?? "com.intuit.quickbooks.accounting";

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
            {
                TempData["Empresas_ShowWizard"] = "1";
                TempData["Empresas_Warn"] = "Faltan credenciales de IntuitPayrollAuth (ClientId / RedirectUri).";
                return Page();
            }

            // companyId=0 => en el callback se creará la empresa si no existe
            var stateObj  = new { companyId = 0, returnTo = "/Payroll/Empresas" };
            var stateJson = JsonSerializer.Serialize(stateObj);
            var state     = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateJson));

            var url = "https://appcenter.intuit.com/connect/oauth2"
                      + "?client_id="    + Uri.EscapeDataString(clientId)
                      + "&response_type=code"
                      + "&scope="        + Uri.EscapeDataString(scopes)
                      + "&redirect_uri=" + Uri.EscapeDataString(redirectUri)
                      + "&state="        + Uri.EscapeDataString(state)
                      + "&prompt=consent";

            return Redirect(url);
        }
    }
}

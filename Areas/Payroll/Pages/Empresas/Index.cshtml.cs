
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using IvaFacilitador.Payroll.Services;

namespace IvaFacilitador.Areas.Payroll.Pages.Empresas
{
    public class IndexModel : PageModel
    {
        private readonly PayrollDbContext _db;
        private readonly IConfiguration _cfg;
        private readonly IPayrollAuthService _auth;
        private readonly ILogger<IndexModel> _log;

        public IndexModel(PayrollDbContext db, IConfiguration cfg, IPayrollAuthService auth, ILogger<IndexModel> log)
        {
            _db  = db;
            _cfg = cfg;
            _auth = auth;
            _log = log;
        }

        public List<Row> Empresas { get; set; } = new();

        public class Row
        {
            public int Id { get; set; }
            public string? Nombre { get; set; }
            public string? QboId { get; set; }
            public string Status { get; set; } = "Sin conexión";
        }

        // Blindado: nunca debe tirar 500
        public async Task OnGet(CancellationToken ct)
        {
            try
            {
                // 1) Autocuración: si el nombre es "Empresa vinculada ..." y hay tokens, traer nombre real desde QBO.
                await FixCompanyNamesFromQboAsync(ct);
            }
            catch (Exception ex)
            {
                // Si algo falla aquí, lo registramos y seguimos a la grilla.
                _log.LogWarning(ex, "Empresas/OnGet: falló la autocuración de nombres, continúo dibujando la grilla.");
                TempData["Empresas_Warn"] = "No se pudo validar los nombres con QBO en este momento. La lista se mostrará igualmente.";
            }

            // 2) Cargar grilla (si esto falla, sí es un problema de base/EF y hay que mirar logs)
            var data = await _db.Companies
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.QboId,
                    c.PayPolicy,
                    HasTokens = _db.PayrollQboTokens.Any(t => t.CompanyId == c.Id)
                })
                .ToListAsync(ct);

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

        /// <summary>
        /// Repara silenciosamente nombres "Empresa vinculada ..." usando companyinfo de QBO si hay tokens.
        /// </summary>
        private async Task FixCompanyNamesFromQboAsync(CancellationToken ct)
        {
            // Empresas a corregir (placeholder + con tokens)
            var candidates = await _db.Companies
                .Where(c => _db.PayrollQboTokens.Any(t => t.CompanyId == c.Id))
                .Where(c => string.IsNullOrWhiteSpace(c.Name) ||
                            c.Name.StartsWith("Empresa vinculada", StringComparison.OrdinalIgnoreCase))
                .ToListAsync(ct);

            if (candidates.Count == 0) return;

            var changed = false;

            foreach (var comp in candidates)
            {
                try
                {
                    var tok = await _db.PayrollQboTokens
                        .Where(t => t.CompanyId == comp.Id)
                        .OrderByDescending(t => t.Id)
                        .FirstOrDefaultAsync(ct);

                    if (tok == null || string.IsNullOrWhiteSpace(tok.RealmId) || string.IsNullOrWhiteSpace(tok.AccessToken))
                        continue;

                    var realName = await _auth.TryGetCompanyNameAsync(tok.RealmId!, tok.AccessToken, ct);
                    if (!string.IsNullOrWhiteSpace(realName) &&
                        !string.Equals(comp.Name, realName, StringComparison.Ordinal))
                    {
                        comp.Name = realName!;
                        changed = true;
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "No se pudo actualizar el nombre de la empresa {CompanyId} desde QBO.", comp.Id);
                }
            }

            if (changed)
                await _db.SaveChangesAsync(ct);
        }

        // ====== Botón "Agregar" (redirección a Intuit) ======
        public IActionResult OnGetAgregar() => RedirectToIntuit();
        public IActionResult OnPostAgregar() => RedirectToIntuit();

        private IActionResult RedirectToIntuit()
        {
            var clientId    = _cfg["IntuitPayrollAuth:ClientId"]    ?? _cfg["IntuitPayrollAuth__ClientId"];
            var redirectUri = _cfg["IntuitPayrollAuth:RedirectUri"] ?? _cfg["IntuitPayrollAuth__RedirectUri"];
            var scopes      = _cfg["IntuitPayrollAuth:Scopes"]      ?? _cfg["IntuitPayrollAuth__Scopes"] ?? "com.intuit.quickbooks.accounting";

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
            {
                TempData["Empresas_Warn"] = "Faltan credenciales de IntuitPayrollAuth (ClientId/RedirectUri).";
                return Page();
            }

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

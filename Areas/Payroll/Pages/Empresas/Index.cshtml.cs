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
            public string? DisplayId { get; set; }   // Cédula desde PayPolicy, si existe; si no, Id numérico
            public string? Nombre { get; set; }
            public string? QboId { get; set; }
            public string Status { get; set; } = "Sin conexión";
        }

        // Blindado: nunca debe tirar 500
        public async Task OnGet(CancellationToken ct)
        {
            try
            {
                // 1) Autocuración: si el nombre es placeholder y hay tokens, intentar nombre real desde QBO.
                await FixCompanyNamesFromQboAsync(ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Empresas/OnGet: falló la autocuración de nombres, continúo dibujando la grilla.");
                TempData["Empresas_Warn"] = "No se pudo validar los nombres con QBO en este momento. La lista se mostrará igualmente.";
            }

            // 2) Cargar grilla
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

            Empresas = new List<Row>();
            foreach (var x in data)
            {
                var info = TryReadPolicyStatus(x.PayPolicy);
                var status = !x.HasTokens ? "Sin conexión" : (info.Parametrized ? "Listo" : "Pendiente");

                Empresas.Add(new Row
                {
                    Id = x.Id,
                    DisplayId = string.IsNullOrWhiteSpace(info.Cedula) ? x.Id.ToString() : info.Cedula,
                    Nombre = x.Name,
                    QboId = x.QboId,
                    Status = status
                });
            }
        }

        // Compatibilidad: ahora IsParametrized usa el lector nuevo
        private static bool IsParametrized(string? payPolicy)
        {
            return TryReadPolicyStatus(payPolicy).Parametrized;
        }

        // Lee PayPolicy para saber si está parametrizada y extraer cédula
        private static (bool Parametrized, string? Cedula) TryReadPolicyStatus(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return (false, null);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? cedula = (root.TryGetProperty("cedula", out var c) && c.ValueKind == JsonValueKind.String)
                    ? c.GetString()
                    : null;

                bool hasPeriodo = root.TryGetProperty("periodo", out var per)
                                  && per.ValueKind == JsonValueKind.String
                                  && !string.IsNullOrWhiteSpace(per.GetString());

                bool hasAccounts = false;
                if (root.TryGetProperty("accounts", out var acc) && acc.ValueKind == JsonValueKind.Object)
                {
                    hasAccounts =
                        (acc.TryGetProperty("general", out var gen) && gen.ValueKind == JsonValueKind.Object && gen.EnumerateObject().Any()) ||
                        (acc.TryGetProperty("perSector", out var perSector) && perSector.ValueKind == JsonValueKind.Object && perSector.EnumerateObject().Any());
                }

                bool parametrized = hasPeriodo && hasAccounts; // criterio mínimo
                return (parametrized, cedula);
            }
            catch
            {
                return (false, null);
            }
        }

        /// <summary>
        /// Repara silenciosamente nombres "Empresa vinculada ..." usando companyinfo de QBO si hay tokens.
        /// </summary>
        private async Task FixCompanyNamesFromQboAsync(CancellationToken ct)
        {
            var candidates = await _db.Companies
                .Where(c => _db.PayrollQboTokens.Any(t => t.CompanyId == c.Id))
                .Where(c => c.Name == null || EF.Functions.Like(c.Name!, "Empresa vinculada%"))
                .ToListAsync(ct);

            if (candidates.Count == 0) return;

            var changed = false;

            foreach (var comp in candidates)
            {
                try
                {
                    var (realm, access) = await _auth.GetRealmAndValidAccessTokenAsync(comp.Id, ct);
                    Console.WriteLine($"[Payroll][FixNames] CompanyId={comp.Id} realm={realm}");
                    if (string.IsNullOrWhiteSpace(realm) || string.IsNullOrWhiteSpace(access)) continue;

                    var realName = await _auth.TryGetCompanyNameAsync(realm, access, ct);
                    Console.WriteLine($"[Payroll][FixNames] TryGetCompanyNameAsync -> {realName}");
                    if (!string.IsNullOrWhiteSpace(realName) &&
                        !string.Equals(comp.Name, realName, StringComparison.Ordinal))
                    {
                        comp.Name = realName!;
                        changed = true;
                        Console.WriteLine($"[Payroll][FixNames][DB] Updated CompanyId={comp.Id} Name={comp.Name}");
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

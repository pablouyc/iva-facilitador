using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
            public string? Cedula { get; set; }
            public string Status { get; set; } = "Sin conexión";
        }

        // Blindado: nunca debe tirar 500
        public async Task OnGet(CancellationToken ct)
        {
            try
            {
                // Autocuración de nombres desde QBO si hay tokens y el nombre es placeholder.
                await FixCompanyNamesFromQboAsync(ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Empresas/OnGet: falló la autocuración de nombres, continúo dibujando la grilla.");
                TempData["Empresas_Warn"] = "No se pudo validar los nombres con QBO en este momento. La lista se mostrará igualmente.";
            }

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
                Cedula = GetCedulaFromPolicy(x.PayPolicy),
                Status = !x.HasTokens
                    ? "Sin conexión"
                    : (IsParametrized(x.PayPolicy) ? "Listo" : "Pendiente")
            }).ToList();
        }

        private static string? GetCedulaFromPolicy(string? payPolicy)
        {
            if (string.IsNullOrWhiteSpace(payPolicy)) return null;
            try
            {
                using var doc = JsonDocument.Parse(payPolicy);
                var root = doc.RootElement;
                return root.TryGetProperty("cedula", out var c) && c.ValueKind == JsonValueKind.String
                    ? c.GetString()
                    : null;
            }
            catch { return null; }
        }

        // "Parametrizada" si:
        // - tiene periodo válido
        // - tiene >= 1 sector
        // - y TODAS las claves contables requeridas están asignadas:
        //   * si splitBySector=false: en accounts.general TODAS las claves
        //   * si splitBySector=true : para CADA sector, TODAS las claves en accounts.perSector[sector]
        private static bool IsParametrized(string? payPolicy)
        {
            if (string.IsNullOrWhiteSpace(payPolicy)) return false;
            try
            {
                using var doc = JsonDocument.Parse(payPolicy);
                var root = doc.RootElement;

                // Periodo
                if (!(root.TryGetProperty("periodo", out var p) &&
                      p.ValueKind == JsonValueKind.String &&
                      !string.IsNullOrWhiteSpace(p.GetString())))
                    return false;

                // Sectores
                var sectors = new List<string>();
                if (root.TryGetProperty("sectors", out var secs) && secs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in secs.EnumerateArray())
                    {
                        var s = e.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) sectors.Add(s!);
                    }
                }
                if (sectors.Count == 0) return false;

                // splitBySector
                bool split = false;
                if (root.TryGetProperty("splitBySector", out var sb))
                {
                    split = sb.ValueKind == JsonValueKind.True
                            || (sb.ValueKind == JsonValueKind.String && bool.TryParse(sb.GetString(), out var b) && b);
                }

                // accounts
                if (!(root.TryGetProperty("accounts", out var accNode) && accNode.ValueKind == JsonValueKind.Object))
                    return false;

                var keys = new[] { "SalarioBruto", "Extras", "CCSS", "Deducciones", "SalarioNeto" };

                if (split)
                {
                    if (!(accNode.TryGetProperty("perSector", out var perSector) && perSector.ValueKind == JsonValueKind.Object))
                        return false;

                    foreach (var secName in sectors)
                    {
                        if (!(perSector.TryGetProperty(secName, out var map) && map.ValueKind == JsonValueKind.Object))
                            return false;

                        foreach (var k in keys)
                        {
                            if (!(map.TryGetProperty(k, out var v) &&
                                  v.ValueKind == JsonValueKind.String &&
                                  !string.IsNullOrWhiteSpace(v.GetString())))
                                return false;
                        }
                    }
                }
                else
                {
                    if (!(accNode.TryGetProperty("general", out var gen) && gen.ValueKind == JsonValueKind.Object))
                        return false;

                    foreach (var k in keys)
                    {
                        if (!(gen.TryGetProperty(k, out var v) &&
                              v.ValueKind == JsonValueKind.String &&
                              !string.IsNullOrWhiteSpace(v.GetString())))
                            return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
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
                    if (string.IsNullOrWhiteSpace(realm) || string.IsNullOrWhiteSpace(access)) continue;

                    var realName = await _auth.TryGetCompanyNameAsync(realm, access, ct);
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

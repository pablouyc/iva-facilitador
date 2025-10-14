<<<<<<< HEAD
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using IvaFacilitador.Payroll.Services;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
=======
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
using IvaFacilitador.Payroll.Services;      // IPayrollQboApi
using IvaFacilitador.Data.Payroll;          // PayrollDbContext (de Fase 1)
>>>>>>> 67d5566 (Deploy: Parametrizador Payroll enlazado a memoria (PayPolicy + PayrollQboToken) y migraciones aplicadas)

namespace IvaFacilitador.Areas.Payroll.Pages.Parametrizador
{
    public class IndexModel : PageModel
    {
        private readonly PayrollDbContext _db;
        private readonly IPayrollQboApi _api;
        private readonly ILogger<IndexModel> _log;
        private readonly IConfiguration _cfg;

        public IndexModel(PayrollDbContext db, IPayrollQboApi api, ILogger<IndexModel> log, IConfiguration cfg)
        {
            _db = db; _api = api; _log = log; _cfg = cfg;
        }

<<<<<<< HEAD
        [BindProperty(SupportsGet = true)]
        public int? id { get; set; }

        public int    CompanyId   { get; set; }
        public string? CompanyName { get; set; }
        public string? RealmId     { get; set; }
        public bool   HasTokens    { get; set; }

        public class Opt { public string Id { get; set; } = ""; public string Name { get; set; } = ""; }

        public List<Opt> Accounts { get; set; } = new();
        public List<Opt> Items    { get; set; } = new();

        [BindProperty] public string? ExpenseAccountId { get; set; }
        [BindProperty] public string? WageItemId       { get; set; }

        private async Task<(string realm, string access)> LoadTokensAsync(int companyId, CancellationToken ct)
        {
            var tk = await _db.PayrollQboTokens
                              .Where(t => t.CompanyId == companyId)
                              .OrderByDescending(t => t.Id)
=======
        // -------- Navegación / Estado --------
        [BindProperty(SupportsGet = true)]
        public string? id { get; set; }                                 // Guid string
        public Guid EmpresaId { get; set; }
        [BindProperty] public string? CompanyId { get; set; }           // compat hidden en la vista (string Guid)
        [BindProperty] public string? CompanyName { get; set; }         // mapea a Empresa.Nombre
        public string? RealmId { get; set; }
        public bool HasTokens { get; set; }

        // -------- Listas QBO --------
        public class Opt { public string Id { get; set; } = ""; public string Name { get; set; } = ""; }
        public List<Opt> Accounts { get; set; } = new();
        public List<Opt> Items    { get; set; } = new();

        // -------- Compat simple (si UI vieja lo usa) --------
        [BindProperty] public string? ExpenseAccountId { get; set; }
        [BindProperty] public string? WageItemId       { get; set; }

        // -------- BLOQUE 1: PayPolicy base --------
        [BindProperty] public string TaxId { get; set; } = string.Empty;         // cédula
        [BindProperty] public string PayFrequency { get; set; } = "Mensual";     // Mensual|Quincenal|Semanal
        [BindProperty] public bool   SepararPorSector { get; set; } = false;
        [BindProperty] public List<string> Sectores { get; set; } = new();

        private static readonly JsonSerializerOptions _ppJson =
            new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

        private class PayPolicyDto
        {
            public string? PayFrequency { get; set; }
            public bool?   SepararPorSector { get; set; }
            public List<string>? Sectores { get; set; }
            public object? Accounts { get; set; }    // BLOQUE 2
            public string? WageItemId { get; set; }
            public string? TaxId { get; set; }
            public object DefaultRules { get; set; } = new {
                quincenal = new [] { "1-15", "16-eom" },
                semanal   = new { weekStart = "monday" }
            };
            public object Qbo { get; set; } = new { realmId = "", nombreEmpresa = "" };
        }

        // -------- BLOQUE 2: mapeo de cuentas --------
        [BindProperty] public Dictionary<string, string?> AccountMapDefault { get; set; } = new();
        [BindProperty] public Dictionary<string, Dictionary<string, string?>> AccountMapPorSector { get; set; } = new();
        public static readonly string[] TiposCuenta = new[] { "SalarioBruto", "HorasExtras", "CCSS", "Deducciones", "SalarioNeto", "Otros" };

        // ================== Helpers ==================
        private async Task<(string realm, string access)> LoadTokensAsync(Guid empresaId, CancellationToken ct)
        {
            var tk = await _db.PayrollQboTokens
                              .Where(t => t.EmpresaId == empresaId)
                              .OrderByDescending(t => t.ExpiresAtUtc)
>>>>>>> 67d5566 (Deploy: Parametrizador Payroll enlazado a memoria (PayPolicy + PayrollQboToken) y migraciones aplicadas)
                              .FirstOrDefaultAsync(ct);
            if (tk == null) throw new InvalidOperationException("No hay tokens de Payroll para esta empresa.");
            return (tk.RealmId ?? "", tk.AccessToken);
        }

<<<<<<< HEAD
        private async Task<bool> TokensExistAsync(int companyId, CancellationToken ct) =>
            await _db.PayrollQboTokens.AnyAsync(t => t.CompanyId == companyId, ct);
=======
        private Task<bool> TokensExistAsync(Guid empresaId, CancellationToken ct) =>
            _db.PayrollQboTokens.AnyAsync(t => t.EmpresaId == empresaId, ct);
>>>>>>> 67d5566 (Deploy: Parametrizador Payroll enlazado a memoria (PayPolicy + PayrollQboToken) y migraciones aplicadas)

        private void LoadDefaultsFromPolicy(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
<<<<<<< HEAD
                ExpenseAccountId = root.TryGetProperty("defaultExpenseAccountId", out var a) ? a.GetString() : null;
                WageItemId       = root.TryGetProperty("defaultWageItemId", out var i) ? i.GetString() : null;
=======

                // Compat antiguos
                ExpenseAccountId = root.TryGetProperty("defaultExpenseAccountId", out var a) ? a.GetString() : null;
                WageItemId       = root.TryGetProperty("defaultWageItemId", out var i) ? i.GetString() : null;

                // Base
                PayFrequency     = root.TryGetProperty("payFrequency", out var pf) ? (pf.GetString() ?? PayFrequency) : PayFrequency;
                SepararPorSector = root.TryGetProperty("separarPorSector", out var sps) && sps.GetBoolean();
                TaxId            = root.TryGetProperty("taxId", out var tx) ? (tx.GetString() ?? TaxId) : TaxId;

                if (root.TryGetProperty("sectores", out var secs) && secs.ValueKind == JsonValueKind.Array)
                    Sectores = secs.EnumerateArray().Select(e => e.GetString() ?? "")
                                   .Where(x => !string.IsNullOrWhiteSpace(x))
                                   .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (root.TryGetProperty("accounts", out var acc) && acc.ValueKind == JsonValueKind.Object)
                {
                    if (acc.TryGetProperty("default", out var def) && def.ValueKind == JsonValueKind.Object)
                    {
                        AccountMapDefault = def.EnumerateObject()
                                               .ToDictionary(p => p.Name, p => p.Value.GetString(), StringComparer.OrdinalIgnoreCase);
                    }
                    if (acc.TryGetProperty("porSector", out var per) && per.ValueKind == JsonValueKind.Object)
                    {
                        var tmp = new Dictionary<string, Dictionary<string, string?>>(StringComparer.OrdinalIgnoreCase);
                        foreach (var s in per.EnumerateObject())
                        {
                            if (s.Value.ValueKind != JsonValueKind.Object) continue;
                            tmp[s.Name] = s.Value.EnumerateObject()
                                                .ToDictionary(p => p.Name, p => p.Value.GetString(), StringComparer.OrdinalIgnoreCase);
                        }
                        AccountMapPorSector = tmp;
                    }
                }
>>>>>>> 67d5566 (Deploy: Parametrizador Payroll enlazado a memoria (PayPolicy + PayrollQboToken) y migraciones aplicadas)
            }
            catch { /* ignore */ }
        }

<<<<<<< HEAD
        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            var comp = await _db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
            if (id.HasValue) comp = await _db.Companies.FindAsync(new object[] { id!.Value }, ct);
            if (comp == null) return Redirect("/Payroll/Empresas");

            CompanyId   = comp.Id;
            CompanyName = comp.Name;
            LoadDefaultsFromPolicy(comp.PayPolicy);

            HasTokens = await TokensExistAsync(CompanyId, ct);
=======
        // ================== GET ==================
        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            var emp = await _db.Empresas.OrderBy(e => e.CreatedAtUtc).FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(id) && Guid.TryParse(id, out var gid))
                emp = await _db.Empresas.FindAsync(new object[] { gid }, ct);

            if (emp == null) return Redirect("/Payroll/Empresas");

            EmpresaId   = emp.Id;
            CompanyId   = emp.Id.ToString();
            CompanyName = emp.Nombre;
            RealmId     = emp.RealmId;

            LoadDefaultsFromPolicy(emp.PayPolicy);

            HasTokens = await TokensExistAsync(EmpresaId, ct);
>>>>>>> 67d5566 (Deploy: Parametrizador Payroll enlazado a memoria (PayPolicy + PayrollQboToken) y migraciones aplicadas)

            if (HasTokens)
            {
                try
                {
<<<<<<< HEAD
                    var (realm, access) = await LoadTokensAsync(CompanyId, ct);
                    RealmId = realm;

                    // Listas desde QBO
=======
                    var (realm, access) = await LoadTokensAsync(EmpresaId, ct);
                    RealmId = realm;

>>>>>>> 67d5566 (Deploy: Parametrizador Payroll enlazado a memoria (PayPolicy + PayrollQboToken) y migraciones aplicadas)
                    var accs = await _api.GetExpenseAccountsAsync(realm, access, ct);
                    Accounts = accs.Select(a => new Opt { Id = a.Id, Name = a.Name }).ToList();

                    var items = await _api.GetServiceItemsAsync(realm, access, ct);
                    Items = items.Select(i => new Opt { Id = i.Id, Name = i.Name }).ToList();

<<<<<<< HEAD
                    // Refrescar nombre de empresa si está genérico
                    if (string.IsNullOrWhiteSpace(CompanyName) || CompanyName.StartsWith("Empresa vinculada "))
=======
                    if (string.IsNullOrWhiteSpace(CompanyName))
>>>>>>> 67d5566 (Deploy: Parametrizador Payroll enlazado a memoria (PayPolicy + PayrollQboToken) y migraciones aplicadas)
                    {
                        var realName = await _api.GetCompanyNameAsync(realm, access, ct);
                        if (!string.IsNullOrWhiteSpace(realName))
                        {
                            CompanyName = realName;
<<<<<<< HEAD

                            if (!string.Equals(comp.Name, realName, StringComparison.Ordinal))
                            {
                                comp.Name = realName!;
=======
                            if (!string.Equals(emp.Nombre, realName, StringComparison.Ordinal))
                            {
                                emp.Nombre = realName!;
>>>>>>> 67d5566 (Deploy: Parametrizador Payroll enlazado a memoria (PayPolicy + PayrollQboToken) y migraciones aplicadas)
                                await _db.SaveChangesAsync(ct);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "No se pudieron obtener listas/CompanyInfo desde QBO.");
                }
            }

            return Page();
        }

<<<<<<< HEAD
        public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
        {
            if (!Request.Form.TryGetValue("CompanyId", out var cid) || !int.TryParse(cid.ToString(), out var companyId))
                return BadRequest("companyId inválido.");

            var comp = await _db.Companies.FindAsync(new object[] { companyId }, ct);
            if (comp == null) return NotFound();
=======
        // ================== POSTs ==================
        // Compat antiguo (solo guarda cuentas básicas en JSON)
        public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
        {
            Guid gid;
            if (!string.IsNullOrWhiteSpace(CompanyId) && Guid.TryParse(CompanyId, out gid))
            { /* ok */ }
            else if (!string.IsNullOrWhiteSpace(id) && Guid.TryParse(id, out gid))
            { /* ok */ }
            else
            {
                var first = await _db.Empresas.OrderBy(e => e.CreatedAtUtc).Select(e => e.Id).FirstOrDefaultAsync(ct);
                gid = first;
            }

            var emp = await _db.Empresas.FindAsync(new object[] { gid }, ct);
            if (emp == null) return NotFound();
>>>>>>> 67d5566 (Deploy: Parametrizador Payroll enlazado a memoria (PayPolicy + PayrollQboToken) y migraciones aplicadas)

            var json = JsonSerializer.Serialize(new {
                defaultExpenseAccountId = ExpenseAccountId,
                defaultWageItemId       = WageItemId
            });
<<<<<<< HEAD

            comp.PayPolicy = json;
            await _db.SaveChangesAsync(ct);

            TempData["ok"] = "Parámetros guardados.";
            return Redirect($"/Payroll/Parametrizador/{companyId}");
=======
            emp.PayPolicy = json;
            await _db.SaveChangesAsync(ct);

            TempData["ok"] = "Parámetros guardados.";
            return Redirect($"/Payroll/Parametrizador/{gid}");
        }

        // BLOQUE 1: guarda política base
        public async Task<IActionResult> OnPostSavePolicyAsync(CancellationToken ct)
        {
            var emp = await _db.Empresas.OrderBy(e => e.CreatedAtUtc).FirstOrDefaultAsync(ct);
            if (emp is null) return NotFound("No existe Empresa para guardar PayPolicy.");

            if (!string.IsNullOrWhiteSpace(CompanyName)) emp.Nombre = CompanyName.Trim();

            var sectores = (Sectores ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (sectores.Count == 0) sectores.Add("General");

            var dto = new PayPolicyDto {
                PayFrequency     = string.IsNullOrWhiteSpace(PayFrequency) ? "Mensual" : PayFrequency,
                SepararPorSector = SepararPorSector,
                Sectores         = sectores,
                WageItemId       = WageItemId,
                TaxId            = string.IsNullOrWhiteSpace(TaxId) ? null : TaxId.Trim(),
                Qbo              = new { realmId = RealmId ?? "", nombreEmpresa = emp.Nombre ?? "" }
            };

            try {
                if (!string.IsNullOrWhiteSpace(emp.PayPolicy))
                {
                    var existing = JsonSerializer.Deserialize<Dictionary<string, object?>>(emp.PayPolicy) ?? new();
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(dto, _ppJson)) ?? new();
                    if (existing.TryGetValue("accounts", out var accountsBlock) && accountsBlock != null)
                        dict["accounts"] = accountsBlock;
                    emp.PayPolicy = JsonSerializer.Serialize(dict, _ppJson);
                }
                else
                {
                    emp.PayPolicy = JsonSerializer.Serialize(dto, _ppJson);
                }
            } catch {
                emp.PayPolicy = JsonSerializer.Serialize(dto, _ppJson);
            }

            await _db.SaveChangesAsync(ct);
            TempData["ok"] = "Parámetros básicos guardados";
            return RedirectToPage();
        }

        // BLOQUE 2: guarda mapeo de cuentas
        public async Task<IActionResult> OnPostSaveAccountsAsync(CancellationToken ct)
        {
            var emp = await _db.Empresas.OrderBy(e => e.CreatedAtUtc).FirstOrDefaultAsync(ct);
            if (emp is null) return NotFound("No existe Empresa para guardar mapeo de cuentas.");

            var policy = new Dictionary<string, object?>();
            try {
                if (!string.IsNullOrWhiteSpace(emp.PayPolicy))
                {
                    var tmp = JsonSerializer.Deserialize<Dictionary<string, object?>>(emp.PayPolicy);
                    if (tmp != null) policy = tmp;
                }
            } catch { /* ignore */ }

            var accountsObj = new Dictionary<string, object?>();

            if (AccountMapDefault != null && AccountMapDefault.Count > 0)
                accountsObj["default"] = AccountMapDefault;

            if (SepararPorSector && AccountMapPorSector != null && AccountMapPorSector.Count > 0)
                accountsObj["porSector"] = AccountMapPorSector;

            policy["accounts"] = accountsObj;

            emp.PayPolicy = JsonSerializer.Serialize(policy, _ppJson);
            await _db.SaveChangesAsync(ct);

            TempData["ok"] = "Cuentas de planilla guardadas";
            return RedirectToPage();
>>>>>>> 67d5566 (Deploy: Parametrizador Payroll enlazado a memoria (PayPolicy + PayrollQboToken) y migraciones aplicadas)
        }

        public async Task<IActionResult> OnPostSyncAsync(CancellationToken ct)
        {
<<<<<<< HEAD
            if (!Request.Form.TryGetValue("CompanyId", out var cid) || !int.TryParse(cid.ToString(), out var companyId))
                return BadRequest("companyId inválido.");
            id = companyId;
=======
            if (!string.IsNullOrWhiteSpace(CompanyId) && Guid.TryParse(CompanyId, out var gid))
                id = gid.ToString();
>>>>>>> 67d5566 (Deploy: Parametrizador Payroll enlazado a memoria (PayPolicy + PayrollQboToken) y migraciones aplicadas)
            return await OnGetAsync(ct);
        }

        public IActionResult OnPostConnect()
        {
<<<<<<< HEAD
            if (!Request.Form.TryGetValue("CompanyId", out var cid) || !int.TryParse(cid.ToString(), out var companyId))
                return BadRequest("companyId inválido.");
=======
            if (string.IsNullOrWhiteSpace(CompanyId) || !Guid.TryParse(CompanyId, out var gid))
                return BadRequest("EmpresaId inválido.");
>>>>>>> 67d5566 (Deploy: Parametrizador Payroll enlazado a memoria (PayPolicy + PayrollQboToken) y migraciones aplicadas)

            var clientId    = _cfg["IntuitPayrollAuth:ClientId"]    ?? _cfg["IntuitPayrollAuth__ClientId"];
            var redirectUri = _cfg["IntuitPayrollAuth:RedirectUri"] ?? _cfg["IntuitPayrollAuth__RedirectUri"];
            var scopes      = _cfg["IntuitPayrollAuth:Scopes"]      ?? _cfg["IntuitPayrollAuth__Scopes"] ?? "com.intuit.quickbooks.accounting";

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
                return BadRequest("Faltan credenciales de IntuitPayrollAuth.");

<<<<<<< HEAD
            var stateObj  = new { companyId = companyId, returnTo = $"/Payroll/Parametrizador/{companyId}" };
=======
            var stateObj  = new { empresaId = gid, returnTo = $"/Payroll/Parametrizador/{gid}" };
>>>>>>> 67d5566 (Deploy: Parametrizador Payroll enlazado a memoria (PayPolicy + PayrollQboToken) y migraciones aplicadas)
            var stateJson = JsonSerializer.Serialize(stateObj);
            var state     = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateJson));

            var url = "https://appcenter.intuit.com/connect/oauth2" +
                      "?client_id="    + Uri.EscapeDataString(clientId) +
                      "&response_type=code" +
                      "&scope="        + Uri.EscapeDataString(scopes) +
                      "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                      "&state="        + Uri.EscapeDataString(state) +
                      "&prompt=consent";

            return Redirect(url);
        }
    }
}

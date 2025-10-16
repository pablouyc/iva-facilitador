using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using IvaFacilitador.Payroll.Services;

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
            _db  = db;
            _api = api;
            _log = log;
            _cfg = cfg;
        }

        // ===== Parámetros =====
        [BindProperty(SupportsGet = true)]
        public int? id { get; set; }

        public int    CompanyId   { get; set; }
        public string? CompanyName { get; set; }
        public string? Cedula      { get; set; }
        public string? RealmId     { get; set; }
        public bool   HasTokens    { get; set; }

        // Toggle contable
        [BindProperty]
        public bool SplitBySector { get; set; }

        
        // Periodo de la nómina (Mensual/Quincenal/Semanal)
        public string Periodo { get; set; } = "Mensual";

        // Lista de nombres de sectores que la vista muestra/usa
        public List<string> SectorNames { get; set; } = new() { "General" };
// Sectores (mínimo 1)
        public List<string> Sectores { get; set; } = new() { "General" };

        // Claves contables
        public static readonly string[] Keys = new[] { "SalarioBruto", "Extras", "CCSS", "Deducciones", "SalarioNeto" };

        // Opciones de cuentas (de QBO)
        public class Opt
        {
            public string Id   { get; set; } = "";
            public string Name { get; set; } = "";
        }
        public List<Opt> Accounts { get; set; } = new();

        // Mapeos desde PayPolicy
        public Dictionary<string, string> GeneralAccounts { get; set; } = new(); // key -> accId
        public Dictionary<string, Dictionary<string, string>> AccountsBySector { get; set; } = new(); // sector -> (key->accId)

        // --------- Helpers tokens ----------
        private async Task<(string realm, string access)> LoadTokensAsync(int companyId, CancellationToken ct)
        {
            var tk = await _db.PayrollQboTokens
                              .Where(t => t.CompanyId == companyId)
                              .OrderByDescending(t => t.Id)
                              .FirstOrDefaultAsync(ct);
            if (tk == null) throw new InvalidOperationException("No hay tokens de Payroll para esta empresa.");
            return (tk.RealmId ?? "", tk.AccessToken);
        }

        private Task<bool> TokensExistAsync(int companyId, CancellationToken ct) =>
            _db.PayrollQboTokens.AnyAsync(t => t.CompanyId == companyId, ct);

        // --------- Carga desde PayPolicy ----------
        private void LoadFromPolicy(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                SplitBySector = root.TryGetProperty("splitBySector", out var sb) && sb.GetBoolean();

                // Sectores
                var sectors = new List<string>();
                if (root.TryGetProperty("sectors", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in arr.EnumerateArray())
                    {
                        var s = e.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) sectors.Add(s!);
                    }
                }
                if (sectors.Count > 0) Sectores = sectors;

                // Cuentas
                if (root.TryGetProperty("accounts", out var accNode))
                {
                    if (accNode.TryGetProperty("general", out var gen) && gen.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in gen.EnumerateObject())
                            GeneralAccounts[prop.Name] = prop.Value.GetString() ?? "";
                    }
                    if (accNode.TryGetProperty("perSector", out var perSector) && perSector.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var sec in perSector.EnumerateObject())
                        {
                            var map = new Dictionary<string, string>();
                            foreach (var prop in sec.Value.EnumerateObject())
                                map[prop.Name] = prop.Value.GetString() ?? "";
                            AccountsBySector[sec.Name] = map;
                        }
                    }
                }

                // Cedula opcional en policy
                if (root.TryGetProperty("cedula", out var c))
                    Cedula = c.GetString();
            
                if (root.TryGetProperty("periodo", out var per))
                    Periodo = per.GetString() ?? "Mensual";
}
            catch (Exception ex)
            {
                _log.LogWarning(ex, "No se pudo leer PayPolicy");
            }
        }

        // Valor preseleccionado en el grid (si existe)
        public string? SelectedFor(string sectorName, string key)
        {
            if (SplitBySector)
            {
                if (AccountsBySector.TryGetValue(sectorName, out var d) && d.TryGetValue(key, out var v))
                    return v;
            }
            else
            {
                if (GeneralAccounts.TryGetValue(key, out var v)) return v;
            }
            return null;
        }

        // --------- GET ----------
        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            var comp = await _db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
            if (id.HasValue) comp = await _db.Companies.FindAsync(new object[] { id!.Value }, ct);
            if (comp == null) return Redirect("/Payroll/Empresas");

            CompanyId   = comp.Id;
            CompanyName = comp.Name;
            LoadFromPolicy(comp.PayPolicy);

            
            SectorNames = (Sectores != null && Sectores.Count > 0)
                ? new List<string>(Sectores)
                : new List<string> { "General" };HasTokens = await TokensExistAsync(CompanyId, ct);
            if (HasTokens)
            {
                try
                {
                    var (realm, access) = await LoadTokensAsync(CompanyId, ct);
                    RealmId = realm;

                    // 1) Traer cuentas (para los selects)
                    var accs = await _api.GetExpenseAccountsAsync(realm, access, ct);
                    Accounts = accs?.Select(x => new Opt { Id = x.Id ?? "", Name = x.Name ?? "" }).ToList() ?? new();

                    // 2) Traer nombre real de la compañía (y sincronizar DB si cambió)
                    var realName = await _api.GetCompanyNameAsync(realm, access, ct);
                    if (!string.IsNullOrWhiteSpace(realName))
                    {
                        CompanyName = realName;

                        // <<< SINCRONIZA EN DB SI DIFERENTE >>>
                        if (!string.Equals(comp.Name, realName, StringComparison.Ordinal))
                        {
                            comp.Name = realName;
                            await _db.SaveChangesAsync(ct);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "No se pudieron cargar datos desde QBO.");
                }
            }

            return Page();
        }

        // --------- POST: Guardar ----------
        public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
        {
            if (!Request.Form.TryGetValue("CompanyId", out var cid) || !int.TryParse(cid.ToString(), out var companyId))
                return BadRequest("companyId inválido.");

            var comp = await _db.Companies.FindAsync(new object[] { companyId }, ct);
            if (comp == null) return NotFound();

            // Nombre y cédula (coalesce para evitar warnings nulos)
            CompanyName = Request.Form["CompanyName"].ToString() ?? string.Empty;
            Cedula      = Request.Form["Cedula"].ToString()      ?? string.Empty;
            Periodo     = Request.Form["Periodo"].ToString() ?? "Mensual";

            if (!string.IsNullOrWhiteSpace(CompanyName) &&
                !string.Equals(CompanyName, comp.Name, StringComparison.Ordinal))
            {
                comp.Name = CompanyName!;
            }
            // Si tienes un campo específico para cédula en Companies, asígnalo aquí.

            // Toggle
            SplitBySector =
                string.Equals(Request.Form["SplitBySector"], "on", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Request.Form["SplitBySector"], "true", StringComparison.OrdinalIgnoreCase);

            // Sectores (si no hay, mantener 1)
            Sectores = Request.Form["Sectores"]
                        .Select(s => (s ?? string.Empty).Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
            if (Sectores.Count == 0) Sectores = new List<string> { "General" };

            // Recoger filas del grid (traemos el nombre del sector que usa el grid)
            var rows = new List<(int idx, string name)>();
            foreach (var kv in Request.Form)
            {
                if (kv.Key.StartsWith("SectorRow_", StringComparison.OrdinalIgnoreCase))
                {
                    var tail = kv.Key.Substring("SectorRow_".Length);
                    if (int.TryParse(tail, out var idx))
                        rows.Add((idx, kv.Value.ToString()));
                }
            }
            rows = rows.OrderBy(x => x.idx).ToList();

            // Map general y por sector
            var genMap    = new Dictionary<string, string>();
            var perSector = new Dictionary<string, Dictionary<string, string>>();

            foreach (var row in rows)
            {
                var rowMap = new Dictionary<string, string>();
                foreach (var k in Keys)
                {
                    var fkey = $"Map_{row.idx}_{k}";
                    var val  = Request.Form[fkey].ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(val)) rowMap[k] = val;
                }

                if (SplitBySector)
                {
                    perSector[row.name] = rowMap;
                }
                else
                {
                    genMap = rowMap;
                    break; // solo 1 fila cuando no se separa por sector
                }
            }

            // Persistir en PayPolicy
            var policy = new
            {
                splitBySector = SplitBySector,
                sectors       = Sectores,
                cedula        = Cedula,
                
                    periodo = Periodo,
                    accounts      = new
                {
                    general  = genMap,
                    perSector = perSector
                }
            };
            comp.PayPolicy = JsonSerializer.Serialize(policy);
            await _db.SaveChangesAsync(ct);

            TempData["ok"] = "Parámetros de nómina guardados.";
            return Redirect($"/Payroll/Parametrizador/{companyId}");
        }

        // --------- POST: sincronizar (solo recarga la página y vuelve a consultar QBO si hay tokens)
        public async Task<IActionResult> OnPostSyncAsync(CancellationToken ct)
        {
            if (!Request.Form.TryGetValue("CompanyId", out var cid) || !int.TryParse(cid.ToString(), out var companyId))
                return BadRequest("companyId inválido.");
            id = companyId;
            return await OnGetAsync(ct);
        }

        // --------- GET: AJAX cuentas ----------
        public async Task<IActionResult> OnGetAccountsAsync(CancellationToken ct)
        {
            var comp = await _db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
            if (id.HasValue) comp = await _db.Companies.FindAsync(new object[] { id!.Value }, ct);
            if (comp == null) return new JsonResult(Array.Empty<object>());

            try
            {
                var (realm, access) = await LoadTokensAsync(comp.Id, ct);
                var accs = await _api.GetExpenseAccountsAsync(realm, access, ct);
                var list = accs?.Select(a => new { id = a.Id ?? "", name = a.Name ?? "" }).ToList() ?? new();
                return new JsonResult(list);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "GetAccountsAsync: error QBO");
                return new JsonResult(Array.Empty<object>());
            }
        }

        // --------- POST: conectar Intuit ----------
        public IActionResult OnPostConnect()
        {
            if (!Request.Form.TryGetValue("CompanyId", out var cid) || !int.TryParse(cid.ToString(), out var companyId))
                return BadRequest("companyId inválido.");

            var clientId    = _cfg["IntuitPayrollAuth:ClientId"]    ?? _cfg["IntuitPayrollAuth__ClientId"];
            var redirectUri = _cfg["IntuitPayrollAuth:RedirectUri"] ?? _cfg["IntuitPayrollAuth__RedirectUri"];
            var scopes      = _cfg["IntuitPayrollAuth:Scopes"]      ?? _cfg["IntuitPayrollAuth__Scopes"] ?? "com.intuit.quickbooks.accounting";

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
                return BadRequest("Faltan credenciales de IntuitPayrollAuth.");

            var stateObj  = new { companyId = companyId, returnTo = $"/Payroll/Parametrizador/{companyId}" };
            var stateJson = JsonSerializer.Serialize(stateObj);
            var state     = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateJson));

            var url = "https://appcenter.intuit.com/connect/oauth2" +
                      "?client_id="   + Uri.EscapeDataString(clientId) +
                      "&response_type=code" +
                      "&scope="       + Uri.EscapeDataString(scopes) +
                      "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                      "&state="       + Uri.EscapeDataString(state) +
                      "&prompt=consent";

            return Redirect(url);
        }
    }
}





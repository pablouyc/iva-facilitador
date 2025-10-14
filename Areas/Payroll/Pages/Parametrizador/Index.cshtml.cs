using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using IvaFacilitador.Payroll.Services;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;

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

        // ===== Routing =====
        [BindProperty(SupportsGet = true)]
        public int? id { get; set; }

        // ===== UI State =====
        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public string? RealmId { get; set; }
        public bool HasTokens { get; set; }

        // Campos solicitados que guardamos en PayPolicy (JSON)
        [BindProperty] public string? Cedula { get; set; }
        [BindProperty] public string? Periodicidad { get; set; } = "Quincenal";
        [BindProperty] public bool MapPorSector { get; set; } = false;

        public List<string> Sectores { get; set; } = new() { "General" };

        // Cuentas
        public List<SelectListItem> AccountOptions { get; set; } = new();
        [BindProperty] public string? GeneralExpenseAccountId { get; set; }

        // Mapa por sector (Sector -> AccountId)
        public Dictionary<string,string> SectorAccounts { get; set; } = new();

        // ===== Helpers QBO =====
        private async Task<(string realm, string access)> LoadTokensAsync(int companyId, CancellationToken ct)
        {
            var tk = await _db.PayrollQboTokens
                              .Where(t => t.CompanyId == companyId)
                              .OrderByDescending(t => t.Id)
                              .FirstOrDefaultAsync(ct);
            if (tk == null) throw new InvalidOperationException("No hay tokens de Payroll para esta empresa.");
            return (tk.RealmId ?? "", tk.AccessToken);
        }

        private async Task<bool> TokensExistAsync(int companyId, CancellationToken ct)
            => await _db.PayrollQboTokens.AnyAsync(t => t.CompanyId == companyId, ct);

        // ===== Policy JSON =====
        private void LoadFromPolicy(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;

                Cedula       = r.TryGetProperty("cedula", out var c)        ? c.GetString() : Cedula;
                Periodicidad = r.TryGetProperty("periodicidad", out var p)  ? p.GetString() : Periodicidad;
                MapPorSector = r.TryGetProperty("mapPorSector", out var m)  && m.GetBoolean();

                if (r.TryGetProperty("sectores", out var s) && s.ValueKind == JsonValueKind.Array)
                    Sectores = s.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                if (Sectores.Count == 0) Sectores.Add("General");

                GeneralExpenseAccountId = r.TryGetProperty("cuentaGeneralId", out var g) ? g.GetString() : null;

                SectorAccounts.Clear();
                if (r.TryGetProperty("cuentasPorSector", out var map) && map.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in map.EnumerateObject())
                        SectorAccounts[prop.Name] = prop.Value.GetString() ?? "";
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Error leyendo PayPolicy, se usarán defaults.");
            }
        }

        private string BuildPolicyJson()
        {
            var obj = new
            {
                cedula = Cedula,
                periodicidad = Periodicidad,
                mapPorSector = MapPorSector,
                sectores = Sectores,
                cuentaGeneralId = GeneralExpenseAccountId,
                cuentasPorSector = SectorAccounts
            };
            return JsonSerializer.Serialize(obj);
        }

        private void BuildAccountOptions(IEnumerable<(string Id, string Name)> src)
        {
            AccountOptions = new List<SelectListItem> {
                new SelectListItem("(sin seleccionar)", "")
            };
            foreach (var a in src)
                AccountOptions.Add(new SelectListItem(a.Name, a.Id));
        }

        // ===== GET =====
        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            // Empresa
            var comp = await _db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
            if (id.HasValue) comp = await _db.Companies.FindAsync(new object[] { id!.Value }, ct);
            if (comp == null) return Redirect("/Payroll/Empresas");

            CompanyId   = comp.Id;
            CompanyName = comp.Name;

            // Cargar política guardada
            LoadFromPolicy(comp.PayPolicy);

            // Tokens/QBO
            HasTokens = await TokensExistAsync(CompanyId, ct);
            if (HasTokens)
            {
                try
                {
                    var (realm, access) = await LoadTokensAsync(CompanyId, ct);
                    RealmId = realm;

                    // Cuentas desde QBO
                    var accs = await _api.GetExpenseAccountsAsync(realm, access, ct);
                    BuildAccountOptions(accs.Select(a => (a.Id, a.Name)));

                    // Actualizar nombre si está genérico
                    if (string.IsNullOrWhiteSpace(CompanyName) || CompanyName.StartsWith("Empresa vinculada "))
                    {
                        var realName = await _api.GetCompanyNameAsync(realm, access, ct);
                        if (!string.IsNullOrWhiteSpace(realName))
                        {
                            CompanyName = realName!;
                            if (!string.Equals(comp.Name, realName, StringComparison.Ordinal))
                            {
                                comp.Name = realName!;
                                await _db.SaveChangesAsync(ct);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "No se pudieron obtener datos desde QBO.");
                    BuildAccountOptions(Array.Empty<(string Id,string Name)>());
                }
            }
            else
            {
                BuildAccountOptions(Array.Empty<(string Id,string Name)>());
            }

            return Page();
        }

        // ===== POST: Guardar =====
        public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
        {
            if (!Request.Form.TryGetValue("CompanyId", out var cid) || !int.TryParse(cid.ToString(), out var companyId))
                return BadRequest("companyId inválido.");

            var comp = await _db.Companies.FindAsync(new object[] { companyId }, ct);
            if (comp == null) return NotFound();

            // Nombre de empresa editable
            CompanyName = Request.Form["CompanyName"];
            if (!string.IsNullOrWhiteSpace(CompanyName) && CompanyName != comp.Name)
                comp.Name = CompanyName!;

            // Sectores (del bloque principal)
            var sectorNames = (Request.Form["SectorNames[]"].ToArray() ?? Array.Empty<string>())
                              .Select(x => (x ?? "").Trim())
                              .Where(x => !string.IsNullOrWhiteSpace(x))
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToList();
            if (sectorNames.Count == 0) sectorNames.Add("General");
            Sectores = sectorNames;

            // Periodicidad, Cedula, MapPorSector ya vienen binded
            bool.TryParse(Request.Form["MapPorSector"], out var mapSector);
            MapPorSector = mapSector;

            Cedula = Request.Form["Cedula"];
            Periodicidad = Request.Form["Periodicidad"];

            // Cuentas
            GeneralExpenseAccountId = Request.Form["GeneralExpenseAccountId"];

            // Mapa por sector (tabla)
            var mapNames = Request.Form["SectorNamesMap[]"].ToArray();
            var mapIds   = Request.Form["SectorAccountIds[]"].ToArray();
            SectorAccounts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (mapNames != null && mapIds != null)
            {
                for (int i = 0; i < Math.Min(mapNames.Length, mapIds.Length); i++)
                {
                    var name = (mapNames[i] ?? "").Trim();
                    var val  = (mapIds[i] ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                        SectorAccounts[name] = val;
                }
            }

            // Persistir en PayPolicy
            comp.PayPolicy = BuildPolicyJson();
            await _db.SaveChangesAsync(ct);

            TempData["ok"] = "Parámetros guardados.";
            return Redirect($"/Payroll/Parametrizador/{companyId}");
        }

        // ===== POST: Sincronizar =====
        public async Task<IActionResult> OnPostSyncAsync(CancellationToken ct)
        {
            if (!Request.Form.TryGetValue("CompanyId", out var cid) || !int.TryParse(cid.ToString(), out var companyId))
                return BadRequest("companyId inválido.");
            id = companyId;
            return await OnGetAsync(ct);
        }

        // ===== POST: Conectar QBO =====
        public IActionResult OnPostConnect()
        {
            if (!Request.Form.TryGetValue("CompanyId", out var cid) || !int.TryParse(cid.ToString(), out var companyId))
                return BadRequest("companyId inválido.");

            var clientId    = _cfg["IntuitPayrollAuth:ClientId"]    ?? _cfg["IntuitPayrollAuth__ClientId"];
            var redirectUri = _cfg["IntuitPayrollAuth:RedirectUri"] ?? _cfg["IntuitPayrollAuth__RedirectUri"];
            var scopes      = _cfg["IntuitPayrollAuth:Scopes"]      ?? _cfg["IntuitPayrollAuth__Scopes"]
                              ?? "com.intuit.quickbooks.accounting";

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
            {
                TempData["err"] = "Faltan credenciales de Intuit (Payroll).";
                return Redirect($"/Payroll/Parametrizador/{companyId}");
            }

            var stateObj  = new { companyId, returnTo = $"/Payroll/Parametrizador/{companyId}" };
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



using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

        // ---- UI State ----
        [BindProperty(SupportsGet = true)] public int? id { get; set; }

        public int      CompanyId     { get; set; }
        public string?  CompanyName   { get; set; }
        public string?  Cedula        { get; set; }
        public string?  RealmId       { get; set; }
        public string?  Periodicidad  { get; set; } = "Quincenal";
        public bool     SepararPorSectores { get; set; } = false;

        public readonly string[] Categories = new[] { "SalarioBruto", "Extras", "CCSS", "Deducciones", "SalarioNeto" };

        public class Opt { public string Id { get; set; } = ""; public string Name { get; set; } = ""; }
        public List<Opt> Accounts { get; set; } = new();

        public List<(string Name, string Key)> SectorKeys { get; private set; } = new();

        // General mapping and sector mapping (category -> accountId)
        public Dictionary<string, string> GeneralMap { get; set; } = new();
        public Dictionary<string, Dictionary<string, string>> MapPorSector { get; set; } = new();

        public string? TryGetSectorSelection(string sectorName, string cat)
        {
            if (MapPorSector.TryGetValue(sectorName, out var inner) && inner.TryGetValue(cat, out var v)) return v;
            return null;
        }

        // ---- Helpers ----
        private static string ToKey(string s)
        {
            var arr = s.Normalize(NormalizationForm.FormD)
                       .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                       .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_')
                       .ToArray();
            return new string(arr);
        }

        private async Task<(string realm, string access)> LoadTokensAsync(int companyId, CancellationToken ct)
        {
            var tk = await _db.PayrollQboTokens
                              .Where(t => t.CompanyId == companyId)
                              .OrderByDescending(t => t.Id)
                              .FirstOrDefaultAsync(ct);
            if (tk == null) throw new InvalidOperationException("No hay tokens de Payroll para esta empresa.");
            return (tk.RealmId ?? "", tk.AccessToken);
        }

        private async Task<bool> TokensExistAsync(int companyId, CancellationToken ct) =>
            await _db.PayrollQboTokens.AnyAsync(t => t.CompanyId == companyId, ct);

        private void EnsureDefaultSectors()
        {
            if (SectorKeys.Count == 0) SectorKeys = new List<(string, string)> { ("General", ToKey("General")) };
        }

        private void LoadFromPolicyJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) { EnsureDefaultSectors(); return; }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                Cedula        = root.TryGetProperty("cedula", out var ce) ? ce.GetString() : Cedula;
                Periodicidad  = root.TryGetProperty("periodicidad", out var pe) ? pe.GetString() : Periodicidad;
                SepararPorSectores = root.TryGetProperty("separarPorSectores", out var sp) && sp.GetBoolean();

                // sectores
                SectorKeys = new();
                if (root.TryGetProperty("sectores", out var ss) && ss.ValueKind == JsonValueKind.Array && ss.GetArrayLength() > 0)
                {
                    foreach (var s in ss.EnumerateArray())
                    {
                        var name = s.GetString() ?? "General";
                        SectorKeys.Add((name, ToKey(name)));
                    }
                }
                else
                {
                    EnsureDefaultSectors();
                }

                // general
                GeneralMap = new();
                if (root.TryGetProperty("generalAccounts", out var ga) && ga.ValueKind == JsonValueKind.Object)
                {
                    foreach (var cat in Categories)
                    {
                        if (ga.TryGetProperty(cat, out var v) && v.ValueKind == JsonValueKind.String)
                            GeneralMap[cat] = v.GetString()!;
                    }
                }

                // por sector
                MapPorSector = new();
                if (root.TryGetProperty("mapPorSector", out var mps) && mps.ValueKind == JsonValueKind.Object)
                {
                    foreach (var sProp in mps.EnumerateObject())
                    {
                        var dict = new Dictionary<string, string>();
                        foreach (var cat in Categories)
                        {
                            if (sProp.Value.ValueKind == JsonValueKind.Object &&
                                sProp.Value.TryGetProperty(cat, out var v) &&
                                v.ValueKind == JsonValueKind.String)
                            {
                                dict[cat] = v.GetString()!;
                            }
                        }
                        if (dict.Count > 0) MapPorSector[sProp.Name] = dict;
                    }
                }
            }
            catch
            {
                EnsureDefaultSectors(); // robustez si el JSON viejo no coincide
            }
        }

        // ---- GET ----
        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            var comp = await _db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
            if (id.HasValue) comp = await _db.Companies.FindAsync(new object[] { id!.Value }, ct);
            if (comp == null) return Redirect("/Payroll/Empresas");

            CompanyId   = comp.Id;
            CompanyName = comp.Name;

            LoadFromPolicyJson(comp.PayPolicy);

            // QBO accounts
            if (await TokensExistAsync(CompanyId, ct))
            {
                try
                {
                    var (realm, access) = await LoadTokensAsync(CompanyId, ct);
                    RealmId = realm;

                    var accs = await _api.GetExpenseAccountsAsync(realm, access, ct);
                    Accounts = accs.Select(a => new Opt { Id = a.Id, Name = a.Name }).ToList();
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "No se pudieron obtener cuentas desde QBO.");
                }
            }

            return Page();
        }

        // ---- POST SAVE ----
        public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
        {
            if (!Request.Form.TryGetValue("CompanyId", out var cid) || !int.TryParse(cid, out var companyId))
                return BadRequest("companyId inválido.");

            var comp = await _db.Companies.FindAsync(new object[] { companyId }, ct);
            if (comp == null) return NotFound();

            CompanyId = comp.Id;

            // Campos básicos
            var newName = Request.Form["CompanyName"].ToString();
            Cedula       = Request.Form["Cedula"].ToString();
            Periodicidad = Request.Form["Periodicidad"].ToString();
            SepararPorSectores = Request.Form.ContainsKey("SepSectores");

            // Sectores
            var sectores = Request.Form["Sectores"].Select(s => s.ToString().Trim())
                                                   .Where(s => !string.IsNullOrWhiteSpace(s))
                                                   .Distinct(StringComparer.Ordinal)
                                                   .ToList();
            if (sectores.Count == 0) sectores.Add("General");

            // Cuentas generales
            var general = new Dictionary<string, string>();
            foreach (var cat in Categories)
            {
                var val = Request.Form[$"gen_{cat}"].ToString();
                if (!string.IsNullOrWhiteSpace(val)) general[cat] = val;
            }

            // Cuentas por sector (acc_{key}_{cat})
            var porSector = new Dictionary<string, Dictionary<string, string>>();
            foreach (var s in sectores)
            {
                var key = ToKey(s);
                var dict = new Dictionary<string, string>();
                foreach (var cat in Categories)
                {
                    var val = Request.Form[$"acc_{key}_{cat}"].ToString();
                    if (!string.IsNullOrWhiteSpace(val)) dict[cat] = val;
                }
                if (dict.Count > 0) porSector[s] = dict;
            }

            // Guardar nombre si cambió
            if (!string.IsNullOrWhiteSpace(newName) && !string.Equals(comp.Name, newName, StringComparison.Ordinal))
                comp.Name = newName;

            // Persistir PayPolicy
            var policy = new
            {
                cedula = Cedula,
                periodicidad = Periodicidad,
                separarPorSectores = SepararPorSectores,
                sectores = sectores,
                generalAccounts = general,
                mapPorSector = porSector
            };
            comp.PayPolicy = JsonSerializer.Serialize(policy, new JsonSerializerOptions { WriteIndented = false });

            await _db.SaveChangesAsync(ct);

            TempData["ok"] = "Parámetros guardados.";
            return Redirect($"/Payroll/Parametrizador/{companyId}");
        }

        // ---- POST SYNC (QBO) ----
        public async Task<IActionResult> OnPostSyncAsync(CancellationToken ct)
        {
            if (!Request.Form.TryGetValue("CompanyId", out var cid) || !int.TryParse(cid, out var companyId))
                return BadRequest("companyId inválido.");

            id = companyId;
            return await OnGetAsync(ct);
        }
    }
}

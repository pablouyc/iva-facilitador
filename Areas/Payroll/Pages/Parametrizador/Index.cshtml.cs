﻿using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using IvaFacilitador.Payroll.Services;

namespace IvaFacilitador.Areas.Payroll.Pages.Parametrizador
{
    public class IndexModel : PageModel
    {
        private readonly PayrollDbContext _db;
        private readonly IPayrollQboApi _api;
        private readonly IPayrollAuthService _auth;
        private readonly ILogger<IndexModel> _log;

        public IndexModel(
            PayrollDbContext db,
            IPayrollQboApi api,
            IPayrollAuthService auth,
            ILogger<IndexModel> log)
        {
            _db   = db;
            _api  = api;
            _auth = auth;
            _log  = log;
        }

        [BindProperty(SupportsGet = true)]
        public int? id { get; set; }

        public int    CompanyId    { get; set; }
        public string? CompanyName { get; set; }
        public string? RealmId     { get; set; }
        public bool   HasTokens    { get; set; }

        public class Opt { public string Id { get; set; } = ""; public string Name { get; set; } = ""; }
        public List<Opt> Accounts { get; set; } = new();

        [BindProperty] public bool SplitBySector { get; set; }
        public Dictionary<string, string> GeneralAccounts { get; set; } = new();
        public Dictionary<string, Dictionary<string, string>> AccountsBySector { get; set; } = new();
        public List<string> SectorNames { get; set; } = new();

        private void LoadFromPolicy(string? json)
        {
            SplitBySector = false;
            GeneralAccounts = new();
            AccountsBySector = new();
            SectorNames = new();

            if (string.IsNullOrWhiteSpace(json)) { SectorNames.Add("General"); return; }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("splitBySector", out var s))
                    SplitBySector = s.GetBoolean();

                if (SplitBySector)
                {
                    if (root.TryGetProperty("bySector", out var bySec) && bySec.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var sec in bySec.EnumerateObject())
                        {
                            var map = new Dictionary<string, string>();
                            if (sec.Value.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var p in sec.Value.EnumerateObject())
                                    map[p.Name] = p.Value.GetString() ?? "";
                            }
                            AccountsBySector[sec.Name] = map;
                        }
                    }

                    SectorNames = AccountsBySector.Keys.Any()
                        ? AccountsBySector.Keys.OrderBy(x => x).ToList()
                        : new List<string> { "General" };
                }
                else
                {
                    if (root.TryGetProperty("accounts", out var accNode) && accNode.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in accNode.EnumerateObject())
                            GeneralAccounts[p.Name] = p.Value.GetString() ?? "";
                    }
                    SectorNames = new List<string> { "General" };
                }
            }
            catch
            {
                SectorNames = new List<string> { "General" };
            }
        }

        public string? SelectedFor(string rowName, string key)
        {
            if (SplitBySector)
            {
                if (AccountsBySector.TryGetValue(rowName, out var d) && d.TryGetValue(key, out var v))
                    return v;
                return null;
            }
            else
            {
                if (GeneralAccounts.TryGetValue(key, out var v)) return v;
                return null;
            }
        }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            var comp = await _db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
            if (id.HasValue) comp = await _db.Companies.FindAsync(new object[] { id!.Value }, ct);
            if (comp == null) return Redirect("/Payroll/Empresas");

            CompanyId   = comp.Id;
            CompanyName = comp.Name;

            LoadFromPolicy(comp.PayPolicy);

            HasTokens = await _db.PayrollQboTokens.AnyAsync(t => t.CompanyId == CompanyId, ct);
            if (HasTokens)
            {
                try
                {
                    var (realm, access) = await _auth.GetRealmAndValidAccessTokenAsync(CompanyId, ct);
                    RealmId = realm;

                    var accs = await _api.GetExpenseAccountsAsync(realm, access, ct);
                    Accounts = accs?.Select(a => new Opt { Id = a.Id ?? "", Name = a.Name ?? "" }).ToList() ?? new();

                    if (string.IsNullOrWhiteSpace(CompanyName) ||
                        CompanyName.StartsWith("Empresa vinculada ", StringComparison.OrdinalIgnoreCase))
                    {
                        var realName = await _api.GetCompanyNameAsync(realm, access, ct);
                        if (!string.IsNullOrWhiteSpace(realName))
                        {
                            CompanyName = realName;
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
                    _log.LogWarning(ex, "No se pudieron cargar CompanyInfo/cuentas desde QBO.");
                }
            }
            else
            {
                Accounts = new();
            }

            if (SectorNames.Count == 0) SectorNames.Add("General");
            return Page();
        }

        public async Task<IActionResult> OnGetAccountsAsync(CancellationToken ct)
        {
            var comp = await _db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
            if (id.HasValue) comp = await _db.Companies.FindAsync(new object[] { id!.Value }, ct);
            if (comp == null) return new JsonResult(Array.Empty<object>());

            try
            {
                CompanyId = comp.Id;
                var (realm, access) = await _auth.GetRealmAndValidAccessTokenAsync(CompanyId, ct);
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

        public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
        {
            if (!Request.Form.TryGetValue("CompanyId", out var cid) || !int.TryParse(cid.ToString(), out var companyId))
                return BadRequest("companyId inválido.");

            var comp = await _db.Companies.FindAsync(new object[] { companyId }, ct);
            if (comp == null) return NotFound();

            CompanyName = Request.Form["CompanyName"];
            if (!string.IsNullOrWhiteSpace(CompanyName) && !string.Equals(CompanyName, comp.Name, StringComparison.Ordinal))
                comp.Name = CompanyName!;

            SplitBySector = string.Equals(Request.Form["SplitBySector"], "true", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(Request.Form["SplitBySector"], "on",   StringComparison.OrdinalIgnoreCase);

            var sectorIndexToName = new Dictionary<string, string>();
            foreach (var k in Request.Form.Keys.Where(k => k.StartsWith("SectorName_", StringComparison.Ordinal)))
            {
                var idx = k.Substring("SectorName_".Length);
                var name = Request.Form[k].ToString().Trim();
                if (string.IsNullOrWhiteSpace(name)) name = "General";
                sectorIndexToName[idx] = name;
            }

            var keys = new[] { "SalarioBruto", "Extras", "CCSS", "Deducciones", "SalarioNeto" };

            if (SplitBySector)
            {
                var bySector = new Dictionary<string, Dictionary<string, string>>();
                foreach (var pair in sectorIndexToName)
                {
                    var idx  = pair.Key;
                    var name = pair.Value;

                    var map = new Dictionary<string, string>();
                    foreach (var k in keys)
                    {
                        var formKey = $"Map_{idx}_{k}";
                        var val = Request.Form[formKey].ToString();
                        if (!string.IsNullOrWhiteSpace(val)) map[k] = val;
                    }
                    bySector[name] = map;
                }

                var json = JsonSerializer.Serialize(new
                {
                    splitBySector = true,
                    bySector
                });

                comp.PayPolicy = json;
            }
            else
            {
                var map = new Dictionary<string, string>();
                string idx = sectorIndexToName.Keys.OrderBy(x => x).FirstOrDefault() ?? "0";
                foreach (var k in keys)
                {
                    var formKey = $"Map_{idx}_{k}";
                    var val = Request.Form[formKey].ToString();
                    if (!string.IsNullOrWhiteSpace(val)) map[k] = val;
                }

                var json = JsonSerializer.Serialize(new
                {
                    splitBySector = false,
                    accounts = map
                });

                comp.PayPolicy = json;
            }

            await _db.SaveChangesAsync(ct);
            TempData["ok"] = "Parámetros guardados.";
            return Redirect($"/Payroll/Parametrizador/{companyId}");
        }

        public IActionResult OnPostConnect()
        {
            // Respaldo por si quieres gatillar el flujo OAuth desde aquí.
            return Redirect("/Auth/PayrollCallback");
        }
    }
}

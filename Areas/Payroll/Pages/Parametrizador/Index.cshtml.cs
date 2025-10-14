using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

        [BindProperty(SupportsGet = true)]
        public int? id { get; set; }

        public int     CompanyId    { get; set; }
        public string? CompanyName  { get; set; }
        public string? RealmId      { get; set; }
        public bool    HasTokens    { get; set; }

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
                              .FirstOrDefaultAsync(ct);
            if (tk == null) throw new InvalidOperationException("No hay tokens de Payroll para esta empresa.");
            return (tk.RealmId ?? "", tk.AccessToken);
        }

        private async Task<bool> TokensExistAsync(int companyId, CancellationToken ct) =>
            await _db.PayrollQboTokens.AnyAsync(t => t.CompanyId == companyId, ct);

        private void LoadDefaultsFromPolicy(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                ExpenseAccountId = root.TryGetProperty("defaultExpenseAccountId", out var a) ? a.GetString() : null;
                WageItemId       = root.TryGetProperty("defaultWageItemId", out var i) ? i.GetString() : null;
            }
            catch { /* ignore */ }
        }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            // Defensa en producción/Render: asegura que la BD y tablas existan
            try { await _db.Database.MigrateAsync(ct); }
            catch (Exception ex) { _log.LogWarning(ex, "No se pudo migrar/abrir la BD de Payroll en este entorno."); }

            var comp = await _db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
            if (id.HasValue) comp = await _db.Companies.FindAsync(new object[] { id!.Value }, ct);
            if (comp == null) return Redirect("/Payroll/Empresas");

            CompanyId   = comp.Id;
            CompanyName = comp.Name;
            LoadDefaultsFromPolicy(comp.PayPolicy);

            try { HasTokens = await TokensExistAsync(CompanyId, ct); }
            catch (Exception ex) { _log.LogWarning(ex, "Tabla de tokens no disponible; seguimos sin QBO."); HasTokens = false; }

            if (HasTokens)
            {
                try
                {
                    var (realm, access) = await LoadTokensAsync(CompanyId, ct);
                    RealmId = realm;

                    // Listas desde QBO
                    var accs = await _api.GetExpenseAccountsAsync(realm, access, ct);
                    Accounts = accs.Select(a => new Opt { Id = a.Id, Name = a.Name }).ToList();

                    var items = await _api.GetServiceItemsAsync(realm, access, ct);
                    Items = items.Select(i => new Opt { Id = i.Id, Name = i.Name }).ToList();

                    // Refrescar nombre de empresa si está genérico
                    if (string.IsNullOrWhiteSpace(CompanyName) || CompanyName.StartsWith("Empresa vinculada "))
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
                    _log.LogWarning(ex, "No se pudieron obtener listas/CompanyInfo desde QBO.");
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
        {
            if (!Request.Form.TryGetValue("CompanyId", out var cid) || !int.TryParse(cid.ToString(), out var companyId))
                return BadRequest("companyId inválido.");

            var comp = await _db.Companies.FindAsync(new object[] { companyId }, ct);
            if (comp == null) return NotFound();

            var json = JsonSerializer.Serialize(new
            {
                defaultExpenseAccountId = ExpenseAccountId,
                defaultWageItemId       = WageItemId
            });

            comp.PayPolicy = json;
            await _db.SaveChangesAsync(ct);

            TempData["ok"] = "Parámetros guardados.";
            return Redirect($"/Payroll/Parametrizador/{companyId}");
        }

        public async Task<IActionResult> OnPostSyncAsync(CancellationToken ct)
        {
            if (!Request.Form.TryGetValue("CompanyId", out var cid) || !int.TryParse(cid.ToString(), out var companyId))
                return BadRequest("companyId inválido.");

            id = companyId;
            return await OnGetAsync(ct);
        }

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



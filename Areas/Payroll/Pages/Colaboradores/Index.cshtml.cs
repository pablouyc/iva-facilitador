using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IvaFacilitador.Payroll.Services;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;

namespace IvaFacilitador.Areas.Payroll.Pages.Colaboradores
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _log;
        private readonly PayrollDbContext _db;
        private readonly IQboEmployeeService _qbo;

        public IndexModel(ILogger<IndexModel> log, PayrollDbContext db, IQboEmployeeService qbo)
        {
            _log = log;
            _db = db;
            _qbo = qbo;
        }

        // Query
        public int? CompanyId { get; private set; }
        public string Status { get; private set; } = "activos";

        // Empresa / política
        public List<string> SectorNames { get; private set; } = new() { "General" };
        public string Periodo { get; private set; } = "Mensual";

        // Gating (igual que Empresas): tokens QBO + paypolicy parametrizado
        public bool CompanyReady { get; private set; } = false;

        // QBO
        public List<string> QboEmployees { get; private set; } = new();
        public string QboRawJson { get; private set; } = "[]"; // objetos con FullName/GivenName/FamilyName/Email/Phone

        // Tabla actual
        public List<RowVM> Rows { get; private set; } = new();
        public class RowVM
        {
            public int Id { get; set; }
            public string Nombre { get; set; } = "";
            public string Cedula { get; set; } = "";
            public string? Sector { get; set; }
            public string? Cargo { get; set; }
            public decimal? SalarioMensual { get; set; }
            public bool HasCcss { get; set; }
            public bool HasIns { get; set; }
            public string PorcentajePago { get; set; } = "";
            public string Estado { get; set; } = "Activo";
            public DateTime? EndDate { get; set; }
        }

        public async Task OnGetAsync(CancellationToken ct)
        {
            // Params
            int tmp;
            var qsCompany = HttpContext.Request.Query["companyId"].ToString();
            CompanyId = int.TryParse(qsCompany, out tmp) ? tmp : null;
            var qsStatus = (HttpContext.Request.Query["status"].ToString() ?? "").Trim().ToLowerInvariant();
            Status = (qsStatus == "inactivos") ? "inactivos" : "activos";

            // PayPolicy (sectores/periodo)
            string payPolicy = "";
            try
            {
                if (CompanyId.HasValue)
                {
                    var comp = await _db.Companies.FindAsync(new object[] { CompanyId.Value }, ct);
                    payPolicy = comp?.PayPolicy ?? "";
                    if (!string.IsNullOrWhiteSpace(payPolicy))
                    {
                        using var doc = JsonDocument.Parse(payPolicy);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("periodo", out var per) && per.ValueKind == JsonValueKind.String)
                            Periodo = per.GetString() ?? "Mensual";

                        var secs = new List<string>();
                        if (root.TryGetProperty("sectors", out var arr) && arr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var e in arr.EnumerateArray())
                            {
                                var s = e.GetString();
                                if (!string.IsNullOrWhiteSpace(s)) secs.Add(s!);
                            }
                        }
                        if (secs.Count > 0) SectorNames = secs;
                        if (SectorNames.Count == 0) SectorNames = new() { "General" };
                    }
                }
            }
            catch { /* defaults */ }

            // Gating: Tokens QBO + paypolicy válida
            try
            {
                CompanyReady = false;
                if (CompanyId.HasValue)
                {
                    var hasTokens = await _db.PayrollQboTokens.AnyAsync(t => t.CompanyId == CompanyId.Value, ct);
                    CompanyReady = hasTokens && IsParametrized(payPolicy);
                }
            }
            catch { CompanyReady = false; }

            // Lista de empleados QBO (nombres y datos básicos) usando reflexión para no romper compilación
            try
            {
                if (CompanyId.HasValue)
                {
                    var names = new List<string>();
                    var rows = new List<Dictionary<string, string>>();

                    object? ResToObj(Task task)
                    {
                        var pr = task.GetType().GetProperty("Result");
                        return pr?.GetValue(task);
                    }

                    string? TryString(object obj, params string[] props)
                    {
                        foreach (var p in props)
                        {
                            var pi = obj.GetType().GetProperty(p);
                            if (pi == null) continue;
                            var v = pi.GetValue(obj);
                            if (v is string s && !string.IsNullOrWhiteSpace(s)) return s;
                            // nested: PrimaryEmailAddr.Address / PrimaryPhone.FreeFormNumber
                            if (v != null)
                            {
                                var addr = v.GetType().GetProperty("Address")?.GetValue(v) as string;
                                if (!string.IsNullOrWhiteSpace(addr)) return addr;
                                var num = v.GetType().GetProperty("FreeFormNumber")?.GetValue(v) as string;
                                if (!string.IsNullOrWhiteSpace(num)) return num;
                            }
                        }
                        return null;
                    }

                    var candidateNames = new[] { "ListEmployeesAsync", "GetEmployeesAsync", "GetEmployeeNamesAsync", "ListQBEmployeesAsync" };
                    var mi = candidateNames.Select(n => _qbo.GetType().GetMethod(n)).FirstOrDefault(m => m != null);
                    if (mi != null)
                    {
                        var pars = mi.GetParameters();
                        object?[] args =
                            pars.Length == 2 ? new object?[] { CompanyId.Value, ct } :
                            pars.Length == 1 ? new object?[] { CompanyId.Value } :
                            Array.Empty<object?>();

                        var task = mi.Invoke(_qbo, args) as Task;
                        if (task != null)
                        {
                            await task;
                            var result = ResToObj(task);

                            if (result is IEnumerable<string> onlyNames)
                            {
                                names = onlyNames.Where(s => !string.IsNullOrWhiteSpace(s))
                                                 .Select(s => s.Trim())
                                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                                 .OrderBy(s => s).ToList();
                            }
                            else if (result is System.Collections.IEnumerable seq)
                            {
                                foreach (var it in seq)
                                {
                                    string? full = TryString(it, "FullName", "DisplayName", "Name");
                                    string? given = TryString(it, "GivenName", "FirstName");
                                    string? family = TryString(it, "FamilyName", "LastName");
                                    string? email = TryString(it, "PrimaryEmailAddr", "Email", "Mail");
                                    string? phone = TryString(it, "PrimaryPhone", "Phone");

                                    var show = full ?? ((given ?? "").Trim() + " " + (family ?? "").Trim()).Trim();
                                    if (!string.IsNullOrWhiteSpace(show)) names.Add(show);

                                    var row = new Dictionary<string, string>
                                    {
                                        ["FullName"]   = show ?? "",
                                        ["GivenName"]  = given ?? "",
                                        ["FamilyName"] = family ?? "",
                                        ["Email"]      = email ?? "",
                                        ["Phone"]      = phone ?? ""
                                    };
                                    rows.Add(row);
                                }
                                names = names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
                            }

                            QboEmployees = names;
                            QboRawJson = JsonSerializer.Serialize(rows);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "No se pudo obtener la lista de empleados QBO.");
                QboEmployees = new List<string>();
                QboRawJson = "[]";
            }

            // Grilla actual (BD)
            try
            {
                var isActivos = Status == "activos";
                var q = _db.Employees.AsNoTracking().AsQueryable();
                if (CompanyId.HasValue) q = q.Where(e => e.CompanyId == CompanyId.Value);
                q = isActivos ? q.Where(e => e.Status == "Activo") : q.Where(e => e.Status != "Activo");

                var list = await q.OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                    .Select(e => new
                    {
                        e.Id, e.FirstName, e.LastName, e.NationalId,
                        e.BaseSalary, e.HasCcss, e.HasIns,
                        e.PayPct1, e.PayPct2, e.PayPct3, e.PayPct4, e.Status
                    })
                    .ToListAsync(ct);

                Rows = new List<RowVM>(list.Count);
                foreach (var e in list)
                {
                    Rows.Add(new RowVM
                    {
                        Id = e.Id,
                        Nombre = $"{e.FirstName} {e.LastName}".Trim(),
                        Cedula = e.NationalId ?? "",
                        Sector = null,
                        Cargo = null,
                        SalarioMensual = e.BaseSalary,
                        HasCcss = e.HasCcss,
                        HasIns = e.HasIns,
                        PorcentajePago = BuildPctString(e.PayPct1, e.PayPct2, e.PayPct3, e.PayPct4),
                        Estado = e.Status ?? "Activo",
                        EndDate = null
                    });
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[Colaboradores] Error cargando empleados");
                Rows = new List<RowVM>();
            }
        }

        // Respaldo: redirige a handler real de importación si lo necesitas
        public Microsoft.AspNetCore.Mvc.IActionResult OnGetImportQbo(int companyId)
        {
            if (companyId <= 0) return BadRequest();
            TempData["Colab_Import"] = "qbo";
            return RedirectToPage(new { companyId = companyId, status = this.Status });
        }

        private static string BuildPctString(decimal? p1, decimal? p2, decimal? p3, decimal? p4)
        {
            var parts = new List<string>();
            void add(decimal? p){ if (p.HasValue && p.Value > 0) parts.Add(p.Value.ToString(p.Value % 1 == 0 ? "0" : "0.##", CultureInfo.InvariantCulture)); }
            add(p1); add(p2); add(p3); add(p4);
            return parts.Count == 0 ? "" : string.Join("/", parts);
        }

        // ===== Validación de PayPolicy como en Empresas =====
        private static bool IsParametrized(string? payPolicy)
        {
            if (string.IsNullOrWhiteSpace(payPolicy)) return false;
            try
            {
                using var doc = JsonDocument.Parse(payPolicy);
                var root = doc.RootElement;

                if (!(root.TryGetProperty("periodo", out var p) &&
                      p.ValueKind == JsonValueKind.String &&
                      !string.IsNullOrWhiteSpace(p.GetString())))
                    return false;

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

                bool split = false;
                if (root.TryGetProperty("splitBySector", out var sb))
                {
                    split = sb.ValueKind == JsonValueKind.True
                            || (sb.ValueKind == JsonValueKind.String && bool.TryParse(sb.GetString(), out var b) && b);
                }

                if (!root.TryGetProperty("accounts", out var accNode) || accNode.ValueKind != JsonValueKind.Object)
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
            catch { return false; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
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

        // Empresa / política de pago
        public List<string> SectorNames { get; private set; } = new() { "General" };
        public string Periodo { get; private set; } = "Mensual";

        // Estado/gating
        public string CompanyState { get; private set; } = "";
        public bool CompanyReady { get; private set; } = false;

        // QBO
        public bool IsCompanyLinked { get; private set; } = false;
        public List<string> QboEmployees { get; private set; } = new();
        public string QboRawJson { get; private set; } = "[]"; // [{Id,GivenName,FamilyName,DisplayName,Email,Phone,Active}]

        // Tabla
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
            // 1) Params
            int tmp;
            var qsCompany = HttpContext.Request.Query["companyId"].ToString();
            CompanyId = int.TryParse(qsCompany, out tmp) ? tmp : null;

            var qsStatus = (HttpContext.Request.Query["status"].ToString() ?? "").Trim().ToLowerInvariant();
            Status = (qsStatus == "inactivos") ? "inactivos" : "activos";

            // 2) Vinculación QBO
            if (CompanyId.HasValue)
            {
                try { IsCompanyLinked = await _qbo.IsCompanyLinkedAsync(CompanyId.Value, ct); }
                catch { IsCompanyLinked = false; }
            }

            // 3) PayPolicy: periodo + sectores
            string? payPolicy = null;
            try
            {
                if (CompanyId.HasValue)
                {
                    var comp = await _db.Companies.FindAsync(new object[] { CompanyId.Value }, ct);
                    payPolicy = comp?.PayPolicy;
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
            catch { /* defaults ok */ }

            // 4) Estado como en Empresas: tokens + PayPolicy parametrizada => "Listo"
            try
            {
                var hasTokens = CompanyId.HasValue && _db.PayrollQboTokens.Any(t => t.CompanyId == CompanyId.Value);
                var isParam   = IsParametrized(payPolicy, SectorNames);
                CompanyState  = !hasTokens ? "Sin conexión" : (isParam ? "Listo" : "Pendiente");
                CompanyReady  = hasTokens && isParam;
            }
            catch
            {
                CompanyState = "Sin conexión";
                CompanyReady = false;
            }

            // 5) Lista empleados QBO para pestaña y vinculación
            try
            {
                if (CompanyId.HasValue && IsCompanyLinked)
                {
                    var qbo = await _qbo.GetEmployeesAsync(CompanyId.Value, includeInactive: false, ct);
                    QboEmployees = qbo.Select(e => e.DisplayName)
                                      .Where(s => !string.IsNullOrWhiteSpace(s))
                                      .Distinct()
                                      .ToList();
                    QboRawJson   = JsonSerializer.Serialize(qbo);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[Colaboradores] No se pudo recuperar lista QBO.");
                QboEmployees = new();
                QboRawJson   = "[]";
            }

            // 6) Tabla principal
            try
            {
                var isActivos = Status == "activos";
                var q = _db.Employees.AsNoTracking().AsQueryable();

                if (CompanyId.HasValue) q = q.Where(e => e.CompanyId == CompanyId.Value);
                q = isActivos ? q.Where(e => e.Status == "Activo")
                              : q.Where(e => e.Status != "Activo");

                var list = await q.OrderBy(e => e.LastName)
                                  .ThenBy(e => e.FirstName)
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

        public Microsoft.AspNetCore.Mvc.IActionResult OnGetImportQbo(int companyId)
        {
            if (companyId <= 0) return BadRequest();
            TempData["Colab_Import"] = "qbo";
            return RedirectToPage(new { companyId = companyId, status = this.Status });
        }

        private static string BuildPctString(decimal? p1, decimal? p2, decimal? p3, decimal? p4)
        {
            var parts = new List<string>();
            void add(decimal? p)
            {
                if (p.HasValue && p.Value > 0)
                    parts.Add(p.Value.ToString(p.Value % 1 == 0 ? "0" : "0.##", CultureInfo.InvariantCulture));
            }
            add(p1); add(p2); add(p3); add(p4);
            return parts.Count == 0 ? "" : string.Join("/", parts);
        }

        // Misma regla que Empresas: periodo válido, ≥1 sector y cuentas requeridas
        private static bool IsParametrized(string? payPolicy, List<string> sectorNames)
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
                var sectors = sectorNames ?? new List<string>();
                if (sectors.Count == 0) return false;

                // splitBySector
                bool split = false;
                if (root.TryGetProperty("splitBySector", out var sb))
                {
                    split = sb.ValueKind == JsonValueKind.True
                            || (sb.ValueKind == JsonValueKind.String &&
                                bool.TryParse(sb.GetString(), out var b) && b);
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
    }
}

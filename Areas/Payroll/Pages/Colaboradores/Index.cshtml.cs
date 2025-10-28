using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        // Estado real de la empresa
        public string CompanyState { get; private set; } = "";
        public bool CompanyReady { get; private set; } = false;   // Solo TRUE si CompanyState == "Listo"

        // QBO
        public bool IsCompanyLinked { get; private set; } = false;
        public List<string> QboEmployees { get; private set; } = new();

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

            // 2) ¿Vinculada a QBO?
            if (CompanyId.HasValue)
            {
                try { IsCompanyLinked = await _qbo.IsCompanyLinkedAsync(CompanyId.Value, ct); }
                catch { IsCompanyLinked = false; }
            }

            // 3) Cargar PayPolicy (Periodo + Sectores)
            try
            {
                if (CompanyId.HasValue)
                {
                    var comp = await _db.Companies.FindAsync(new object[] { CompanyId.Value }, ct);
                    var json = comp?.PayPolicy;
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("periodo", out var per) && per.ValueKind == System.Text.Json.JsonValueKind.String)
                            Periodo = per.GetString() ?? "Mensual";

                        var secs = new List<string>();
                        if (root.TryGetProperty("sectors", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
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
            catch { /* defaults ya establecidos */ }

            // 4) Estado de la empresa y gating exacto: seleccionado + estado "Listo"
            try
            {
                CompanyReady = false;
                if (CompanyId.HasValue)
                {
                    var comp = await _db.Companies.FindAsync(new object[] { CompanyId.Value }, ct);
                    string? state = null;
                    var t = comp?.GetType();
                    if (t != null)
                    {
                        foreach (var name in new[] { "PayrollStatus", "PayrollState", "Estado", "State", "Status" })
                        {
                            var p = t.GetProperty(name);
                            if (p != null)
                            {
                                var val = p.GetValue(comp);
                                if (val != null) { state = val.ToString(); break; }
                            }
                        }
                    }
                    CompanyState = state ?? "";
                    CompanyReady = !string.IsNullOrWhiteSpace(CompanyState)
                                   && CompanyState.Equals("Listo", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { CompanyReady = false; }

            // 5) (Opcional) Poblado de lista QBO (si existe tabla/servicio); dejamos vacío por ahora
            try
            {
                if (CompanyId.HasValue)
                {
                    // Ejemplo:
                    // QboEmployees = _db.QboEmployees.Where(e => e.CompanyId == CompanyId.Value)
                    //                   .Select(e => e.FullName).ToList();
                }
            }
            catch { /* noop */ }

            // 6) Cargar empleados
            try
            {
                var isActivos = Status == "activos";
                var q = _db.Employees.AsNoTracking().AsQueryable();

                if (CompanyId.HasValue)
                    q = q.Where(e => e.CompanyId == CompanyId.Value);

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

        // GET: Traer de QBO (placeholder; conecta aquí tu flujo real de importación)
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
    }
}


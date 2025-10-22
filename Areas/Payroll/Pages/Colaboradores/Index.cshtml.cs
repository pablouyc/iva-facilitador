using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using IvaFacilitador.Areas.Payroll.ModelosPayroll;
using IvaFacilitador.Areas.Payroll.Services;
using IvaFacilitador.Payroll.Services;

using System.Text;
// Alias para evitar ambigüedad con BaseDatosPayroll.PayPeriod
using PayPeriodModel = IvaFacilitador.Areas.Payroll.ModelosPayroll.PayPeriod;

namespace IvaFacilitador.Areas.Payroll.Pages.Colaboradores
{
    public class IndexModel : PageModel
    {
        [TempData] public string? FlashOk  { get; set; }
        [TempData] public string? FlashErr { get; set; }
        public class Opt { public string Id { get; set; } = ""; public string Name { get; set; } = ""; }

        private readonly PayrollDbContext _db;
        private readonly ICollaboratorsStore _store;
        private readonly IPayrollAuthService _auth;
        private readonly IPayrollQboApi _qbo;

        public IndexModel(PayrollDbContext db, ICollaboratorsStore store, IPayrollAuthService auth, IPayrollQboApi qbo)
        {
            _db = db;
            _store = store;
            _auth = auth;
            _qbo = qbo;
        }

        [BindProperty(SupportsGet = true)]
        public bool ShowInactive { get; set; }

        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = "";
        public List<Collaborator> Rows { get; set; } = new();

        // Lista para pestaña QBO
        public List<Opt> QboEmployees { get; set; } = new();

        // Modelo para pestaña Manual
        public class ManualInput
        {
            [Required] public string Name { get; set; } = "";
            public string? TaxId { get; set; }
            public string? Sector { get; set; }
            public string? Position { get; set; }
            [Range(0, double.MaxValue)] public decimal? MonthlySalary { get; set; }
            [Required] public PayPeriodModel PayPeriod { get; set; } = PayPeriodModel.Mensual;
            [Range(0,100)] public int? Split1 { get; set; }
            [Range(0,100)] public int? Split2 { get; set; }
            [Range(0,100)] public int? Split3 { get; set; }
            [Range(0,100)] public int? Split4 { get; set; }
            public bool UseCCSS { get; set; }
            public bool UseSeguro { get; set; }
        }

        [BindProperty] public ManualInput Manual { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            await _store.EnsureSchemaAsync(_db, ct);

            var company = await _db.Companies
                                   .OrderBy(c => c.Id)
                                   .FirstOrDefaultAsync(ct);
            CompanyId = company?.Id ?? 0;
            CompanyName = company?.Name ?? "";

            Rows = await _store.ListAsync(_db, CompanyId, ShowInactive, ct);

            // Cargar lista QBO (si existen tokens válidos)
            try
            {
                var (realm, access) = await _auth.GetRealmAndValidAccessTokenAsync(CompanyId, ct);
                if (!string.IsNullOrWhiteSpace(realm) && !string.IsNullOrWhiteSpace(access))
                {
                    var emps = await _qbo.GetEmployeesAsync(realm, access, ct);
                    QboEmployees = (emps ?? new()).Select(e => new Opt { Id = e.Id, Name = e.Name }).ToList();
                    Console.WriteLine($"[Colaboradores][QBO] Empleados: {QboEmployees.Count}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Colaboradores][QBO] Error: " + ex.Message);
                QboEmployees = new();
            }

            return Page();
        }

        // ===== Agregar Manual =====
        public async Task<IActionResult> OnPostAddManualAsync([FromForm] int companyId, CancellationToken ct)
        {
            if (!ModelState.IsValid) return await OnGetAsync(ct);

            if (!ValidateSplits(Manual.PayPeriod, Manual.Split1, Manual.Split2, Manual.Split3, Manual.Split4))
            {
                ModelState.AddModelError(string.Empty, "La suma de porcentajes no es válida para el periodo seleccionado.");
                FlashErr = "La suma de porcentajes no es válida para el periodo.";
                return await OnGetAsync(ct);
            }

            // Crear o enlazar en QBO
            try
            {
                var (realm, access) = await _auth.GetRealmAndValidAccessTokenAsync(companyId, ct);
                if (!string.IsNullOrWhiteSpace(realm) && !string.IsNullOrWhiteSpace(access) && !string.IsNullOrWhiteSpace(Manual.Name))
                {
                    var qid = await _qbo.CreateOrLinkEmployeeAsync(realm, access, Manual.Name, null, null, ct);
                    Console.WriteLine($"[Colaboradores][QBO][Manual] Vinculado/creado: {qid ?? "-"} ({Manual.Name})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Colaboradores][QBO][Manual] " + ex.Message);
            }

            var c = new Collaborator
            {
                CompanyId = companyId,
                Name = Manual.Name,
                TaxId = Manual.TaxId,
                Sector = Manual.Sector,
                Position = Manual.Position,
                MonthlySalary = Manual.MonthlySalary,
                PayPeriod = Manual.PayPeriod,
                Split1 = Manual.Split1,
                Split2 = Manual.Split2,
                Split3 = Manual.Split3,
                Split4 = Manual.Split4,
                UseCCSS = Manual.UseCCSS,
                UseSeguro = Manual.UseSeguro,
                Status = 0
            };

            await _store.AddManualAsync(_db, c, ct);
            FlashOk = "Colaborador agregado.";
            return RedirectToPage(new { ShowInactive });
        }

        // ===== Importar desde QBO (solo nombres/ids) =====
        public async Task<IActionResult> OnPostImportQboAsync([FromForm] int companyId, CancellationToken ct)
        {
            await _store.EnsureSchemaAsync(_db, ct);
            try
            {
                var (realm, access) = await _auth.GetRealmAndValidAccessTokenAsync(companyId, ct);
                var emps = await _qbo.GetEmployeesAsync(realm, access, ct);
                var up = await _store.UpsertFromQboAsync(_db, companyId, emps, ct);
                Console.WriteLine($"[Colaboradores][QBO] upsert: {up}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Colaboradores][QBO] Import error: " + ex.Message);
                FlashErr = "Error al importar desde QBO.";
                return RedirectToPage(new { ShowInactive = false });
            }
            FlashOk = "Importación desde QBO completada.";
            return RedirectToPage(new { ShowInactive = false });
        }

        // ===== Importar CSV (plantilla) =====
        [BindProperty] public IFormFile? Csv { get; set; }

        public async Task<IActionResult> OnPostUploadCsvAsync([FromForm] int companyId, CancellationToken ct)
        {
            int okCount = 0, badCount = 0;

            if (Csv == null || Csv.Length == 0)
            {
                FlashErr = "Debe adjuntar un archivo CSV.";
                return RedirectToPage(new { ShowInactive = false });
            }

            using var s = Csv.OpenReadStream();
            using var sr = new StreamReader(s);
            var header = await sr.ReadLineAsync(); // línea de cabecera

            while (!sr.EndOfStream)
            {
                var line = await sr.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var p = line.Split(',');
                try
                {
                    _ = decimal.TryParse(p.ElementAtOrDefault(4), out var sal);
                    _ = Enum.TryParse<PayPeriodModel>(p.ElementAtOrDefault(5) ?? "Mensual", out var per);
                    _ = int.TryParse(p.ElementAtOrDefault(6), out var s1);
                    _ = int.TryParse(p.ElementAtOrDefault(7), out var s2);
                    _ = int.TryParse(p.ElementAtOrDefault(8), out var s3);
                    _ = int.TryParse(p.ElementAtOrDefault(9), out var s4);

                    var c = new Collaborator
                    {
                        CompanyId = companyId,
                        Name = p.ElementAtOrDefault(0) ?? "",
                        TaxId = p.ElementAtOrDefault(1),
                        Sector = p.ElementAtOrDefault(2),
                        Position = p.ElementAtOrDefault(3),
                        MonthlySalary = sal,
                        PayPeriod = per,
                        Split1 = s1,
                        Split2 = s2,
                        Split3 = s3,
                        Split4 = s4,
                        UseCCSS = (p.ElementAtOrDefault(10) ?? "").Equals("1") || (p.ElementAtOrDefault(10) ?? "").Equals("true", StringComparison.OrdinalIgnoreCase),
                        UseSeguro = (p.ElementAtOrDefault(11) ?? "").Equals("1") || (p.ElementAtOrDefault(11) ?? "").Equals("true", StringComparison.OrdinalIgnoreCase),
                        Status = 0
                    };

                    if (!ValidateSplits(c.PayPeriod, c.Split1, c.Split2, c.Split3, c.Split4)) { badCount++; continue; }

                    try
                    {
                        var (realm, access) = await _auth.GetRealmAndValidAccessTokenAsync(companyId, ct);
                        if (!string.IsNullOrWhiteSpace(realm) && !string.IsNullOrWhiteSpace(access) && !string.IsNullOrWhiteSpace(c.Name))
                        {
                            var qid = await _qbo.CreateOrLinkEmployeeAsync(realm, access, c.Name, null, null, ct);
                            Console.WriteLine($"[Colaboradores][QBO][CSV] Vinculado/creado: {qid ?? "-"} ({c.Name})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[Colaboradores][QBO][CSV] " + ex.Message);
                    }

                    await _store.AddManualAsync(_db, c, ct);
                    okCount++;
                }
                catch
                {
                    badCount++;
                }
            }

            FlashOk = $"Importación CSV: {okCount} agregado(s), {badCount} con error.";
            return RedirectToPage(new { ShowInactive = false });
        }

        // ===== Terminar / Reactivar =====
        public async Task<IActionResult> OnPostSetStatusAsync([FromForm] int id, [FromForm] int status, CancellationToken ct)
        {
            await _store.SetStatusAsync(_db, id, status, ct);
            return RedirectToPage(new { ShowInactive = status == 1 });
        }

        // Descargar plantilla CSV
        public IActionResult OnGetDownloadCsvTemplate()
        {
            var header = "Name,TaxId,Sector,Position,MonthlySalary,PayPeriod,Split1,Split2,Split3,Split4,UseCCSS,UseSeguro";
            var example1 = "Ejemplo 1,1-2345-6789,Operaciones,Analista,550000,Mensual,100,0,0,0,1,0";
            var example2 = "Ejemplo 2,2-1111-2222,Ventas,Vendedor,400000,Quincenal,50,50,0,0,1,1";
            var csv = string.Join("\r\n", new[] { header, example1, example2 }) + "\r\n";
            var bytes = Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", "Colaboradores_Template.csv");
        }

        private bool ValidateSplits(PayPeriodModel period, int? s1, int? s2, int? s3, int? s4)
        {
            int v1 = s1 ?? 0, v2 = s2 ?? 0, v3 = s3 ?? 0, v4 = s4 ?? 0;
            if (period == PayPeriodModel.Mensual)   return v1 == 100 && (v2 + v3 + v4) == 0;
            if (period == PayPeriodModel.Quincenal) return (v1 + v2) == 100;
            if (period == PayPeriodModel.Semanal)   return (v1 + v2 + v3 + v4) == 100;
            return true;
        }
    }
}

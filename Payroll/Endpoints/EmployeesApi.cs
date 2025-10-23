using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Payroll.Services;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using IvaFacilitador.Areas.Payroll.ModelosPayroll;

namespace IvaFacilitador.Payroll.Endpoints
{
    // Requests
    public class ManualEmployeeRequest
    {
        public int CompanyId { get; set; }
        public string NationalId { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName  { get; set; } = "";
        public DateTime? JoinDate { get; set; } = null;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public decimal? BaseSalary { get; set; }
        public string? Sector { get; set; }    // nombre del sector
        public string? JobTitle { get; set; }  // cargo
        public bool HasCcss { get; set; }
        public bool HasIns  { get; set; }
        public decimal? PayPct1 { get; set; }
        public decimal? PayPct2 { get; set; }
        public decimal? PayPct3 { get; set; }
        public decimal? PayPct4 { get; set; }
    }

    public class UploadEmployeesRequest
    {
        public int CompanyId { get; set; }
        public List<ManualEmployeeRequest> Rows { get; set; } = new();
    }

    public static class EmployeesApi
    {
        public static void Register(WebApplication app)
        {
            var api = app.MapGroup("/payroll/api");

            // GET /payroll/api/qbo/employees?companyId=1&includeInactive=false
            api.MapGet("/qbo/employees", async (
                int companyId,
                bool? includeInactive,
                IQboEmployeeService qbo,
                CancellationToken ct) =>
            {
                if (companyId <= 0) return Results.BadRequest("companyId requerido.");
                var linked = await qbo.IsCompanyLinkedAsync(companyId, ct);
                if (!linked) return Results.BadRequest("La empresa no está vinculada a QBO.");

                var list = await qbo.GetEmployeesAsync(companyId, includeInactive ?? false, ct);
                return Results.Ok(list);
            });

            // POST /payroll/api/employees/manual  => Persiste en BD
            api.MapPost("/employees/manual", async (
                ManualEmployeeRequest body,
                IQboEmployeeService qbo,
                PayrollDbContext db,
                CancellationToken ct) =>
            {
                if (body == null) return Results.BadRequest("Body vacío.");
                if (body.CompanyId <= 0) return Results.BadRequest("CompanyId requerido.");
                if (string.IsNullOrWhiteSpace(body.NationalId)) return Results.BadRequest("Cédula requerida.");
                if (string.IsNullOrWhiteSpace(body.FirstName) || string.IsNullOrWhiteSpace(body.LastName))
                    return Results.BadRequest("Nombre y Apellido son requeridos.");

                // Validación suma de porcentajes (=100)
                decimal s = (body.PayPct1 ?? 0) + (body.PayPct2 ?? 0) + (body.PayPct3 ?? 0) + (body.PayPct4 ?? 0);
                if (Math.Abs(s - 100m) > 0.001m)
                    return Results.BadRequest("La suma de porcentajes debe ser 100.");

                // Unicidad por (CompanyId, NationalId)
                var exists = await db.Employees.AsNoTracking()
                    .AnyAsync(e => e.CompanyId == body.CompanyId && e.NationalId == body.NationalId, ct);
                if (exists) return Results.Conflict("Ya existe un colaborador con esa cédula en la empresa.");

                // Intentar match/crear en QBO (para enlazar QboEmployeeId)
                var fullName = $"{body.FirstName} {body.LastName}".Trim();
                var match = await qbo.TryMatchAsync(
                    body.CompanyId,
                    new QboMatchQuery(body.NationalId, fullName, body.Email, body.Phone),
                    ct);

                var q = match ?? await qbo.CreateAsync(
                    body.CompanyId,
                    new QboEmployeeCreate(body.FirstName, body.LastName, fullName, body.Email, body.Phone),
                    ct);

                var emp = new Employee
                {
                    CompanyId     = body.CompanyId,
                    NationalId    = body.NationalId,
                    FirstName     = body.FirstName,
                    LastName      = body.LastName,
                    JoinDate      = body.JoinDate?.Date ?? DateTime.UtcNow.Date,
                    Email         = body.Email,
                    Status        = "Activo",
                    BaseSalary    = body.BaseSalary,
                    Sector        = body.Sector,
                    JobTitle      = body.JobTitle,
                    HasCcss       = body.HasCcss,
                    HasIns        = body.HasIns,
                    PayPct1       = body.PayPct1,
                    PayPct2       = body.PayPct2,
                    PayPct3       = body.PayPct3,
                    PayPct4       = body.PayPct4,
                    EndDate       = null,
                    QboEmployeeId = q.Id
                };

                db.Employees.Add(emp);
                await db.SaveChangesAsync(ct);

                return Results.Ok(new {
                    id = emp.Id,
                    qboId = q.Id,
                    displayName = q.DisplayName,
                    message = "Colaborador creado."
                });
            });

            // POST /payroll/api/employees/upload  => Alta masiva básica en BD
            api.MapPost("/employees/upload", async (
                UploadEmployeesRequest body,
                IQboEmployeeService qbo,
                PayrollDbContext db,
                CancellationToken ct) =>
            {
                if (body == null || body.Rows == null) return Results.BadRequest("Body/Rows vacíos.");
                if (body.CompanyId <= 0) return Results.BadRequest("CompanyId requerido.");

                int ok = 0, skip = 0;
                var results = new List<object>();

                foreach (var r in body.Rows)
                {
                    if (string.IsNullOrWhiteSpace(r.NationalId) || string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName))
                    {
                        skip++; results.Add(new { nationalId = r.NationalId, error = "Faltan datos mínimos." }); continue;
                    }

                    bool exists = await db.Employees.AsNoTracking()
                        .AnyAsync(e => e.CompanyId == body.CompanyId && e.NationalId == r.NationalId, ct);
                    if (exists) { skip++; results.Add(new { nationalId = r.NationalId, error = "Duplicado." }); continue; }

                                        // Validación de porcentajes (=100). Default mensual si vienen en blanco (100/0/0/0).
                    decimal sRow = (r.PayPct1 ?? 0) + (r.PayPct2 ?? 0) + (r.PayPct3 ?? 0) + (r.PayPct4 ?? 0);
                    if (sRow == 0m)
                    {
                        r.PayPct1 = 100; r.PayPct2 = r.PayPct3 = r.PayPct4 = 0;
                    }
                    else if (Math.Abs(sRow - 100m) > 0.001m)
                    {
                        skip++;
                        results.Add(new { nationalId = r.NationalId, error = "La suma de % debe ser 100." });
                        continue;
                    }
var full = $"{r.FirstName} {r.LastName}".Trim();
                    var match = await qbo.TryMatchAsync(body.CompanyId, new QboMatchQuery(r.NationalId, full, r.Email, r.Phone), ct);
                    var q = match ?? await qbo.CreateAsync(body.CompanyId, new QboEmployeeCreate(r.FirstName, r.LastName, full, r.Email, r.Phone), ct);

                    var emp = new Employee
                    {
                        CompanyId  = body.CompanyId,
                        NationalId = r.NationalId,
                        FirstName  = r.FirstName,
                        LastName   = r.LastName,
                        JoinDate   = r.JoinDate?.Date ?? DateTime.UtcNow.Date,
                        Email      = r.Email,
                        Status     = "Activo",
                        BaseSalary = r.BaseSalary,
                        Sector     = r.Sector,
                        JobTitle   = r.JobTitle,
                        HasCcss    = r.HasCcss,
                        HasIns     = r.HasIns,
                        PayPct1    = r.PayPct1,
                        PayPct2    = r.PayPct2,
                        PayPct3    = r.PayPct3,
                        PayPct4    = r.PayPct4,
                        QboEmployeeId = q.Id
                    };

                    db.Employees.Add(emp);
                    ok++;
                    results.Add(new { nationalId = r.NationalId, id = emp.Id, qboId = q.Id });
                }

                await db.SaveChangesAsync(ct);
                return Results.Ok(new { processed = ok, skipped = skip, rows = results });
            });
        }
    }
}


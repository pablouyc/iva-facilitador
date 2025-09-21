using System;
using System.Globalization;
using System.IO;
using System.Linq;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using Microsoft.EntityFrameworkCore;

CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("es-CR");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("es-CR");

string BaseDir()
{
    // bin/Debug/net8.0/ -> subir 5 niveles hasta la raíz del proyecto web
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    if (!File.Exists(Path.Combine(root, "IvaFacilitador.csproj")))
    {
        // Fallback: usar el directorio padre x3
        root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    }
    return root;
}

var rootPath = BaseDir();
var dbPath = Path.Combine(rootPath, "payroll.db");
var conn = $"Data Source={dbPath}";

var opt = new DbContextOptionsBuilder<PayrollDbContext>()
    .UseSqlite(conn)
    .Options;

using var db = new PayrollDbContext(opt);

// Asegurar base
db.Database.EnsureCreated();

// ===== Helpers idempotentes =====
T GetOrAdd<T>(Func<T?> finder, Func<T> creator) where T : class
{
    var found = finder();
    if (found != null) return found;
    var created = creator();
    db.Add(created);
    db.SaveChanges();
    return created;
}

// ===== 1) Companies =====
var empA = GetOrAdd(
    () => db.Companies.FirstOrDefault(x => x.Name == "Empresa A (demo)"),
    () => new Company { Name = "Empresa A (demo)", TaxId = "3-101-000111", QboId = "QBO-A", PayPolicy = "Quincenal", ObligationsNotes = "Caja/INS" }
);
var empB = GetOrAdd(
    () => db.Companies.FirstOrDefault(x => x.Name == "Empresa B (demo)"),
    () => new Company { Name = "Empresa B (demo)", TaxId = "3-101-000222", QboId = "QBO-B", PayPolicy = "Mensual", ObligationsNotes = "Caja/INS" }
);

// ===== 2) PayItems =====
PayItem EnsureItem(string code, string name, string type, bool recurring=false)
{
    return GetOrAdd(
        () => db.PayItems.FirstOrDefault(i => i.Code == code),
        () => new PayItem { Code = code, Name = name, Type = type, IsRecurring = recurring }
    );
}
var itSalario  = EnsureItem("SALARIO",  "Salario base",    "earning",  true);
var itHextra   = EnsureItem("HEXTRA",   "Horas extra",      "earning");
var itDeduRent = EnsureItem("DEDRENTA", "Deducción renta", "deduction");
var itPatron   = EnsureItem("CCSS",     "Aporte patronal",  "employer", true);

// ===== 3) Employees =====
Employee EnsureEmp(Company c, string nid, string fn, string ln, DateTime join, string? email, decimal baseSalary)
{
    return GetOrAdd(
        () => db.Employees.FirstOrDefault(e => e.CompanyId == c.Id && e.NationalId == nid),
        () => new Employee {
            CompanyId = c.Id,
            NationalId = nid,
            FirstName = fn,
            LastName = ln,
            JoinDate = join,
            Email = email,
            Status = "Activo",
            BaseSalary = baseSalary
        }
    );
}

// A
var a1 = EnsureEmp(empA, "1-1111-1111", "María",  "Pérez",  new DateTime(2023,5,2),  "maria@a.demo", 500000);
var a2 = EnsureEmp(empA, "2-2222-2222", "Carlos", "Gómez",  new DateTime(2022,9,1),  "carlos@a.demo", 520000);
var a3 = EnsureEmp(empA, "3-3333-3333", "Ana",    "Torres", new DateTime(2024,1,15), "ana@a.demo",   480000);

// B
var b1 = EnsureEmp(empB, "4-4444-4444", "José",   "López",  new DateTime(2021,3,10), "jose@b.demo",  600000);
var b2 = EnsureEmp(empB, "5-5555-5555", "Luisa",  "Mora",   new DateTime(2020,7,22), "luisa@b.demo", 590000);
var b3 = EnsureEmp(empB, "6-6666-6666", "Diego",  "Vargas", new DateTime(2019,11,5), "diego@b.demo", 610000);

// ===== 4) PayPeriods (septiembre 2025) =====
PayPeriod EnsurePeriod(Company c, DateTime start, DateTime end, string status)
{
    return GetOrAdd(
        () => db.PayPeriods.FirstOrDefault(p => p.CompanyId == c.Id && p.StartDate == start && p.EndDate == end),
        () => new PayPeriod { CompanyId = c.Id, StartDate = start, EndDate = end, Status = status }
    );
}
var pA1 = EnsurePeriod(empA, new DateTime(2025,9,1),  new DateTime(2025,9,15), "Draft");
var pA2 = EnsurePeriod(empA, new DateTime(2025,9,16), new DateTime(2025,9,30), "Draft");
var pB1 = EnsurePeriod(empB, new DateTime(2025,9,1),  new DateTime(2025,9,30), "Draft");

// ===== 5) PayEvents =====
PayEvent EnsureEvent(int companyId, int? empId, PayItem item, DateTime date, decimal amt, string? note=null)
{
    return GetOrAdd(
        () => db.PayEvents.FirstOrDefault(e => e.CompanyId == companyId && e.EmployeeId == empId && e.ItemId == item.Id && e.Date == date && e.Amount == amt),
        () => new PayEvent { CompanyId = companyId, EmployeeId = empId, ItemId = item.Id, Date = date, Amount = amt, Note = note }
    );
}

var today = new DateTime(2025,9,20);

// Empresa A
EnsureEvent(empA.Id, a1.Id, itSalario,  new DateTime(2025,9,15), 250000, "Pago quincena");
EnsureEvent(empA.Id, a2.Id, itSalario,  new DateTime(2025,9,15), 260000, "Pago quincena");
EnsureEvent(empA.Id, a3.Id, itSalario,  new DateTime(2025,9,15), 240000, "Pago quincena");
EnsureEvent(empA.Id, a2.Id, itHextra,   new DateTime(2025,9,18),  15000, "Horas extra nocturnas");
EnsureEvent(empA.Id, a1.Id, itDeduRent, new DateTime(2025,9,18), -12000, "Anticipo impuesto");

// Empresa B
EnsureEvent(empB.Id, b1.Id, itSalario,  new DateTime(2025,9,30), 600000, "Pago mensual");
EnsureEvent(empB.Id, b2.Id, itSalario,  new DateTime(2025,9,30), 590000, "Pago mensual");
EnsureEvent(empB.Id, b3.Id, itSalario,  new DateTime(2025,9,30), 610000, "Pago mensual");
EnsureEvent(empB.Id, null,  itPatron,   new DateTime(2025,9,25),  85000, "Reporte patronales CCSS");

Console.WriteLine("✅ Seeding terminado.");
Console.WriteLine($"Empresas: {db.Companies.Count()}");
Console.WriteLine($"Colaboradores: {db.Employees.Count()}");
Console.WriteLine($"Items: {db.PayItems.Count()}");
Console.WriteLine($"Eventos: {db.PayEvents.Count()}");
Console.WriteLine($"Períodos: {db.PayPeriods.Count()}");

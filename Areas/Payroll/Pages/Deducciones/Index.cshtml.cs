using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IvaFacilitador.Areas.Payroll.Pages.Deducciones
{
    public class IndexModel : PageModel
    {
        private readonly PayrollDbContext _db;
        public IndexModel(PayrollDbContext db) { _db = db; }

        public string CompanyIdRaw { get; private set; } = "";
        public int? CompanyId { get; private set; }
        public DateTime From { get; private set; }
        public DateTime To { get; private set; }
        public string PeriodoLabel => $"{From:yyyy-MM-dd} — {To:yyyy-MM-dd}";
        public string QueryRaw { get; private set; } = "";
        public bool IsApproved { get; private set; }

        public List<SelectListItem> Employees { get; private set; } = new();
        public List<SelectListItem> Items { get; private set; } = new();
        public List<RowVM> Rows { get; private set; } = new();

        public class RowVM
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public int EmployeeId { get; set; }
            public string Colaborador { get; set; } = "";
            public string NationalId { get; set; } = "";
            public string Cargo { get; set; } = "";
            public string Sector { get; set; } = "";
            public decimal SalarioMensual { get; set; }
            public decimal SalarioQuincena { get; set; }
            public decimal Amount { get; set; }
            public string AmountInvariant => Amount.ToString(CultureInfo.InvariantCulture);
            public int ItemId { get; set; }
            public string ItemName { get; set; } = "";
            public string? NotePlain { get; set; }
        }

        public class AddEditInput
        {
            public int? Id { get; set; }
            [Required] public DateTime Date { get; set; }
            [Required] public int EmployeeId { get; set; }
            [Required] public int ItemId { get; set; }
            [Required] public decimal Amount { get; set; }
            [MaxLength(500)] public string? Note { get; set; }
            public string? ReturnToQuery { get; set; }
        }

        [BindProperty] public AddEditInput Input { get; set; } = new();

        public async Task OnGet()
        {
            ParseQuery();
            try
            {
                await EnsureSeedItemsAsync();
                await LoadListsAsync();
                await LoadRowsAsync();
            }
            catch
            {
                Rows ??= new List<IndexModel.RowVM>();
                Employees ??= new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
                Items ??= new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
            }}

        public async Task<IActionResult> OnPostAdd()
        {
            ParseQuery();
            if(!ModelState.IsValid){ await LoadListsAsync(); await LoadRowsAsync(); return Page(); }
            var ev = new PayEvent{
                CompanyId = CompanyId ?? 0,
                EmployeeId = Input.EmployeeId,
                ItemId = Input.ItemId,
                Date = Input.Date.Date,
                Amount = -Math.Abs(Input.Amount), // deducciones almacenadas negativas
                Note = (Input.Note ?? "").Trim()
            };
            _db.PayEvents.Add(ev);
            await _db.SaveChangesAsync();
            return Redirect(Ret());
        }

        public async Task<IActionResult> OnPostEdit()
        {
            ParseQuery();
            if(!ModelState.IsValid || Input.Id==null){ await LoadListsAsync(); await LoadRowsAsync(); return Page(); }
            var ev = await _db.PayEvents.FirstOrDefaultAsync(e => e.Id==Input.Id);
            if(ev==null) return Redirect(Ret());
            ev.EmployeeId = Input.EmployeeId;
            ev.ItemId = Input.ItemId;
            ev.Date = Input.Date.Date;
            ev.Amount = -Math.Abs(Input.Amount);
            ev.Note = (Input.Note ?? "").Trim();
            await _db.SaveChangesAsync();
            return Redirect(Ret());
        }

        public async Task<IActionResult> OnPostDelete()
        {
            ParseQuery();
            if(Input.Id!=null){
                var ev = await _db.PayEvents.FirstOrDefaultAsync(e => e.Id==Input.Id);
                if(ev!=null){ _db.PayEvents.Remove(ev); await _db.SaveChangesAsync(); }
            }
            return Redirect(Ret());
        }

        private string Ret() => string.IsNullOrWhiteSpace(Input.ReturnToQuery) ? Request.Path + Request.QueryString : Request.Path + "?" + Input.ReturnToQuery!.TrimStart('?');

        private void ParseQuery()
        {
            QueryRaw = Request.QueryString.HasValue ? Request.QueryString.Value! : "";
            CompanyIdRaw = (Request.Query["companyId"].ToString() ?? "").Trim();
            if(int.TryParse(CompanyIdRaw, out var cid)) CompanyId = cid;
            DateTime from,to;
            if(DateTime.TryParse(Request.Query["from"], out from) && DateTime.TryParse(Request.Query["to"], out to))
            { From = from.Date; To = to.Date; if(From>To){ var t=From; From=To; To=t; } }
            else
            { var today = DateTime.Today; From=new DateTime(today.Year,today.Month,1); To=new DateTime(today.Year,today.Month,Math.Min(15, DateTime.DaysInMonth(today.Year,today.Month))); }
            var s = (Request.Query["status"].ToString() ?? "").Trim().ToLowerInvariant();
            IsApproved = s.StartsWith("aprob");
        }

        private async Task LoadListsAsync()
        {
            var qEmp = _db.Employees.AsQueryable();
            if(CompanyId!=null) qEmp = qEmp.Where(e => e.CompanyId==CompanyId.Value);
            Employees = await qEmp.OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
              .Select(e => new SelectListItem{ Value=e.Id.ToString(), Text=e.FirstName + " " + e.LastName + " (" + e.NationalId + ")" })
              .ToListAsync();

            Items = await _db.PayItems.Where(p => p.Type=="Deduction")
              .OrderBy(p => p.Name)
              .Select(p => new SelectListItem{ Value=p.Id.ToString(), Text=p.Name })
              .ToListAsync();
        }

        private async Task LoadRowsAsync()
        {
            var q = _db.PayEvents
                .Include(e => e.Item)
                .Include(e => e.Employee)
                .Where(e => (e.Item != null && e.Item.Type=="Deduction") && e.Date>=From && e.Date<=To);
            if(CompanyId!=null) q = q.Where(e => e.CompanyId==CompanyId.Value);

            var list = await q.OrderByDescending(e => e.Date).ThenBy(e => e.Id).ToListAsync();

            Rows = list.Select(e => new RowVM{
                Id = e.Id,
                Date = e.Date.Date,
                EmployeeId = e.EmployeeId ?? 0,
                Colaborador = (e.Employee==null) ? "—" : (e.Employee.FirstName + " " + e.Employee.LastName),
                NationalId = e.Employee?.NationalId ?? "—",
                Cargo = "",
                Sector = "",
                SalarioMensual = e.Employee?.BaseSalary ?? 0m,
                SalarioQuincena = Math.Round((e.Employee?.BaseSalary ?? 0m)/2m, 2),
                Amount = Math.Abs(e.Amount), // mostrar en positivo
                ItemId = e.ItemId,
                ItemName = e.Item?.Name ?? "—",
                NotePlain = e.Note
            }).ToList();
        }

        private async Task EnsureSeedItemsAsync()
        {
            var seeds = new (string Code, string Name)[]
            {
                ("DED_AUSENCIA","Ausencia injustificada"),
                ("DED_RETENCION","Retención judicial"),
                ("DED_ADELANTO","Adelanto de salario"),
                ("DED_MULTA","Multa"),
                ("DED_VIATICOS","Viáticos/Alimentación (descuento)"),
                ("DED_CCSS","Rebajo CCSS"),
                ("DED_RENTA","Rebajo Renta"),
                ("DED_OTROS","Otros")
            };
            var existing = await _db.PayItems.Where(p => p.Type=="Deduction").ToListAsync();
            var codes = existing.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach(var s in seeds)
            {
                if(!codes.Contains(s.Code))
                {
                    _db.PayItems.Add(new PayItem{ Code=s.Code, Name=s.Name, Type="Deduction", IsRecurring=false });
                }
            }
            if(_db.ChangeTracker.HasChanges()) await _db.SaveChangesAsync();
        }

        public string Money(decimal value) =>
            string.Format(CultureInfo.GetCultureInfo("es-CR"), "{0:C}", value);
    }
}








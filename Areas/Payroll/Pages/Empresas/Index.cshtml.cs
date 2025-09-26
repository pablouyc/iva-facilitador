using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;

namespace IvaFacilitador.Areas.Payroll.Pages.Empresas
{
    public class IndexModel : PageModel
    {
        private readonly PayrollDbContext _db;

        public IndexModel(PayrollDbContext db) => _db = db;

        public List<RowVM> Rows { get; set; } = new List<RowVM>();

        public class RowVM
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string? TaxId { get; set; }
            public string? QboId { get; set; }
            public bool QboConnected => !string.IsNullOrWhiteSpace(QboId);
        }

        public async Task OnGet()
        {
            // Mostrar por defecto SOLO las conectadas a QBO
            Rows = await _db.Companies
                .Where(c => !string.IsNullOrEmpty(c.QboId))
                .OrderBy(c => c.Name)
                .Select(c => new RowVM
                {
                    Id = c.Id,
                    Name = c.Name,
                    TaxId = c.TaxId,
                    QboId = c.QboId
                })
                .ToListAsync();
        }

        // Agregar -> crea placeholder -> redirige a OAuth de QBO
        public async Task<IActionResult> OnPostAdd()
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
            var company = new Company
            {
                Name = $"Nueva empresa {stamp}",
                TaxId = null,
                QboId = null
            };
            _db.Companies.Add(company);
            await _db.SaveChangesAsync();

            var url = $"/Auth/ConnectQbo?companyId=" + company.Id + "&returnTo=/Payroll/Empresas/Config/" + company.Id;
            return Redirect(url);
        }

        // /Empresas?handler=Disconnect&id=123
        public async Task<IActionResult> OnPostDisconnect(int id)
        {
            var company = await _db.Companies.FindAsync(id);
            if (company != null)
            {
                company.QboId = null; // desligar QBO
                await _db.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}


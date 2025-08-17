using IvaFacilitador.Data;
using IvaFacilitador.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace IvaFacilitador.Pages.Empresas;

[Authorize]
public class DetalleModel : PageModel
{
    private readonly AppDbContext _db;

    public DetalleModel(AppDbContext db)
    {
        _db = db;
    }

    public Empresa Empresa { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var emp = await _db.Empresas.Include(e => e.ConexionQbo).FirstOrDefaultAsync(e => e.Id == id);
        if (emp == null)
        {
            return RedirectToPage("/Inicio/Index");
        }
        Empresa = emp;
        return Page();
    }
}

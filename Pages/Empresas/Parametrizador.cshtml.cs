using IvaFacilitador.Data;
using IvaFacilitador.Models;
using IvaFacilitador.Models.Dto;
using IvaFacilitador.Services;
using IvaFacilitador.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IvaFacilitador.Pages.Empresas;

[Authorize]
public class ParametrizadorModel : PageModel
{
    private readonly ISessionPendingCompanyService _session;
    private readonly ICryptoProtector _crypto;
    private readonly AppDbContext _db;

    public ParametrizadorModel(ISessionPendingCompanyService session, ICryptoProtector crypto, AppDbContext db)
    {
        _session = session;
        _crypto = crypto;
        _db = db;
    }

    [BindProperty]
    public ParametrizacionEmpresaDto Input { get; set; } = new();

    public string CompanyName { get; set; } = string.Empty;
    public string RealmId { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        var pc = _session.Get();
        if (pc is null)
        {
            TempData["Warn"] = "No hay conexión preliminar.";
            return RedirectToPage("/Empresas/Nueva");
        }
        CompanyName = pc.CompanyName;
        RealmId = pc.RealmId;
        return Page();
    }

    public async Task<IActionResult> OnPostListoAsync()
    {
        if (!ModelState.IsValid)
        {
            var pc = _session.Get();
            if (pc != null)
            {
                CompanyName = pc.CompanyName;
                RealmId = pc.RealmId;
            }
            return Page();
        }

        var pending = _session.Get();
        if (pending is null)
        {
            TempData["Warn"] = "No hay conexión preliminar.";
            return RedirectToPage("/Empresas/Nueva");
        }

        var empresa = new Empresa
        {
            CompanyName = pending.CompanyName,
            RealmId = pending.RealmId,
            Moneda = Input.Moneda,
            Pais = Input.Pais,
            PeriodoIva = Input.PeriodoIva,
            PorcentajeIvaDefault = Input.PorcentajeIvaDefault,
            MetodoRedondeo = Input.MetodoRedondeo,
            ConexionQbo = new ConexionQbo
            {
                AccessTokenEnc = _crypto.Protect(pending.AccessToken),
                RefreshTokenEnc = _crypto.Protect(pending.RefreshToken),
                ExpiresAtUtc = pending.ExpiresAtUtc
            }
        };
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();
        _session.Clear();
        TempData["Ok"] = "Empresa creada";
        return RedirectToPage("/Empresas/Detalle", new { id = empresa.Id });
    }

    public IActionResult OnPostCancelar()
    {
        _session.Clear();
        TempData["Info"] = "Operación cancelada";
        return RedirectToPage("/Inicio/Index");
    }
}

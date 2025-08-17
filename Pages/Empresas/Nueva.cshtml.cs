using IvaFacilitador.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IvaFacilitador.Pages.Empresas;

[Authorize]
public class NuevaModel : PageModel
{
    private readonly IQboAuthService _qboAuth;

    public NuevaModel(IQboAuthService qboAuth)
    {
        _qboAuth = qboAuth;
    }

    public void OnGet()
    {
    }

    public IActionResult OnPostConectarPreliminar()
    {
        var url = _qboAuth.GetAuthorizationUrl(Url.Page("/Empresas/Parametrizador"));
        return Redirect(url);
    }
}

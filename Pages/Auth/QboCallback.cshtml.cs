using IvaFacilitador.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IvaFacilitador.Pages.Auth;

[AllowAnonymous]
public class QboCallbackModel : PageModel
{
    private readonly IQboAuthService _auth;
    private readonly ISessionPendingCompanyService _session;

    public QboCallbackModel(IQboAuthService auth, ISessionPendingCompanyService session)
    {
        _auth = auth;
        _session = session;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var pc = await _auth.HandleCallbackAsync(Request);
        _session.Save(pc);
        return RedirectToPage("/Empresas/Parametrizador");
    }
}

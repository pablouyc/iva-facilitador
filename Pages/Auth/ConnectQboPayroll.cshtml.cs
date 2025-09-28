using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using IvaFacilitador.Payroll.Services;

namespace IvaFacilitador.Pages.Auth
{
    [AllowAnonymous]
    public class ConnectQboPayrollModel : PageModel
    {
        private readonly IPayrollAuthService _auth;
        public ConnectQboPayrollModel(IPayrollAuthService auth) => _auth = auth;

        public IActionResult OnGet(int companyId, string returnTo = "/Payroll/Empresas")
        {
            var url = _auth.GetAuthorizeUrl(companyId, returnTo);
            return Redirect(url);
        }
    }
}

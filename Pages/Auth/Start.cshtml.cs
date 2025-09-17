using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.RazorPages; using IvaFacilitador.Services;
namespace IvaFacilitador.Pages.Auth{
public class StartModel:PageModel{
    private readonly IQuickBooksAuth _auth;
    public StartModel(IQuickBooksAuth auth){ _auth=auth; }
    public IActionResult OnGet(){ var url=_auth.BuildAuthorizeUrl(Guid.NewGuid().ToString("N")); return Redirect(url); }
}}

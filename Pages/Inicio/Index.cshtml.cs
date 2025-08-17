using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IvaFacilitador.Pages.Inicio;

[Authorize]
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}

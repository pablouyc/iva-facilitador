using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace IvaFacilitador.Pages.Auth
{
    public class LoginModel : PageModel
    {
        [BindProperty] public string? InputUser { get; set; }
        [BindProperty] public string? InputPassword { get; set; }
        public string? Error { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = (InputUser ?? "").Trim();
            var pass = InputPassword ?? "";

            // Único usuario solicitado
            if (string.Equals(user, "administrador", StringComparison.OrdinalIgnoreCase)
                && pass == "pruebasuyc123:)")
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, "administrador"),
                    new Claim(ClaimTypes.Role, "Admin")
                };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                return RedirectToPage("/Index");
            }

            Error = "Credenciales inválidas.";
            return Page();
        }
    }
}

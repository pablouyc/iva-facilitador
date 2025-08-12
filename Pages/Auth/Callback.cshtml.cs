using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using IvaFacilitador.Services;
using IvaFacilitador.Models;

namespace IvaFacilitador.Pages.Auth
{
    public class CallbackModel : PageModel
    {
        private readonly IQuickBooksAuth _auth;
        private readonly ITokenStore _tokenStore;
        private readonly ICompanyStore _companyStore;

        public CallbackModel(
            IQuickBooksAuth auth,
            ITokenStore tokenStore,
            ICompanyStore companyStore
        )
        {
            _auth = auth;
            _tokenStore = tokenStore;
            _companyStore = companyStore;
        }

        [BindProperty(SupportsGet = true)] public string? code { get; set; }
        [BindProperty(SupportsGet = true)] public string? realmId { get; set; }
        [BindProperty(SupportsGet = true)] public string? state { get; set; }
        [BindProperty(SupportsGet = true)] public string? error { get; set; }
        [BindProperty(SupportsGet = true)] public string? error_description { get; set; }

        public string? Error { get; set; }
        public string? RealmId { get; set; }

        public async Task OnGet()
        {
            if (!string.IsNullOrEmpty(error))
            {
                Error = $"{error}: {error_description}";
                return;
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(realmId))
            {
                Error = "No se recibieron los parámetros necesarios desde Intuit.";
                return;
            }

            var result = await _auth.TryExchangeCodeForTokenAsync(code!);
            if (!result.ok || result.token == null)
            {
                Error = $"No se pudo intercambiar el código por el token. {result.error}";
                return;
            }

            _tokenStore.Save(realmId!, result.token);
            _companyStore.AddOrUpdateCompany(new CompanyConnection
            {
                RealmId = realmId!,
                Name = $"Empresa {realmId}"
            });

            RealmId = realmId;
        }
    }
}

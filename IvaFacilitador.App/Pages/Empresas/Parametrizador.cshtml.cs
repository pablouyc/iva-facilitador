using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using IvaFacilitador.App.Services;

namespace IvaFacilitador.App.Pages.Empresas
{
    public class ParametrizadorModel : PageModel
    {
        private readonly ICompanyMemoryStore _store;
        public ParametrizadorModel(ICompanyMemoryStore store) { _store = store; }

        [BindProperty(SupportsGet = true)] public string? RealmId { get; set; }
        [BindProperty(SupportsGet = true)] public string? Name { get; set; }
        public bool NotFoundView { get; set; }

        public IActionResult OnGet()
        {
            if (string.IsNullOrWhiteSpace(RealmId))
            {
                NotFoundView = true;
                return Page();
            }

            // Si viene nombre en query, registrar carga preliminar
            if (!string.IsNullOrWhiteSpace(Name))
            {
                _store.SetPrelim(new CompanyInfo { RealmId = RealmId!, Name = Name! });
            }

            // Si no hay nombre en query, intenta leer preliminar
            var prelim = _store.GetPrelim(RealmId!);
            if (prelim != null && string.IsNullOrWhiteSpace(Name))
            {
                Name = prelim.Name;
            }

            return Page();
        }

        public IActionResult OnPostGuardar()
        {
            if (string.IsNullOrWhiteSpace(RealmId))
                return Redirect("/");

            // Tomar preliminar si existe; si no, usar datos del form
            var prelim = _store.GetPrelim(RealmId!) ?? new CompanyInfo
            {
                RealmId = RealmId!,
                Name = Name ?? "Empresa"
            };

            // Guardar definitivo y limpiar preliminar
            _store.Save(prelim);
            _store.ClearPrelim(RealmId!);

            return Redirect("/");
        }

        public IActionResult OnPostCancelar()
        {
            if (string.IsNullOrWhiteSpace(RealmId))
                return Redirect("/");

            // Cancelar: borrar preliminar y definitivo
            _store.ClearPrelim(RealmId!);
            _store.Delete(RealmId!);

            return Redirect("/");
        }
    }
}

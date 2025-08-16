using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using IvaFacilitador.Models;
using IvaFacilitador.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IvaFacilitador.Pages.IVA
{
    public class ParametrizacionModel : PageModel
    {
        private readonly ParametrizacionRepository _repo;

        public ParametrizacionModel(ParametrizacionRepository repo)
        {
            _repo = repo;
        }

        [BindProperty]
        public List<TarifaUsada> Tarifas { get; set; } = new();

        [BindProperty]
        public List<string> Seleccionadas { get; set; } = new();

        [BindProperty]
        public string? TarifasJson { get; set; }

        public void OnGet()
        {
            // Las tarifas deben asignarse antes de renderizar la página.
        }

        public async Task<IActionResult> OnPostGuardar()
        {
            if (!string.IsNullOrWhiteSpace(TarifasJson))
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                Tarifas = JsonSerializer.Deserialize<List<TarifaUsada>>(TarifasJson, opts) ?? new List<TarifaUsada>();
            }

            var seleccion = Tarifas
                .Where(t => Seleccionadas.Contains($"{t.TaxCodeId}|{t.TaxRateId}"))
                .Select(t => new TarifaSeleccionada
                {
                    TaxCodeId = t.TaxCodeId,
                    TaxCodeName = t.TaxCodeName,
                    TaxRateId = t.TaxRateId,
                    TaxRateName = t.TaxRateName,
                    Porcentaje = t.Porcentaje,
                    CountTransacciones = t.CountTransacciones,
                    SumBase = t.SumBase,
                    SumImpuesto = t.SumImpuesto,
                    PrimerUso = t.PrimerUso,
                    UltimoUso = t.UltimoUso
                }).ToList();

            var config = new ParametrizacionEmpresa();
            config.IvaVentas.TarifasSeleccionadas = seleccion;

            var realmId = Request.Form["RealmId"].FirstOrDefault() ?? Request.Cookies["realmId"];
            if (!string.IsNullOrWhiteSpace(realmId))
            {
                var json = JsonSerializer.Serialize(config);
                await _repo.SaveAsync(realmId!, json);
                TempData["Toast"] = "Tarifa(s) de IVA de ventas guardada(s)";
            }
            else
            {
                TempData["Toast"] = "No se pudo determinar la empresa";
            }
            return RedirectToPage();
        }
    }
}

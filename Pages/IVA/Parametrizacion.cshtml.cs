using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using IvaFacilitador.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IvaFacilitador.Pages.IVA
{
    public class ParametrizacionModel : PageModel
    {
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

        public IActionResult OnPostGuardar()
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

            var config = ParametrizacionEmpresa.CargarParametrizacion() ?? new ParametrizacionEmpresa();
            config.IvaVentas.TarifasSeleccionadas = seleccion;
            config.GuardarParametrizacion();

            TempData["Toast"] = "Tarifa(s) de IVA de ventas guardada(s)";
            return RedirectToPage();
        }
    }
}


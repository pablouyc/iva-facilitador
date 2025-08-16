using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace IvaFacilitador.Models
{
    public class ParametrizacionEmpresa
    {
        public IvaVentas IvaVentas { get; set; } = new();

        public void GuardarParametrizacion(string filePath = "parametrizacion.json")
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json);
        }

        public static ParametrizacionEmpresa? CargarParametrizacion(string filePath = "parametrizacion.json")
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ParametrizacionEmpresa>(json);
        }
    }

    public class IvaVentas
    {
        public DateTime FechaDeteccion { get; set; }
        public int PeriodoAnalizadoMeses { get; set; }
        public string? Fuente { get; set; }
        public List<TarifaSeleccionada> TarifasSeleccionadas { get; set; } = new();
    }

    public class TarifaSeleccionada
    {
        public string? TaxCodeId { get; set; }
        public string? TaxCodeName { get; set; }
        public string? TaxRateId { get; set; }
        public string? TaxRateName { get; set; }
        public decimal Porcentaje { get; set; }
        public string? AgencyRef { get; set; }
        public int CountTransacciones { get; set; }
        public decimal SumBase { get; set; }
        public decimal SumImpuesto { get; set; }
        public DateTime? PrimerUso { get; set; }
        public DateTime? UltimoUso { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IvaFacilitador.Areas.Payroll.Pages.PlanillaTotal
{
    public class IndexModel : PageModel
    {
        [BindProperty(SupportsGet = true)] public string? companyId { get; set; }
        [BindProperty(SupportsGet = true)] public string? m { get; set; }    // yyyy-MM
        [BindProperty(SupportsGet = true)] public string? q { get; set; }    // 1 | 2
        [BindProperty(SupportsGet = true)] public string? from { get; set; } // yyyy-MM-dd
        [BindProperty(SupportsGet = true)] public string? to { get; set; }   // yyyy-MM-dd

        public DateTime FromDate { get; private set; }
        public DateTime ToDate   { get; private set; }
        public string PeriodoLabel { get; private set; } = string.Empty;

        public List<RowVM> Rows { get; } = new();

        public class ItemVM { public string Name { get; set; } = ""; public decimal Amount { get; set; } }

        public class RowVM
        {
            public string Colaborador { get; set; } = "";
            public string Identificacion { get; set; } = "";
            public string Cargo { get; set; } = "";
            public string Sector { get; set; } = "";
            public decimal SalarioMensual { get; set; }
            public decimal SalarioQuincena => Math.Round(SalarioMensual / 2m, 2);

            public List<ItemVM> ExtrasDetalle { get; } = new();
            public List<ItemVM> DeduccionesDetalle { get; } = new();

            public decimal Extras => ExtrasDetalle.Sum(i => i.Amount);
            public decimal Deducciones => DeduccionesDetalle.Sum(i => i.Amount);
            public decimal Bruto => SalarioQuincena + Extras;
            public decimal Neto  => Bruto - Deducciones;
        }

        public string Money(decimal n)
        {
            var cr = CultureInfo.GetCultureInfo("es-CR");
            return n.ToString("C0", cr);
        }

        public void OnGet()
        {
            var tz = TryFindCRTimeZone();
            var todayCR = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

            var firstOfMonth = new DateTime(todayCR.Year, todayCR.Month, 1);
            if (!string.IsNullOrWhiteSpace(m) &&
                DateTime.TryParseExact(m, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var mdt))
            {
                firstOfMonth = new DateTime(mdt.Year, mdt.Month, 1);
            }

            var dim = DateTime.DaysInMonth(firstOfMonth.Year, firstOfMonth.Month);

            if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to) &&
                DateTime.TryParse(from, out var fd) && DateTime.TryParse(to, out var td))
            {
                if (fd > td) { var t = fd; fd = td; td = t; }
                FromDate = fd; ToDate = td;
            }
            else
            {
                var qInt = 0;
                int.TryParse(q, out qInt);
                if (qInt == 0) qInt = (todayCR.Day <= 15) ? 1 : 2;

                if (qInt == 1)
                {
                    FromDate = firstOfMonth;
                    ToDate = new DateTime(firstOfMonth.Year, firstOfMonth.Month, Math.Min(15, dim));
                }
                else
                {
                    FromDate = new DateTime(firstOfMonth.Year, firstOfMonth.Month, 16);
                    ToDate = new DateTime(firstOfMonth.Year, firstOfMonth.Month, dim);
                }
            }

            PeriodoLabel = $"{FromDate:dd MMM yyyy} – {ToDate:dd MMM yyyy}".ToLower(new CultureInfo("es-CR"));

            SeedMock();
        }

        private static TimeZoneInfo TryFindCRTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica"); }
            catch
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time"); }
                catch { return TimeZoneInfo.Local; }
            }
        }

        private void SeedMock()
        {
            var baseRows = new[]
            {
                new {N="Ana Rodríguez",   ID="1-1010-1001", Cargo="Analista",      Sector="Operaciones",       Mensual=650000m},
                new {N="Carlos Pérez",    ID="1-1212-2020", Cargo="Gerente",       Sector="Gerencia",          Mensual=1500000m},
                new {N="María Gómez",     ID="2-3030-3030", Cargo="Contadora",     Sector="Administración",    Mensual=900000m},
                new {N="José Ramírez",    ID="1-4040-4040", Cargo="Soporte TI",    Sector="Tecnología",        Mensual=800000m},
                new {N="Luis Hernández",  ID="1-5050-5050", Cargo="Ventas Sr.",    Sector="Comercial",         Mensual=700000m},
                new {N="Sofía Martínez",  ID="1-6060-6060", Cargo="HR Partner",    Sector="Recursos Humanos",  Mensual=820000m},
            };

            int i = 0;
            foreach (var r in baseRows)
            {
                var row = new RowVM
                {
                    Colaborador = r.N,
                    Identificacion = r.ID,
                    Cargo = r.Cargo,
                    Sector = r.Sector,
                    SalarioMensual = r.Mensual
                };

                // Desgloses determinísticos por fila
                if (i % 2 == 0)
                {
                    row.ExtrasDetalle.Add(new ItemVM { Name = "Horas extra", Amount = 25000m });
                    row.ExtrasDetalle.Add(new ItemVM { Name = "Bono productividad", Amount = 15000m });
                    row.DeduccionesDetalle.Add(new ItemVM { Name = "CCSS", Amount = Math.Round(row.SalarioQuincena * 0.0967m, 0) });
                    row.DeduccionesDetalle.Add(new ItemVM { Name = "Renta", Amount = 10000m });
                }
                else
                {
                    row.ExtrasDetalle.Add(new ItemVM { Name = "Comisiones", Amount = 30000m });
                    row.DeduccionesDetalle.Add(new ItemVM { Name = "CCSS", Amount = Math.Round(row.SalarioQuincena * 0.0967m, 0) });
                    row.DeduccionesDetalle.Add(new ItemVM { Name = "Embargo judicial", Amount = 8000m });
                }

                Rows.Add(row);
                i++;
            }
        }
    }
}

using System.Text.Json;

namespace IvaFacilitador.Areas.Payroll.Api
{
    public static class PayrollApi
    {
        public record PayrollEvent(
            string EmpresaId,
            DateTime Fecha,
            string Titulo,
            string Categoria,
            decimal? Monto
        );

        public static void MapPayrollApi(this WebApplication app)
        {
            app.MapGet("/api/payroll/events", (HttpContext ctx) =>
            {
                string empresa = ctx.Request.Query["empresa"];
                DateTime? desde = TryDate(ctx.Request.Query["desde"]);
                DateTime? hasta = TryDate(ctx.Request.Query["hasta"]);

                var root = app.Environment.ContentRootPath;
                var file = Path.Combine(root, "Areas", "Payroll", "base-datos", "payroll", "eventos.json");

                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                List<PayrollEvent> all = new();
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    all = JsonSerializer.Deserialize<List<PayrollEvent>>(json, opts) ?? new();
                }

                IEnumerable<PayrollEvent> q = all;
                if (!string.IsNullOrWhiteSpace(empresa))
                    q = q.Where(e => string.Equals(e.EmpresaId, empresa, StringComparison.OrdinalIgnoreCase));
                if (desde.HasValue) q = q.Where(e => e.Fecha.Date >= desde.Value.Date);
                if (hasta.HasValue) q = q.Where(e => e.Fecha.Date <= hasta.Value.Date);

                return Results.Ok(q.OrderBy(e => e.Fecha).ToList());
            });
        }

        private static DateTime? TryDate(string? s)
        {
            if (DateTime.TryParse(s, out var d)) return d;
            return null;
        }
    }
}

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaFacilitador.Services
{
    public interface IQuickBooksApi
    {
        Task<string?> GetCompanyNameAsync(string realmId, string accessToken, CancellationToken ct = default);
        Task<List<string>> DetectSalesTariffsAsync(string realmId, string accessToken, DateTime from, DateTime to, CancellationToken ct = default);
        Task<List<string>> ListAvailableSalesTaxLabelsAsync(string realmId, string accessToken, CancellationToken ct = default);
    }

    public class QuickBooksApi : IQuickBooksApi
    {
        private readonly IHttpClientFactory _http;
        private readonly IOptions<IntuitOAuthSettings> _settings;
        private readonly ILogger<QuickBooksApi> _logger;

        public QuickBooksApi(IHttpClientFactory http, IOptions<IntuitOAuthSettings> settings, ILogger<QuickBooksApi> logger)
        {
            _http = http;
            _settings = settings;
            _logger = logger;
        }

        public async Task<string?> GetCompanyNameAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            var host = (_settings.Value.Environment ?? "production").Equals("sandbox", StringComparison.OrdinalIgnoreCase)
                ? "https://sandbox-quickbooks.api.intuit.com"
                : "https://quickbooks.api.intuit.com";

            var direct = await GetCompanyNameFromCompanyInfoAsync(host, realmId, accessToken, ct);
            if (!string.IsNullOrEmpty(direct)) return direct;

            var viaQuery = await GetCompanyNameViaQueryAsync(host, realmId, accessToken, ct);
            if (!string.IsNullOrEmpty(viaQuery)) return viaQuery;

            _logger.LogWarning("[QBO] No se logró obtener CompanyName por ninguna vía para realmId={RealmId}", realmId);
            return null;
        }

        private async Task<string?> GetCompanyNameFromCompanyInfoAsync(string host, string realmId, string accessToken, CancellationToken ct)
        {
            var url = $"{host}/v3/company/{realmId}/companyinfo/{realmId}?minorversion=70";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var client = _http.CreateClient();
                var resp = await client.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[QBO] companyinfo fallo HTTP {Code}. Body: {Body}", (int)resp.StatusCode, body);
                    return null;
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("CompanyInfo", out var ci)
                    && ci.TryGetProperty("CompanyName", out var nameProp)
                    && nameProp.ValueKind == JsonValueKind.String)
                {
                    var name = nameProp.GetString();
                    _logger.LogInformation("[QBO] CompanyInfo.CompanyName obtenido: {Name}", name);
                    return name;
                }

                _logger.LogWarning("[QBO] companyinfo sin CompanyName. Body: {Body}", body);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QBO] Error en companyinfo");
                return null;
            }
        }

        private async Task<string?> GetCompanyNameViaQueryAsync(string host, string realmId, string accessToken, CancellationToken ct)
        {
            var query = "select * from CompanyInfo";
            var url = $"{host}/v3/company/{realmId}/query?query={Uri.EscapeDataString(query)}&minorversion=70";

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var client = _http.CreateClient();
                var resp = await client.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[QBO] query CompanyInfo fallo HTTP {Code}. Body: {Body}", (int)resp.StatusCode, body);
                    return null;
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("QueryResponse", out var qr)
                    && qr.TryGetProperty("CompanyInfo", out var arr)
                    && arr.ValueKind == JsonValueKind.Array
                    && arr.GetArrayLength() > 0)
                {
                    var first = arr[0];
                    if (first.TryGetProperty("CompanyName", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                    {
                        var name = nameProp.GetString();
                        _logger.LogInformation("[QBO] Query CompanyInfo.CompanyName obtenido: {Name}", name);
                        return name;
                    }
                }

                _logger.LogWarning("[QBO] query CompanyInfo sin CompanyName. Body: {Body}", body);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QBO] Error en query CompanyInfo");
                return null;
            }
        }

        // === Implementación requerida por IQuickBooksApi ===
        public async Task<List<string>> DetectSalesTariffsAsync(string realmId, string accessToken, DateTime from, DateTime to, CancellationToken ct = default)
        {
            var host = (_settings.Value.Environment ?? "production").Equals("sandbox", StringComparison.OrdinalIgnoreCase)
                ? "https://sandbox-quickbooks.api.intuit.com"
                : "https://quickbooks.api.intuit.com";

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1) Mapa de TaxRate (Id -> (Name, Rate))
            var rateMap = await GetTaxRateMapAsync(host, realmId, accessToken, ct);

            // 2) Recolectar de Invoice y SalesReceipt
            await CollectSalesTariffsFromEntityAsync(host, realmId, accessToken, "Invoice", from, to, rateMap, set, ct);
            await CollectSalesTariffsFromEntityAsync(host, realmId, accessToken, "SalesReceipt", from, to, rateMap, set, ct);

            // 3) Orden sugerido
            var ordered = new List<string>();
            string[] pref = new[] { "Tarifa general 13", "Tarifa reducida 4", "Tarifa reducida 2", "Tarifa reducida 1", "0%/Exento/No sujeto" };
            foreach (var p in pref) if (set.Contains(p)) ordered.Add(p);
            foreach (var rest in set.Except(ordered, StringComparer.OrdinalIgnoreCase)) ordered.Add(rest);

            _logger.LogInformation("[QBO] DetectSalesTariffsAsync → {Labels}", string.Join(", ", ordered));
            return ordered;
        }

        public async Task<List<string>> ListAvailableSalesTaxLabelsAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            var host = (_settings.Value.Environment ?? "production").Equals("sandbox", StringComparison.OrdinalIgnoreCase)
                ? "https://sandbox-quickbooks.api.intuit.com"
                : "https://quickbooks.api.intuit.com";

            var query = "select * from TaxRate";
            var url = $"{host}/v3/company/{realmId}/query?query={Uri.EscapeDataString(query)}&minorversion=70";

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var client = _http.CreateClient();
                var resp = await client.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[QBO] TaxRate query HTTP {Code}. Body: {Body}", (int)resp.StatusCode, body);
                    return new List<string>();
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (root.TryGetProperty("QueryResponse", out var qr) &&
                    qr.TryGetProperty("TaxRate", out var arr) &&
                    arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tr in arr.EnumerateArray())
                    {
                        string? name = null;
                        decimal? rate = null;

                        if (tr.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String)
                            name = n.GetString();

                        if (tr.TryGetProperty("RateValue", out var rv) &&
                            rv.ValueKind == JsonValueKind.Number &&
                            rv.TryGetDecimal(out var d))
                            rate = d;

                        var canon = CanonicalizeSalesTax(rate, name);
                        if (!string.IsNullOrWhiteSpace(canon))
                            set.Add(canon);
                    }
                }

                var ordered = new List<string>();
                string[] pref = new[] { "Tarifa general 13", "Tarifa reducida 4", "Tarifa reducida 2", "Tarifa reducida 1", "0%/Exento/No sujeto" };
                foreach (var p in pref) if (set.Contains(p)) ordered.Add(p);
                foreach (var rest in set.Except(ordered, StringComparer.OrdinalIgnoreCase)) ordered.Add(rest);

                _logger.LogInformation("[QBO] TaxRate → disponibles (canónicas): {Labels}", string.Join(", ", ordered));
                return ordered;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QBO] Error consultando TaxRate");
                return new List<string>();
            }
        }

        // === Helpers internos ===

        // 1) Mapa TaxRateId -> (Name, Rate)
        private async Task<Dictionary<string, (string Name, decimal Rate)>> GetTaxRateMapAsync(
            string host, string realmId, string accessToken, CancellationToken ct)
        {
            var map = new Dictionary<string, (string, decimal)>(StringComparer.OrdinalIgnoreCase);
            var q = "select Id, Name, RateValue from TaxRate";
            var url = $"{host}/v3/company/{realmId}/query?query={Uri.EscapeDataString(q)}&minorversion=70";

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var client = _http.CreateClient();
                var resp = await client.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[QBO] TaxRate map HTTP {Code}. Body: {Body}", (int)resp.StatusCode, body);
                    return map;
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("QueryResponse", out var qr) &&
                    qr.TryGetProperty("TaxRate", out var arr) &&
                    arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tr in arr.EnumerateArray())
                    {
                        string? id = null;
                        string? name = null;
                        decimal rate = 0m;

                        if (tr.TryGetProperty("Id", out var idp) && idp.ValueKind == JsonValueKind.String) id = idp.GetString();
                        if (tr.TryGetProperty("Name", out var np) && np.ValueKind == JsonValueKind.String) name = np.GetString();
                        if (tr.TryGetProperty("RateValue", out var rv) && rv.ValueKind == JsonValueKind.Number && rv.TryGetDecimal(out var d)) rate = d;

                        if (!string.IsNullOrWhiteSpace(id))
                            map[id!] = (name ?? string.Empty, rate);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QBO] Error construyendo TaxRate map");
            }

            return map;
        }

        // 2) Recolecta tasas canónicas usadas en un tipo de entidad
        private async Task CollectSalesTariffsFromEntityAsync(
            string host,
            string realmId,
            string accessToken,
            string entity, // "Invoice" o "SalesReceipt"
            DateTime from,
            DateTime to,
            Dictionary<string, (string Name, decimal Rate)> rateMap,
            HashSet<string> set,
            CancellationToken ct)
        {
            var f = from.ToString("yyyy-MM-dd");
            var t = to.ToString("yyyy-MM-dd");

            var query = $"select Id, TxnDate, TxnTaxDetail, Line from {entity} where TxnDate >= '{f}' and TxnDate <= '{t}'";
            var url = $"{host}/v3/company/{realmId}/query?query={Uri.EscapeDataString(query)}&minorversion=70";

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var client = _http.CreateClient();
                var resp = await client.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[QBO] {Entity} query HTTP {Code}. Body: {Body}", entity, (int)resp.StatusCode, body);
                    return;
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (!root.TryGetProperty("QueryResponse", out var qr)) return;
                if (!qr.TryGetProperty(entity, out var arr) || arr.ValueKind != JsonValueKind.Array) return;

                foreach (var tx in arr.EnumerateArray())
                {
                    // Porcentaje desde TxnTaxDetail.TaxLine[].TaxLineDetail
                    if (tx.TryGetProperty("TxnTaxDetail", out var ttd) &&
                        ttd.ValueKind == JsonValueKind.Object &&
                        ttd.TryGetProperty("TaxLine", out var taxLines) &&
                        taxLines.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tl in taxLines.EnumerateArray())
                        {
                            if (tl.TryGetProperty("TaxLineDetail", out var tld) &&
                                tld.ValueKind == JsonValueKind.Object)
                            {
                                // Prefiere TaxRateRef -> id -> busca en rateMap
                                if (tld.TryGetProperty("TaxRateRef", out var trr) &&
                                    trr.ValueKind == JsonValueKind.Object &&
                                    trr.TryGetProperty("value", out var val) &&
                                    val.ValueKind == JsonValueKind.String)
                                {
                                    var id = val.GetString();
                                    if (!string.IsNullOrWhiteSpace(id) && rateMap.TryGetValue(id!, out var info))
                                    {
                                        var canon = CanonicalizeSalesTax(info.Rate, info.Name);
                                        if (!string.IsNullOrWhiteSpace(canon)) set.Add(canon);
                                    }
                                }
                                // Fallback: porcentaje directo
                                else if (tld.TryGetProperty("TaxPercent", out var tp) &&
                                         tp.ValueKind == JsonValueKind.Number &&
                                         tp.TryGetDecimal(out var pct))
                                {
                                    var canon = CanonicalizeSalesTax(pct, null);
                                    if (!string.IsNullOrWhiteSpace(canon)) set.Add(canon);
                                }
                            }
                        }
                    }

                    // También intenta por líneas -> Line[].TaxCodeRef.name
                    if (tx.TryGetProperty("Line", out var lines) && lines.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ln in lines.EnumerateArray())
                        {
                            if (ln.TryGetProperty("TaxCodeRef", out var tcr) &&
                                tcr.ValueKind == JsonValueKind.Object &&
                                tcr.TryGetProperty("name", out var nc) &&
                                nc.ValueKind == JsonValueKind.String)
                            {
                                var (label, percent) = NormalizeSalesTaxLabel(nc.GetString() ?? "");
                                var canon = CanonicalizeSalesTax(percent, label);
                                if (!string.IsNullOrWhiteSpace(canon)) set.Add(canon);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QBO] Error leyendo {Entity}", entity);
            }
        }

        // 3) Homologación a etiquetas canónicas (solo dentro del sistema)
        private string CanonicalizeSalesTax(decimal? percent, string? name)
        {
            var n = (name ?? string.Empty).Trim().ToLowerInvariant();

            // Palabras clave de 0%
            if (n.Contains("exen") || n.Contains("no sujeto") || n.Contains("0%"))
                return "0%/Exento/No sujeto";

            if (percent.HasValue)
            {
                var p = percent.Value;
                if (p >= 12.5m && p <= 13.5m) return "Tarifa general 13";
                if (p >= 3.5m && p <= 4.5m) return "Tarifa reducida 4";
                if (p >= 1.5m && p <= 2.5m) return "Tarifa reducida 2";
                if (p >= 0.5m && p <= 1.5m) return "Tarifa reducida 1";
                if (p == 0m) return "0%/Exento/No sujeto";
            }

            // Si no hay percent claro, intentar por nombre con dígitos
            if (n.Contains("13")) return "Tarifa general 13";
            if (n.Contains("4")) return "Tarifa reducida 4";
            if (n.Contains("2")) return "Tarifa reducida 2";
            if (n.Contains("1")) return "Tarifa reducida 1";

            return "0%/Exento/No sujeto";
        }

        // 4) Normalizador auxiliar para nombres de TaxCodeRef (interno)
        private (string Label, decimal Percent) NormalizeSalesTaxLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return ("0%/Exenta", 0m);
            var n = name.Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ");

            // exento / no sujeto / 0
            if (n.Contains("exen") || n.Contains("no sujeto") || n.Contains("0%") || n == "0" || n.Contains(" 0 "))
                return ("0%/Exenta", 0m);

            // 13 primero (para no confundir con "1")
            if (n.Contains("13")) return ("Tarifa general 13", 13m);

            // 4, 2, 1 (reducidas)
            if (n.Contains("4")) return ("Tarifa reducida 4", 4m);
            if (n.Contains("2")) return ("Tarifa reducida 2", 2m);
            if (n.Contains("1")) return ("Tarifa reducida 1", 1m);

            // fallback: exenta
            return ("0%/Exenta", 0m);
        }
    }
}

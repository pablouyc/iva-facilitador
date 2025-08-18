using System;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        Task<List<string>> ListAvailableSalesTaxLabelsAsync(string realmId, string accessToken, CancellationToken ct = default);}

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
            if (!string.IsNullOrWhiteSpace(direct)) return direct;

            var viaQuery = await GetCompanyNameViaQueryAsync(host, realmId, accessToken, ct);
            if (!string.IsNullOrWhiteSpace(viaQuery)) return viaQuery;

            _logger.LogWarning("[QBO] No se logrÃ³ obtener CompanyName por ninguna vÃ­a para realmId={RealmId}", realmId);
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

    public async Task<List<string>> DetectSalesTariffsAsync(string realmId, string accessToken, DateTime from, DateTime to, CancellationToken ct = default)
        {
            // TODO: Implementar lectura de Invoice y SalesReceipt (últimos 6 meses) y normalización de tasas
            await Task.CompletedTask;
            return new List<string>();
        }

        public async Task<List<string>> ListAvailableSalesTaxLabelsAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            // TODO: Consultar TaxCodes/TaxRates (catálogo) para poblar el selector
            await Task.CompletedTask;
            return new List<string> { "13%", "4%", "2%", "1% (MEIC/CBT)", "0%/Exenta" };
        }
    }
}





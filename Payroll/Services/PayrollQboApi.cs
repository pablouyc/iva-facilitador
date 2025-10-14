using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IvaFacilitador.Payroll.Services
{
    public record QboAccount(string Id, string Name, string? AccountType, string? AccountSubType);
    public record QboItem(string Id, string Name);

    public interface IPayrollQboApi
    {
        Task<List<QboAccount>> GetExpenseAccountsAsync(string realmId, string accessToken, CancellationToken ct = default);
        Task<List<QboItem>>    GetServiceItemsAsync(string realmId, string accessToken, CancellationToken ct = default);
    }

    public class PayrollQboApi : IPayrollQboApi
    {
        private readonly IHttpClientFactory _http;

        public PayrollQboApi(IHttpClientFactory http) => _http = http;

        private HttpClient Client(string accessToken)
        {
            var c = _http.CreateClient("intuit"); // Configurado en Program.cs con AddHttpClient
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return c;
        }

        private async Task<JsonDocument> QueryAsync(string realmId, string accessToken, string sql, CancellationToken ct)
        {
            var url = $"https://quickbooks.api.intuit.com/v3/company/{realmId}/query?minorversion=65";
            using var body = new StringContent(sql, Encoding.UTF8, "application/text");
            var resp = await Client(accessToken).PostAsync(url, body, ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);
            return JsonDocument.Parse(json);
        }

        public async Task<List<QboAccount>> GetExpenseAccountsAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            var sql = "select Id, Name, AccountType, AccountSubType from Account " +
                      "where Active = true and (AccountType = 'Expense' or AccountType = 'OtherExpense' or AccountType = 'CostOfGoodsSold')";

            using var doc = await QueryAsync(realmId, accessToken, sql, ct);
            var list = new List<QboAccount>();

            if (doc.RootElement.TryGetProperty("QueryResponse", out var qr) &&
                qr.TryGetProperty("Account", out var arr))
            {
                foreach (var a in arr.EnumerateArray())
                {
                    var id   = a.GetProperty("Id").GetString() ?? "";
                    var name = a.GetProperty("Name").GetString() ?? id;
                    var type = a.TryGetProperty("AccountType", out var t) ? t.GetString() : null;
                    var sub  = a.TryGetProperty("AccountSubType", out var s) ? s.GetString() : null;
                    list.Add(new QboAccount(id, name, type, sub));
                }
            }
            return list;
        }

        public async Task<List<QboItem>> GetServiceItemsAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            var sql = "select Id, Name, Type from Item where Active = true and Type = 'Service'";
            using var doc = await QueryAsync(realmId, accessToken, sql, ct);
            var list = new List<QboItem>();

            if (doc.RootElement.TryGetProperty("QueryResponse", out var qr) &&
                qr.TryGetProperty("Item", out var arr))
            {
                foreach (var it in arr.EnumerateArray())
                {
                    var id   = it.GetProperty("Id").GetString() ?? "";
                    var name = it.GetProperty("Name").GetString() ?? id;
                    list.Add(new QboItem(id, name));
                }
            }
            return list;
        }
    }
}

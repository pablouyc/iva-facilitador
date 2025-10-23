using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using IvaFacilitador.Areas.Payroll.ModelosPayroll;

namespace IvaFacilitador.Payroll.Services
{
    // === DTOs ===
    public record QboEmployeeDto(
        string Id,
        string? GivenName,
        string? FamilyName,
        string DisplayName,
        string? Email,
        string? Phone,
        bool Active
    );

    public record QboMatchQuery(
        string? NationalId,
        string FullName,
        string? Email,
        string? Phone
    );

    public record QboEmployeeCreate(
        string GivenName,
        string FamilyName,
        string DisplayName,
        string? Email,
        string? Phone
    );

    // === Interfaz ===
    public interface IQboEmployeeService
    {
        Task<bool> IsCompanyLinkedAsync(int companyId, CancellationToken ct = default);
        Task<IReadOnlyList<QboEmployeeDto>> GetEmployeesAsync(int companyId, bool includeInactive, CancellationToken ct = default);
        Task<QboEmployeeDto?> TryMatchAsync(int companyId, QboMatchQuery query, CancellationToken ct = default);
        Task<QboEmployeeDto> CreateAsync(int companyId, QboEmployeeCreate cmd, CancellationToken ct = default);
    }

    // === Implementación ===
    public class QboEmployeeService : IQboEmployeeService
    {
        private readonly PayrollDbContext _db;
        private readonly ILogger<QboEmployeeService> _log;
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _cfg;

        public QboEmployeeService(PayrollDbContext db, ILogger<QboEmployeeService> log, IHttpClientFactory http, IConfiguration cfg)
        {
            _db = db;
            _log = log;
            _http = http;
            _cfg = cfg;
        }

        public async Task<bool> IsCompanyLinkedAsync(int companyId, CancellationToken ct = default)
        {
            try
            {
                return await _db.Set<PayrollQboToken>().AnyAsync(t => t.CompanyId == companyId, ct);
            }
            catch
            {
                return false;
            }
        }

        public async Task<IReadOnlyList<QboEmployeeDto>> GetEmployeesAsync(int companyId, bool includeInactive, CancellationToken ct = default)
        {
            var tok = await _db.Set<PayrollQboToken>()
                .Where(t => t.CompanyId == companyId)
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync(ct);

            if (tok == null || string.IsNullOrWhiteSpace(tok.AccessToken) || string.IsNullOrWhiteSpace(tok.RealmId))
                return Array.Empty<QboEmployeeDto>();

            string host = ((_cfg["IntuitPayrollAuth:Environment"] ?? _cfg["IntuitPayrollAuth__Environment"] ?? "production")
                            .Equals("sandbox", StringComparison.OrdinalIgnoreCase))
                            ? "https://sandbox-quickbooks.api.intuit.com"
                            : "https://quickbooks.api.intuit.com";

            string url = $"{host}/v3/company/{tok.RealmId}/query?minorversion=65";
            string where = includeInactive ? "" : " where Active = true";
            string query = $"select Id, GivenName, FamilyName, DisplayName, PrimaryEmailAddr, PrimaryPhone, Active from Employee{where} order by DisplayName";
            using var content = new StringContent(query, Encoding.UTF8, "application/text");

            var client = _http.CreateClient("intuit");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tok.AccessToken);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var resp = await client.PostAsync(url, content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("[QBO][Employee query] HTTP {Code} {Reason}. Body: {Body}", (int)resp.StatusCode, resp.ReasonPhrase, body);
                return Array.Empty<QboEmployeeDto>();
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (!(root.TryGetProperty("QueryResponse", out var qr)
                    && qr.TryGetProperty("Employee", out var arr)
                    && arr.ValueKind == JsonValueKind.Array))
                    return Array.Empty<QboEmployeeDto>();

                var list = new List<QboEmployeeDto>(arr.GetArrayLength());
                foreach (var el in arr.EnumerateArray())
                {
                    string id = el.TryGetProperty("Id", out var jId) ? jId.GetString() ?? "" : "";
                    string disp = el.TryGetProperty("DisplayName", out var jdn) ? jdn.GetString() ?? "" : "";
                    string? given = el.TryGetProperty("GivenName", out var jgn) ? jgn.GetString() : null;
                    string? family = el.TryGetProperty("FamilyName", out var jfn) ? jfn.GetString() : null;

                    string? email = null;
                    if (el.TryGetProperty("PrimaryEmailAddr", out var em) && em.ValueKind == JsonValueKind.Object)
                    {
                        if (em.TryGetProperty("Address", out var addr) && addr.ValueKind == JsonValueKind.String)
                            email = addr.GetString();
                    }

                    string? phone = null;
                    if (el.TryGetProperty("PrimaryPhone", out var ph) && ph.ValueKind == JsonValueKind.Object)
                    {
                        if (ph.TryGetProperty("FreeFormNumber", out var num) && num.ValueKind == JsonValueKind.String)
                            phone = num.GetString();
                    }

                    bool active = el.TryGetProperty("Active", out var ja) && ja.ValueKind == JsonValueKind.True
                               || (ja.ValueKind == JsonValueKind.String && string.Equals(ja.GetString(), "true", StringComparison.OrdinalIgnoreCase));

                    list.Add(new QboEmployeeDto(id, given, family, disp, email, phone, active));
                }

                return list;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[QBO] Error parseando respuesta de Employee.");
                return Array.Empty<QboEmployeeDto>();
            }
        }

        public async Task<QboEmployeeDto?> TryMatchAsync(int companyId, QboMatchQuery query, CancellationToken ct = default)
        {
            var all = await GetEmployeesAsync(companyId, includeInactive: true, ct);
            if (all.Count == 0) return null;

            string norm(string? s) => (s ?? "").Trim().ToLowerInvariant();
            string normPhone(string? s) => new string((s ?? "").Where(char.IsDigit).ToArray());

            var qEmail = norm(query.Email);
            var qPhone = normPhone(query.Phone);
            var qName  = norm(query.FullName);

            // 1) Por email exacto
            if (!string.IsNullOrWhiteSpace(qEmail))
            {
                var byEmail = all.FirstOrDefault(e => norm(e.Email) == qEmail);
                if (byEmail != null) return byEmail;
            }

            // 2) Por teléfono normalizado
            if (!string.IsNullOrWhiteSpace(qPhone))
            {
                var byPhone = all.FirstOrDefault(e => normPhone(e.Phone) == qPhone);
                if (byPhone != null) return byPhone;
            }

            // 3) Por DisplayName ~ nombre completo
            if (!string.IsNullOrWhiteSpace(qName))
            {
                var byName = all.FirstOrDefault(e => norm(e.DisplayName) == qName);
                if (byName != null) return byName;

                // fallback más suave: comienza con
                byName = all.FirstOrDefault(e => norm(e.DisplayName).StartsWith(qName, StringComparison.Ordinal));
                if (byName != null) return byName;
            }

            return null;
        }

        public Task<QboEmployeeDto> CreateAsync(int companyId, QboEmployeeCreate cmd, CancellationToken ct = default)
        {
            // Stub: simulación de alta en QBO
            var id = Guid.NewGuid().ToString("N");
            var dto = new QboEmployeeDto(
                id,
                cmd.GivenName,
                cmd.FamilyName,
                cmd.DisplayName,
                cmd.Email,
                cmd.Phone,
                Active: true
            );
            return Task.FromResult(dto);
        }
    }
}

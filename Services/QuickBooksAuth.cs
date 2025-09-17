using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaFacilitador.Services
{
    public interface IQuickBooksAuth
    {
        string BuildAuthorizeUrl(string state = "xyz");

        // Devuelve (ok, token, error legible) y deja logs en el servidor
        Task<(bool ok, TokenResponse? token, string? error)> TryExchangeCodeForTokenAsync(
            string code,
            CancellationToken ct = default
        );

        // Envoltorio de compatibilidad (devuelve null si falla)
        Task<TokenResponse?> ExchangeCodeForTokenAsync(string code, CancellationToken ct = default);

        // NUEVO: revocar refresh_token en Intuit
        Task<(bool ok, string? error)> TryRevokeRefreshTokenAsync(
            string refreshToken,
            CancellationToken ct = default
        );
    }

    public class QuickBooksAuth : IQuickBooksAuth
    {
        private readonly IntuitOAuthSettings _settings;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<QuickBooksAuth> _logger;

        public QuickBooksAuth(
            IOptions<IntuitOAuthSettings> options,
            IHttpClientFactory httpFactory,
            ILogger<QuickBooksAuth> logger
        )
        {
            _settings = options.Value;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        public string BuildAuthorizeUrl(string state = "xyz")
        {
            var scope = Uri.EscapeDataString(_settings.Scopes);
            var redirect = Uri.EscapeDataString(_settings.RedirectUri);
            return $"https://appcenter.intuit.com/connect/oauth2" +
                   $"?client_id={_settings.ClientId}" +
                   $"&response_type=code" +
                   $"&scope={scope}" +
                   $"&redirect_uri={redirect}" +
                   $"&state={state}" +
                   $"&prompt=consent";
        }

        public async Task<(bool ok, TokenResponse? token, string? error)> TryExchangeCodeForTokenAsync(
            string code,
            CancellationToken ct = default
        )
        {
            if (string.IsNullOrWhiteSpace(_settings.ClientId) || _settings.ClientId == "DUMMY")
                return (false, null, "ClientId inválido o DUMMY (usa Production keys).");
            if (string.IsNullOrWhiteSpace(_settings.ClientSecret) || _settings.ClientSecret == "DUMMY")
                return (false, null, "ClientSecret inválido o DUMMY (usa Production keys).");
            if (string.IsNullOrWhiteSpace(_settings.RedirectUri))
                return (false, null, "RedirectUri vacío.");

            var client = _httpFactory.CreateClient();
            var tokenEndpoint = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";

            var req = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var basic = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}")
            );
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]    = "authorization_code",
                ["code"]          = code,
                ["redirect_uri"]  = _settings.RedirectUri
            });

            HttpResponseMessage resp;
            string body;

            try
            {
                resp = await client.SendAsync(req, ct);
                body = await resp.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OAuth] Error de red al pedir el token.");
                return (false, null, $"Error de red al pedir token: {ex.Message}");
            }

            if (!resp.IsSuccessStatusCode)
            {
                var msg = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}";
                if (body?.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase) == true)
                    msg = "invalid_grant (código usado/expirado o no se seleccionó empresa). Repite el flujo completo.";
                else if (body?.Contains("invalid_client", StringComparison.OrdinalIgnoreCase) == true)
                    msg = "invalid_client (ClientId/ClientSecret incorrectos o de otro entorno).";
                else if (body?.Contains("redirect_uri", StringComparison.OrdinalIgnoreCase) == true)
                    msg = "redirect_uri_mismatch (el RedirectUri no coincide exactamente con el configurado en Intuit).";

                _logger.LogWarning("[OAuth] Token exchange failed: {Msg}", msg);
                return (false, null, msg);
            }

            // 1) Intentar JSON (respuesta estándar de Intuit)
            try
            {
                var tokenJson = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(
                    body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                if (tokenJson != null && !string.IsNullOrEmpty(tokenJson.access_token))
                    return (true, tokenJson, null);
            }
            catch (Exception exJson)
            {
                _logger.LogWarning(exJson, "[OAuth] Respuesta de token no era JSON válido. Intentando form-urlencoded…");
            }

            // 2) Fallback: parsear como form-urlencoded
            try
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = part.Split('=', 2);
                    if (kv.Length == 2)
                        dict[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
                }

                if (dict.TryGetValue("access_token", out var at))
                {
                    var token = new TokenResponse
                    {
                        access_token  = at,
                        refresh_token = dict.ContainsKey("refresh_token") ? dict["refresh_token"] : "",
                        token_type    = dict.ContainsKey("token_type") ? dict["token_type"] : ""
                    };
                    if (dict.TryGetValue("expires_in", out var exp) && int.TryParse(exp, out var expInt))
                        token.expires_in = expInt;

                    return (true, token, null);
                }
            }
            catch (Exception exForm)
            {
                _logger.LogError(exForm, "[OAuth] Error parseando form-urlencoded. Body: {Body}", body);
            }

            _logger.LogError("[OAuth] Respuesta de token en formato inesperado. Body: {Body}", body);
            return (false, null, "Respuesta de token en formato inesperado.");
        }

        public async Task<TokenResponse?> ExchangeCodeForTokenAsync(string code, CancellationToken ct = default)
        {
            var (ok, token, _) = await TryExchangeCodeForTokenAsync(code, ct);
            return ok ? token : null;
        }

        // ========= NUEVO: REVOCAR REFRESH TOKEN EN INTUIT =========
        public async Task<(bool ok, string? error)> TryRevokeRefreshTokenAsync(
            string refreshToken,
            CancellationToken ct = default
        )
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return (false, "No hay refresh_token para revocar.");

            if (string.IsNullOrWhiteSpace(_settings.ClientId) || _settings.ClientId == "DUMMY")
                return (false, "ClientId inválido o DUMMY.");
            if (string.IsNullOrWhiteSpace(_settings.ClientSecret) || _settings.ClientSecret == "DUMMY")
                return (false, "ClientSecret inválido o DUMMY.");

            var client = _httpFactory.CreateClient();
            var endpoint = "https://developer.api.intuit.com/v2/oauth2/tokens/revoke";

            var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var basic = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}")
            );
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            // Según Intuit: token=REFRESH_TOKEN
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = refreshToken
            });

            try
            {
                var resp = await client.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[OAuth] Revoke falló HTTP {Code}. Body: {Body}", (int)resp.StatusCode, body);
                    // Aunque falle, podemos continuar con borrar localmente si el usuario lo pidió.
                    return (false, $"HTTP {(int)resp.StatusCode}: {body}");
                }

                _logger.LogInformation("[OAuth] Refresh token revocado con éxito.");
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OAuth] Error de red al revocar token.");
                return (false, ex.Message);
            }
        }
    }
}

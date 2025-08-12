using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace IvaFacilitador.Services
{
    public interface IQuickBooksAuth
    {
        string BuildAuthorizeUrl(string state = "xyz");

        // Método con diagnóstico detallado
        Task<(bool ok, TokenResponse? token, string? error)> TryExchangeCodeForTokenAsync(
            string code,
            CancellationToken ct = default
        );

        // Método de compatibilidad (devuelve null si falla)
        Task<TokenResponse?> ExchangeCodeForTokenAsync(string code, CancellationToken ct = default);
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
            return
                $"https://appcenter.intuit.com/connect/oauth2?client_id={_settings.ClientId}&response_type=code&scope={scope}&redirect_uri={redirect}&state={state}&prompt=consent";
        }

        public async Task<(bool ok, TokenResponse? token, string? error)> TryExchangeCodeForTokenAsync(
            string code,
            CancellationToken ct = default
        )
        {
            // Validaciones rápidas de configuración
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
                ["grant_type"]  = "authorization_code",
                ["code"]        = code,
                ["redirect_uri"]= _settings.RedirectUri
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
                // Errores comunes y mensaje claro
                var msg = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}";
                if (body?.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase) == true)
                    msg =
                        "invalid_grant (código usado/expirado o la empresa no fue seleccionada). Abre 'Conectar QuickBooks' y repite el flujo desde cero.";
                else if (body?.Contains("invalid_client", StringComparison.OrdinalIgnoreCase) == true)
                    msg = "invalid_client (ClientId/ClientSecret incorrectos o de otro entorno).";
                else if (body?.Contains("redirect_uri", StringComparison.OrdinalIgnoreCase) == true)
                    msg =
                        "redirect_uri_mismatch (el RedirectUri no coincide exactamente con el configurado en Intuit).";

                _logger.LogWarning("[OAuth] Token exchange failed: {Msg}", msg);
                return (false, null, msg);
            }

            try
            {
                var token = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(body);
                if (token == null) return (false, null, "Respuesta de token vacía.");
                return (true, token, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OAuth] No se pudo deserializar la respuesta del token. Body: {Body}", body);
                return (false, null, "No se pudo leer la respuesta del token.");
            }
        }

        // Compatibilidad con el código existente (internamente usa el método con diagnóstico)
        public async Task<TokenResponse?> ExchangeCodeForTokenAsync(string code, CancellationToken ct = default)
        {
            var (ok, token, _) = await TryExchangeCodeForTokenAsync(code, ct);
            return ok ? token : null;
        }
    }
}

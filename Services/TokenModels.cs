namespace IvaFacilitador.Services
{
    public class IntuitOAuthSettings
    {
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string RedirectUri { get; set; } = "";
        public string Environment { get; set; } = "sandbox";
        public string Scopes { get; set; } = "com.intuit.quickbooks.accounting";
    }

    public class TokenResponse
    {
        public string access_token { get; set; } = "";
        public string refresh_token { get; set; } = "";
        public int expires_in { get; set; }
        public int x_refresh_token_expires_in { get; set; }
        public string token_type { get; set; } = "";
        public string? id_token { get; set; }
    }

    public interface ITokenStore
    {
        void Save(string realmId, TokenResponse token);
        TokenResponse? Get(string realmId);
        void Delete(string realmId); // ← NUEVO
    }

    public class FileTokenStore : ITokenStore
    {
        private readonly string _folder;
        private readonly object _lock = new object();

        public FileTokenStore(IConfiguration cfg)
        {
            _folder = cfg.GetSection("Data")["Folder"] ?? "App_Data";
            Directory.CreateDirectory(_folder);
        }

        public void Save(string realmId, TokenResponse token)
        {
            lock (_lock)
            {
                var path = Path.Combine(_folder, $"token_{realmId}.json");
                File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(token));
            }
        }

        public TokenResponse? Get(string realmId)
        {
            lock (_lock)
            {
                var path = Path.Combine(_folder, $"token_{realmId}.json");
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                return System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(json);
            }
        }

        public void Delete(string realmId) // ← NUEVO
        {
            lock (_lock)
            {
                var path = Path.Combine(_folder, $"token_{realmId}.json");
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}

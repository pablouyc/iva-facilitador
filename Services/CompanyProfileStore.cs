using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using IvaFacilitador.Models;
using Microsoft.AspNetCore.Hosting;

namespace IvaFacilitador.Services
{
    public class CompanyProfileStore
    {
        private readonly string _basePath;
        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        public CompanyProfileStore(IWebHostEnvironment env)
        {
            _basePath = Path.Combine(env.ContentRootPath, "App_Data", "companies");
            Directory.CreateDirectory(_basePath);
        }

        private string PathFor(string realmId) => Path.Combine(_basePath, realmId, "profile.json");

        public async Task<CompanyProfile?> LoadAsync(string realmId)
        {
            if (string.IsNullOrWhiteSpace(realmId)) return null;
            var file = PathFor(realmId);
            if (!File.Exists(file)) return null;
            await using var fs = File.OpenRead(file);
            return await JsonSerializer.DeserializeAsync<CompanyProfile>(fs, _json);
        }

        public async Task SaveAsync(CompanyProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.RealmId)) return;
            var file = PathFor(profile.RealmId);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await using var fs = File.Create(file);
            await JsonSerializer.SerializeAsync(fs, profile, _json);
        }
    }
}

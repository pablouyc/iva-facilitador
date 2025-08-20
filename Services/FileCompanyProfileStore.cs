using System.IO;
using System.Text.Json;
using IvaFacilitador.Models;

namespace IvaFacilitador.Services
{
    public class FileCompanyProfileStore : ICompanyProfileStore
    {
        private readonly string _baseDir;

        public FileCompanyProfileStore(IWebHostEnvironment env)
        {
            // Data/Profiles junto al contenido
            _baseDir = Path.Combine(env.ContentRootPath, "Data", "Profiles");
            Directory.CreateDirectory(_baseDir);
        }

        private string PathFor(string realmId) => Path.Combine(_baseDir, $"{realmId}.json");

        public CompanyProfile? Get(string realmId)
        {
            if (string.IsNullOrWhiteSpace(realmId)) return null;
            var p = PathFor(realmId);
            if (!File.Exists(p)) return null;
            var json = File.ReadAllText(p);
            return JsonSerializer.Deserialize<CompanyProfile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public void Upsert(CompanyProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.RealmId)) return;
            Directory.CreateDirectory(_baseDir);
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PathFor(profile.RealmId), json);
        }
    }
}
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using IvaFacilitador.Models;

namespace IvaFacilitador.Services
{
    public class FileCompanyProfileStore : ICompanyProfileStore
    {
        private static readonly object _lock = new();
        private readonly string _baseDir;
        private readonly JsonSerializerOptions _json = new JsonSerializerOptions { WriteIndented = true };

        public FileCompanyProfileStore()
        {
            _baseDir = Path.Combine(AppContext.BaseDirectory, "App_Data");
            Directory.CreateDirectory(_baseDir);
        }

        private string GetPath(string userId) => Path.Combine(_baseDir, $"profiles_{userId}.json");

        public CompanyProfile? Get(string realmId, string userId = "demo-user")
        {
            lock (_lock)
            {
                var path = GetPath(userId);
                if (!File.Exists(path)) return null;
                var list = JsonSerializer.Deserialize<List<CompanyProfile>>(File.ReadAllText(path)) ?? new();
                return list.FirstOrDefault(p => p.RealmId == realmId);
            }
        }

        public void SaveOrUpdate(CompanyProfile profile, string userId = "demo-user")
        {
            lock (_lock)
            {
                var path = GetPath(userId);
                var list = File.Exists(path)
                    ? (JsonSerializer.Deserialize<List<CompanyProfile>>(File.ReadAllText(path)) ?? new())
                    : new List<CompanyProfile>();

                var existing = list.FirstOrDefault(p => p.RealmId == profile.RealmId);
                if (existing == null)
                {
                    list.Add(profile);
                }
                else
                {
                    existing.TariffsReviewedAt = profile.TariffsReviewedAt;
                    existing.SalesTariffs = profile.SalesTariffs ?? new();
                }

                File.WriteAllText(path, JsonSerializer.Serialize(list, _json));
            }
        }
    }
}

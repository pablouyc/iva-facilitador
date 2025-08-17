using System.Collections.Concurrent;

namespace IvaFacilitador.App.Services
{
    public sealed class CompanyInfo
    {
        public string RealmId { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public interface ICompanyMemoryStore
    {
        // Preliminar
        void SetPrelim(CompanyInfo c);
        CompanyInfo? GetPrelim(string realmId);
        void ClearPrelim(string realmId);

        // Definitivo
        void Save(CompanyInfo c);
        CompanyInfo? Get(string realmId);
        bool Exists(string realmId);
        void Delete(string realmId);
    }

    public class InMemoryCompanyStore : ICompanyMemoryStore
    {
        private readonly ConcurrentDictionary<string, CompanyInfo> _prelim = new();
        private readonly ConcurrentDictionary<string, CompanyInfo> _saved  = new();

        public void SetPrelim(CompanyInfo c) => _prelim[c.RealmId] = c;
        public CompanyInfo? GetPrelim(string realmId) => _prelim.TryGetValue(realmId, out var c) ? c : null;
        public void ClearPrelim(string realmId) => _prelim.TryRemove(realmId, out _);

        public void Save(CompanyInfo c) => _saved[c.RealmId] = c;
        public CompanyInfo? Get(string realmId) => _saved.TryGetValue(realmId, out var c) ? c : null;
        public bool Exists(string realmId) => _saved.ContainsKey(realmId);
        public void Delete(string realmId) => _saved.TryRemove(realmId, out _);
    }
}

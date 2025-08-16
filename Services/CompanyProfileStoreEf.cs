using System.Text.Json;
using System.Threading.Tasks;
using IvaFacilitador.Data;
using Microsoft.EntityFrameworkCore;
using DataCompanyProfile = IvaFacilitador.Data.CompanyProfile;
using DomainCompanyProfile = IvaFacilitador.Models.CompanyProfile;

namespace IvaFacilitador.Services
{
    public class CompanyProfileStoreEf : ICompanyProfileStore
    {
        private readonly AppDbContext _db;
        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        public CompanyProfileStoreEf(AppDbContext db)
        {
            _db = db;
        }

        public async Task<DomainCompanyProfile?> LoadAsync(string realmId)
        {
            if (string.IsNullOrWhiteSpace(realmId)) return null;
            var entity = await _db.CompanyProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.RealmId == realmId);
            if (entity?.Json == null) return null;
            return JsonSerializer.Deserialize<DomainCompanyProfile>(entity.Json, _json);
        }

        public async Task SaveAsync(DomainCompanyProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.RealmId)) return;
            var json = JsonSerializer.Serialize(profile, _json);
            var entity = await _db.CompanyProfiles.SingleOrDefaultAsync(x => x.RealmId == profile.RealmId);
            if (entity == null)
            {
                entity = new DataCompanyProfile { RealmId = profile.RealmId, Json = json };
                _db.CompanyProfiles.Add(entity);
            }
            else
            {
                entity.Json = json;
            }
            await _db.SaveChangesAsync();
        }
    }
}

using System;
using System.Text.Json;
using System.Threading.Tasks;
using IvaFacilitador.Data;
using Microsoft.EntityFrameworkCore;
using DomainParametrizacion = IvaFacilitador.Models.ParametrizacionEmpresa;

namespace IvaFacilitador.Services
{
    public class ParametrizacionRepository
    {
        private readonly AppDbContext _db;
        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        public ParametrizacionRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<DomainParametrizacion?> GetAsync(string realmId)
        {
            var entity = await _db.ParametrizacionEmpresas.AsNoTracking().SingleOrDefaultAsync(p => p.RealmId == realmId);
            if (entity == null) return null;
            return JsonSerializer.Deserialize<DomainParametrizacion>(entity.JsonConfig, _json);
        }

        public async Task SaveAsync(string realmId, string jsonConfig)
        {
            var entity = await _db.ParametrizacionEmpresas.SingleOrDefaultAsync(p => p.RealmId == realmId);
            if (entity == null)
            {
                entity = new Data.ParametrizacionEmpresa
                {
                    RealmId = realmId,
                    JsonConfig = jsonConfig,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.ParametrizacionEmpresas.Add(entity);
            }
            else
            {
                entity.JsonConfig = jsonConfig;
                entity.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
        }
    }
}

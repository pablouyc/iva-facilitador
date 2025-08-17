using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

using IvaFacilitador.Data;
using IvaFacilitador.Models;

namespace IvaFacilitador.Services
{
    public class CompanyStoreEf : ICompanyStore
    {
        private readonly AppDbContext _db;

        public CompanyStoreEf(AppDbContext db)
        {
            _db = db;
        }

        public IReadOnlyList<CompanyConnection> GetCompaniesForUser(string userId = "demo-user")
        {
            return _db.CompanyConnections.AsNoTracking().OrderBy(c => c.Name).ToList();
        }

        public void AddOrUpdateCompany(CompanyConnection company, string userId = "demo-user")
        {
            var existing = _db.CompanyConnections.SingleOrDefault(c => c.RealmId == company.RealmId);
            if (existing == null)
            {
                company.ConnectedAt = DateTime.UtcNow;
                _db.CompanyConnections.Add(company);
            }
            else
            {
                existing.Name = company.Name;
                existing.ConnectedAt = DateTime.UtcNow;
            }
            _db.SaveChanges();
        }

        public void RemoveCompany(string realmId, string userId = "demo-user")
        {
            var existing = _db.CompanyConnections.SingleOrDefault(c => c.RealmId == realmId);
            if (existing != null)
            {
                _db.CompanyConnections.Remove(existing);
                _db.SaveChanges();
            }
        }
    }
}

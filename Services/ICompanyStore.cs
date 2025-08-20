using IvaFacilitador.Models;

namespace IvaFacilitador.Services
{
    public interface ICompanyStore
    {
IReadOnlyList<CompanyConnection> GetCompaniesForUser(string userId = "demo-user");
        void AddOrUpdateCompany(CompanyConnection company, string userId = "demo-user");
        void RemoveCompany(string realmId, string userId = "demo-user"); // ← NUEVO

}
}

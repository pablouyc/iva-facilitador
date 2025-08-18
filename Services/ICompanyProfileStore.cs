using IvaFacilitador.Models;

namespace IvaFacilitador.Services
{
    public interface ICompanyProfileStore
    {
        CompanyProfile? Get(string realmId, string userId = "demo-user");
        void SaveOrUpdate(CompanyProfile profile, string userId = "demo-user");
    }
}

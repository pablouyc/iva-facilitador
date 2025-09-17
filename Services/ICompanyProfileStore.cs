using IvaFacilitador.Models;

namespace IvaFacilitador.Services
{
    public interface ICompanyProfileStore
    {
        CompanyProfile? Get(string realmId);
        void Upsert(CompanyProfile profile);
    }
}
using System.Threading.Tasks;
using IvaFacilitador.Models;

namespace IvaFacilitador.Services
{
    public interface ICompanyProfileStore
    {
        Task<CompanyProfile?> LoadAsync(string realmId);
        Task SaveAsync(CompanyProfile profile);
    }
}

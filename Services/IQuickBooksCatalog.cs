using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IvaFacilitador.Models;

namespace IvaFacilitador.Services
{
    public interface IQuickBooksCatalog
    {
        Task<IReadOnlyList<QboAccount>> SearchAccountsAsync(string realmId, string accessToken, string name, CancellationToken ct = default);
    }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IvaFacilitador.Models;

namespace IvaFacilitador.Services
{
    /// <summary>
    /// Searches QuickBooks catalog for accounts by name.
    /// </summary>
    public class QuickBooksCatalog : IQuickBooksCatalog
    {
        public Task<IReadOnlyList<QboAccount>> SearchAccountsAsync(string realmId, string accessToken, string name, CancellationToken ct = default)
        {
            // Real implementation would call the QuickBooks QueryService.
            return Task.FromResult<IReadOnlyList<QboAccount>>(new List<QboAccount>());
        }
    }

    /// <summary>
    /// Development stub with static accounts.
    /// </summary>
    public class StubQuickBooksCatalog : IQuickBooksCatalog
    {
        public Task<IReadOnlyList<QboAccount>> SearchAccountsAsync(string realmId, string accessToken, string name, CancellationToken ct = default)
        {
            var list = new List<QboAccount>
            {
                new QboAccount { Id = "1", Name = "Caja", FullyQualifiedName = "Caja", AccountType = "Bank" },
                new QboAccount { Id = "2", Name = "Ventas", FullyQualifiedName = "Ingresos:Ventas", AccountType = "Income" }
            };
            return Task.FromResult<IReadOnlyList<QboAccount>>(list);
        }
    }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IvaFacilitador.Services
{
    public interface IQuickBooksTariffDetector
    {
        Task<IEnumerable<string>> DetectTariffsAsync(string realmId, string accessToken, CancellationToken ct = default);
    }
}

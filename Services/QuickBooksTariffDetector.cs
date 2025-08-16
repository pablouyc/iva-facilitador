using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace IvaFacilitador.Services
{
    /// <summary>
    /// Detects tariff labels by inspecting recent QuickBooks transactions.
    /// </summary>
    public class QuickBooksTariffDetector : IQuickBooksTariffDetector
    {
        private readonly ILogger<QuickBooksTariffDetector> _logger;
        public QuickBooksTariffDetector(ILogger<QuickBooksTariffDetector> logger)
        {
            _logger = logger;
        }

        public Task<IEnumerable<string>> DetectTariffsAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            _logger.LogInformation("Detecting tariffs for {RealmId}", realmId);
            // Real implementation would query QuickBooks for invoices and sales receipts
            // from the last six months and gather tariff labels.
            return Task.FromResult<IEnumerable<string>>(new List<string>());
        }
    }

    /// <summary>
    /// Development stub that returns some fake tariffs.
    /// </summary>
    public class StubQuickBooksTariffDetector : IQuickBooksTariffDetector
    {
        public Task<IEnumerable<string>> DetectTariffsAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            IEnumerable<string> tariffs = new[] { "01", "13" };
            return Task.FromResult(tariffs);
        }
    }
}

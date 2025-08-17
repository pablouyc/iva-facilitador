namespace IvaFacilitador.Services;

public interface IQboCompanyInfoService
{
    Task<(string CompanyName, string RealmId)> GetCompanyInfoAsync(string accessToken, string realmId);
}

public class QboCompanyInfoService : IQboCompanyInfoService
{
    public Task<(string CompanyName, string RealmId)> GetCompanyInfoAsync(string accessToken, string realmId)
    {
        // Placeholder implementation
        return Task.FromResult(("DemoCo", realmId));
    }
}

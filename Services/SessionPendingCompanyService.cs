using System.Text.Json;
using IvaFacilitador.Models;
using Microsoft.AspNetCore.Http;

namespace IvaFacilitador.Services;

public interface ISessionPendingCompanyService
{
    void Save(PendingCompany pc);
    PendingCompany? Get();
    void Clear();
}

public class SessionPendingCompanyService : ISessionPendingCompanyService
{
    private const string Key = "PendingCompany";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SessionPendingCompanyService(IHttpContextAccessor accessor)
    {
        _httpContextAccessor = accessor;
    }

    public void Save(PendingCompany pc)
    {
        var json = JsonSerializer.Serialize(pc);
        _httpContextAccessor.HttpContext!.Session.SetString(Key, json);
    }

    public PendingCompany? Get()
    {
        var json = _httpContextAccessor.HttpContext!.Session.GetString(Key);
        return json is null ? null : JsonSerializer.Deserialize<PendingCompany>(json);
    }

    public void Clear()
    {
        _httpContextAccessor.HttpContext!.Session.Remove(Key);
    }
}

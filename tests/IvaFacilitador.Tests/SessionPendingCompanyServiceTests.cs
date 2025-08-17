using System.Text;
using IvaFacilitador.Models;
using IvaFacilitador.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace IvaFacilitador.Tests;

public class SessionPendingCompanyServiceTests
{
    [Fact]
    public void SaveGetClearWorks()
    {
        var context = new DefaultHttpContext();
        context.Session = new FakeSession();
        var accessor = new HttpContextAccessor { HttpContext = context };
        var service = new SessionPendingCompanyService(accessor);

        var pc = new PendingCompany { CompanyName = "Demo" };
        service.Save(pc);
        var loaded = service.Get();
        Assert.NotNull(loaded);
        service.Clear();
        Assert.Null(service.Get());
    }

    private class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public IEnumerable<string> Keys => _store.Keys;
        public string Id { get; } = Guid.NewGuid().ToString();
        public bool IsAvailable => true;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}

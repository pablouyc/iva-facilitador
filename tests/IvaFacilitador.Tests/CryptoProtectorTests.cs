using IvaFacilitador.Utils;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace IvaFacilitador.Tests;

public class CryptoProtectorTests
{
    [Fact]
    public void ProtectsAndUnprotects()
    {
        var provider = DataProtectionProvider.Create("test");
        var crypto = new CryptoProtector(provider);
        var cipher = crypto.Protect("hola");
        var plain = crypto.Unprotect(cipher);
        Assert.Equal("hola", plain);
    }
}

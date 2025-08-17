using Microsoft.AspNetCore.DataProtection;

namespace IvaFacilitador.Utils;

public interface ICryptoProtector
{
    string Protect(string plain);
    string Unprotect(string cipher);
}

public class CryptoProtector : ICryptoProtector
{
    private readonly IDataProtector _protector;

    public CryptoProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("QboTokens");
    }

    public string Protect(string plain) => _protector.Protect(plain);

    public string Unprotect(string cipher) => _protector.Unprotect(cipher);
}

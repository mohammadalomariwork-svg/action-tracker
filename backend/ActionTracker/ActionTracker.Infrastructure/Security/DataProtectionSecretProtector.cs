using ActionTracker.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace ActionTracker.Infrastructure.Security;

/// <summary>
/// Wraps ASP.NET Core DataProtection (already registered by Identity) to
/// encrypt/decrypt small secrets stored in the database.
/// </summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    private const string Purpose = "ActionTracker.MailServerConfig.Password.v1";

    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return string.Empty;
        return _protector.Unprotect(ciphertext);
    }
}

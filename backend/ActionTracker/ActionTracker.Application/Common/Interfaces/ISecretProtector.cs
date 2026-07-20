namespace ActionTracker.Application.Common.Interfaces;

/// <summary>
/// Symmetric encryption helper for secrets at rest (e.g. SMTP password).
/// Implementations should be keyed to a stable purpose so values stay
/// decryptable across app restarts.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}

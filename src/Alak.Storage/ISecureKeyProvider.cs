namespace Alak.Storage;

/// <summary>
/// Service providing secure database encryption passphrase backed by Android KeyStore or environment fallback (SEC-002).
/// </summary>
public interface ISecureKeyProvider
{
    string GetDatabasePassword();
    bool ReinitializeKey() => false;
}

/// <summary>
/// Default development/test key provider returning the standard development encryption password.
/// </summary>
public sealed class DefaultSecureKeyProvider : ISecureKeyProvider
{
    public const string DefaultPassword = "alak_secure_key_2024";

    public string GetDatabasePassword() => DefaultPassword;
    public bool ReinitializeKey() => false;
}

using System;
using System.Security.Cryptography;
using System.Text;
using Alak.Storage;

#if ANDROID
using Android.App;
using Android.Content;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
#endif

namespace Alak.App.Services;

/// <summary>
/// Hardware-backed secure key provider for SQLCipher database encryption key (SEC-002).
/// Backed by Android KeyStore on Android with AES-256 GCM encryption of master passphrase in private storage.
/// Gracefully falls back to development key in unit test and desktop environments.
/// </summary>
public class SecureKeyProvider : ISecureKeyProvider
{
    private const string FallbackPassword = "alak_secure_key_2024";
    private const string KeyAlias = "alak_db_master_key";
    private const string PrefsName = "alak_secure_storage";
    private const string PrefKeyIv = "db_key_iv";
    private const string PrefKeyEncrypted = "db_key_encrypted";
    private string? _cachedPassword;

    public string GetDatabasePassword()
    {
        if (_cachedPassword != null) return _cachedPassword;
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private)
                ?? throw new InvalidOperationException("Failed to access SharedPreferences for secure storage.");

            var keyStore = KeyStore.GetInstance("AndroidKeyStore");
            keyStore.Load(null);

            // If master key doesn't exist in Android KeyStore, generate AES-256 GCM key
            if (!keyStore.ContainsAlias(KeyAlias))
            {
                var keyGenerator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore");
                var builder = new KeyGenParameterSpec.Builder(
                    KeyAlias,
                    KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                    .SetBlockModes(KeyProperties.BlockModeGcm)
                    .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
                    .SetKeySize(256)
                    .SetRandomizedEncryptionRequired(true);

                keyGenerator.Init(builder.Build());
                keyGenerator.GenerateKey();
            }

            var secretKey = keyStore.GetKey(KeyAlias, null)
                ?? throw new InvalidOperationException("Failed to retrieve master key from AndroidKeyStore.");

            var storedIv = prefs.GetString(PrefKeyIv, null);
            var storedEncrypted = prefs.GetString(PrefKeyEncrypted, null);

            if (!string.IsNullOrEmpty(storedIv) && !string.IsNullOrEmpty(storedEncrypted))
            {
                // Decrypt existing database passphrase
                var iv = Convert.FromBase64String(storedIv);
                var encrypted = Convert.FromBase64String(storedEncrypted);

                var cipher = Cipher.GetInstance("AES/GCM/NoPadding");
                var gcmSpec = new GCMParameterSpec(128, iv);
                cipher.Init(Javax.Crypto.CipherMode.DecryptMode, secretKey, gcmSpec);
                var decrypted = cipher.DoFinal(encrypted);
                _cachedPassword = Convert.ToHexString(decrypted);
                return _cachedPassword;
            }
            else
            {
                // Generate new 256-bit database passphrase
                byte[] rawPassphrase = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(rawPassphrase);
                }

                var cipher = Cipher.GetInstance("AES/GCM/NoPadding");
                cipher.Init(Javax.Crypto.CipherMode.EncryptMode, secretKey);
                var iv = cipher.GetIV();
                var encrypted = cipher.DoFinal(rawPassphrase);

                prefs.Edit()?
                    .PutString(PrefKeyIv, Convert.ToBase64String(iv))
                    .PutString(PrefKeyEncrypted, Convert.ToBase64String(encrypted))
                    .Apply();

                _cachedPassword = Convert.ToHexString(rawPassphrase);
                return _cachedPassword;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SecureKeyProvider] Android KeyStore exception: {ex.Message}");
            throw new CryptographicException($"Android KeyStore operation failed: {ex.Message}", ex);
        }
#else
        return FallbackPassword;
#endif
    }

    public bool ReinitializeKey()
    {
        _cachedPassword = null;
#if ANDROID
        try
        {
            var keyStore = KeyStore.GetInstance("AndroidKeyStore");
            keyStore.Load(null);
            if (keyStore.ContainsAlias(KeyAlias))
            {
                keyStore.DeleteEntry(KeyAlias);
            }

            var context = Android.App.Application.Context;
            var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            prefs?.Edit()?.Remove(PrefKeyIv)?.Remove(PrefKeyEncrypted)?.Apply();

            GetDatabasePassword();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SecureKeyProvider] KeyStore re-initialization failed: {ex.Message}");
            return false;
        }
#else
        return false;
#endif
    }
}

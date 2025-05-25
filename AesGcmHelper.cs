using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

public static class AesGcmHelper
{
    /// <summary>
    /// Extracts the AES key from the browser's "Local State" file using DPAPI.
    /// </summary>
    public static byte[] ExtractKeyFromLocalState(string localStatePath)
    {
        if (!File.Exists(localStatePath))
            throw new FileNotFoundException("Local State file not found.", localStatePath);

        string json = File.ReadAllText(localStatePath);
        JObject localState = JObject.Parse(json);

        string encryptedKeyBase64 = localState["os_crypt"]?["encrypted_key"]?.ToString();
        if (string.IsNullOrEmpty(encryptedKeyBase64))
            throw new InvalidOperationException("Encrypted key not found in Local State.");

        byte[] encryptedKey = Convert.FromBase64String(encryptedKeyBase64);

        const int dpapiPrefixLength = 5; // "DPAPI"
        if (!Encoding.ASCII.GetString(encryptedKey, 0, dpapiPrefixLength).Equals("DPAPI"))
            throw new FormatException("Encrypted key does not start with DPAPI.");

        byte[] dpapiEncryptedKey = encryptedKey[dpapiPrefixLength..];
        return ProtectedData.Unprotect(dpapiEncryptedKey, null, DataProtectionScope.CurrentUser);
    }

    /// <summary>
    /// Decrypts a Chromium-style cookie encrypted with AES-GCM or legacy DPAPI.
    /// </summary>
    public static string DecryptCookie(byte[] encryptedValue, byte[] aesKey)
    {
        try
        {
            if (encryptedValue == null || encryptedValue.Length == 0)
                return "(Empty)";
            if (aesKey == null || aesKey.Length == 0)
                return "(Invalid AES key)";

            // Check for AES-GCM prefix
            if (encryptedValue.Length >= 31 &&
                encryptedValue[0] == (byte)'v' && encryptedValue[1] == (byte)'1' &&
                (encryptedValue[2] == (byte)'0' || encryptedValue[2] == (byte)'1')) // v10 or v11
            {
                const int prefixLength = 3;
                const int nonceLength = 12;
                const int tagLength = 16;

                byte[] nonce = new byte[nonceLength];
                Array.Copy(encryptedValue, prefixLength, nonce, 0, nonceLength);

                int ciphertextStart = prefixLength + nonceLength;
                int ciphertextLength = encryptedValue.Length - ciphertextStart - tagLength;

                if (ciphertextLength <= 0)
                    return "(Invalid ciphertext length)";

                byte[] ciphertext = new byte[ciphertextLength];
                byte[] tag = new byte[tagLength];

                Array.Copy(encryptedValue, ciphertextStart, ciphertext, 0, ciphertextLength);
                Array.Copy(encryptedValue, ciphertextStart + ciphertextLength, tag, 0, tagLength);

                byte[] decrypted = new byte[ciphertext.Length];
                using (var aesGcm = new AesGcm(aesKey))
                {
                    aesGcm.Decrypt(nonce, ciphertext, tag, decrypted);
                }

                return Encoding.UTF8.GetString(decrypted);
            }
            else
            {
                // Legacy DPAPI format
                byte[] decrypted = ProtectedData.Unprotect(encryptedValue, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
        }
        catch (CryptographicException ex)
        {
            return $"(Decryption failed: {ex.Message})";
        }
        catch (Exception ex)
        {
            return $"(Error: {ex.GetType().Name}: {ex.Message})";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;

namespace BrowseJobs;

public static class EncryptedCookieHelper
{
    public static List<Cookie> ExtractDecryptedCookies(string userDataDir, string profile = "Default", string domainFilter = "dice.com")
    {
        string cookiePath = Path.Combine(userDataDir, profile, "Network", "Cookies");
        string localStatePath = Path.Combine(userDataDir, "Local State");

        if (!File.Exists(cookiePath) || !File.Exists(localStatePath))
            throw new FileNotFoundException("Chrome/Edge cookie or state file not found.");

        // Copy to avoid lock
        string tempCookiePath = Path.Combine(Path.GetTempPath(), $"cookies_{Guid.NewGuid()}.db");
        File.Copy(cookiePath, tempCookiePath, true);

        // Get the AES key
        byte[] encryptedKey = ExtractBase64KeyFromLocalState(localStatePath);
        byte[] aesKey = ProtectedData.Unprotect(encryptedKey, null, DataProtectionScope.CurrentUser);

        var cookies = new List<Cookie>();

        using var conn = new SQLiteConnection($"Data Source={tempCookiePath};Version=3;");
        conn.Open();

        string sql = $"SELECT host_key, name, encrypted_value, path, expires_utc, is_secure FROM cookies WHERE host_key LIKE '%{domainFilter}%'";
        using var cmd = new SQLiteCommand(sql, conn);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            string domain = reader.GetString(0);
            string name = reader.GetString(1);
            byte[] encryptedBytes = (byte[])reader["encrypted_value"];
            string path = reader.GetString(3);
            long expiresUtc = reader.GetInt64(4);
            bool isSecure = reader.GetBoolean(5);

            string decryptedValue = DecryptChromiumCookie(encryptedBytes, aesKey);
            DateTime expiry = DateTime.Now.AddDays(30);

            try
            {
                expiry = DateTime.FromFileTimeUtc(10 * (expiresUtc - 11644473600000000));
            }
            catch { }

            cookies.Add(new Cookie(name, decryptedValue, domain, path, expiry));
        }

        conn.Close();
        File.Delete(tempCookiePath);
        return cookies;
    }

    private static byte[] ExtractBase64KeyFromLocalState(string localStatePath)
    {
        var json = JObject.Parse(File.ReadAllText(localStatePath));
        string base64Key = json["os_crypt"]?["encrypted_key"]?.ToString();

        if (string.IsNullOrEmpty(base64Key))
            throw new Exception("Could not find encrypted_key in Local State");

        byte[] fullKey = Convert.FromBase64String(base64Key);
        // Strip "DPAPI" prefix (5 bytes)
        return fullKey[5..];
    }

    private static string DecryptChromiumCookie(byte[] encryptedValue, byte[] key)
    {
        const int NONCE_SIZE = 12;
        const int TAG_SIZE = 16;

        if (Encoding.ASCII.GetString(encryptedValue, 0, 3) != "v10")
            return Encoding.UTF8.GetString(encryptedValue); // plaintext fallback

        byte[] nonce = new byte[NONCE_SIZE];
        byte[] ciphertext = new byte[encryptedValue.Length - 3 - NONCE_SIZE - TAG_SIZE];
        byte[] tag = new byte[TAG_SIZE];

        Buffer.BlockCopy(encryptedValue, 3, nonce, 0, NONCE_SIZE);
        Buffer.BlockCopy(encryptedValue, 3 + NONCE_SIZE, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(encryptedValue, encryptedValue.Length - TAG_SIZE, tag, 0, TAG_SIZE);

        byte[] combinedCiphertext = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combinedCiphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combinedCiphertext, ciphertext.Length, tag.Length);

        using var aes = new AesGcm(key);
        byte[] plaintext = new byte[ciphertext.Length];
        aes.Decrypt(nonce, combinedCiphertext, null, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
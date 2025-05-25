using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using SQLitePCL;

public class EdgeCookie
{
    public string Name { get; set; }
    public string Value { get; set; }
    public string Domain { get; set; }
    public string Path { get; set; }
    public bool Secure { get; set; }
    public bool HttpOnly { get; set; }
}

public static class CookieExtractor
{
    public static List<EdgeCookie> ExtractCookies(string browser = "edge", string filterDomain = null)
    {
        string userDataRoot = GetUserDataRoot(browser);
        var allCookies = new List<EdgeCookie>();

        foreach (string profileDir in Directory.GetDirectories(userDataRoot))
        {
            string cookiesDb = Path.Combine(profileDir, "Network", "Cookies");
            if (!File.Exists(cookiesDb))
                continue;

            //string tempDb = Path.Combine(Path.GetTempPath(), $"cookies_{Path.GetFileName(profileDir)}.db");
            string profileName = Path.GetFileName(Path.TrimEndingDirectorySeparator(profileDir));
            string tempDb = Path.Combine(Path.GetTempPath(), $"cookies_{profileName}.db");

            try
            {
                // Copy DB safely even if Edge is running
                using var sourceStream = new FileStream(cookiesDb, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var destStream = new FileStream(tempDb, FileMode.Create, FileAccess.Write);
                sourceStream.CopyTo(destStream);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"⚠️ Skipping locked or inaccessible DB: {cookiesDb} — {ex.Message}");
                continue;
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localStatePath = Path.Combine(userProfile, @"AppData\Local\Microsoft\Edge\User Data\Local State");

            var key_unlock = AesGcmHelper.ExtractKeyFromLocalState(localStatePath);

            Batteries.Init();
            using var conn = new SqliteConnection($"Data Source={tempDb};Mode=ReadOnly;");
            conn.Open();

            string query = "SELECT name, encrypted_value, host_key, path, is_secure, is_httponly FROM cookies";
            using var cmd = new SqliteCommand(query, conn);
            using var reader = cmd.ExecuteReader();
            int encryptedValueIndex = reader.GetOrdinal("encrypted_value");

            while (reader.Read())
            {
                string name = reader.GetString(0);
                byte[] encrypted = reader.GetFieldValue<byte[]>(encryptedValueIndex);
                string host = reader.GetString(2);

                if (!string.IsNullOrEmpty(filterDomain) &&
                    !host.Contains(filterDomain, StringComparison.OrdinalIgnoreCase))
                    continue;


                var decryptCookie = AesGcmHelper.DecryptCookie(encrypted, encrypted);
                string value = Decrypt(encrypted);
                if (value == null) continue;

                allCookies.Add(new EdgeCookie
                {
                    Name = name,
                    Value = value,
                    Domain = host,
                    Path = reader.GetString(3),
                    Secure = reader.GetBoolean(4),
                    HttpOnly = reader.GetBoolean(5)
                });
            }
        }

        return allCookies;
    }

    public static void ExportToJson(List<EdgeCookie> cookies, string outputPath)
    {
        File.WriteAllText(outputPath, JsonConvert.SerializeObject(cookies, Formatting.Indented));
        Console.WriteLine($"✅ Exported {cookies.Count} cookies to: {outputPath}");
    }

    private static string GetUserDataRoot(string browser)
    {
        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return browser.ToLower() switch
        {
            "edge" => Path.Combine(basePath, "Microsoft", "Edge", "User Data"),
            "chrome" => Path.Combine(basePath, "Google", "Chrome", "User Data"),
            _ => throw new ArgumentException($"Unsupported browser: {browser}")
        };
    }

    private static string Decrypt(byte[] encrypted)
    {
        try
        {
            if (encrypted.Length > 3 && encrypted[0] == 'v' && encrypted[1] == '1')
                encrypted = encrypted[3..];  // Strip "v10"/"v11"

            byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.BiDi.Modules.Network;
using OpenQA.Selenium.Chrome;
using Cookie = OpenQA.Selenium.Cookie;

public static class EdgeCookieHelper
{
    static EdgeCookieHelper()
    {

    }
    private static bool DebugMode { get; set; } = true;
    public static readonly string LogFilePath = Path.Combine(Path.GetTempPath(), "cookie_decrypt_log.txt");

    public class CookieData
    {
        public string Name { get; set; }
        public byte[] EncryptedValue { get; set; }
        public string HostKey { get; set; }
        public string Path { get; set; }
        public bool IsSecure { get; set; }
        public bool IsHttpOnly { get; set; }
        public long ExpiresUtc { get; set; }
    }

    public static void Log(string message)
    {
        if (!DebugMode) return;
        string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        Console.WriteLine(logLine);
        try
        {
            File.AppendAllText(LogFilePath, logLine + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to write to log file: {ex.Message}");
        }
    }

    public static string FindCookiesPath(string userProfile)
    {
        Log($"Searching for Cookies database in user profile: {userProfile}");
        string basePath = Path.Combine(userProfile, @"AppData\Local\Microsoft\Edge\User Data");

        if (!Directory.Exists(basePath))
        {
            Log($"Edge User Data directory not found: {basePath}");
            return null;
        }

        string[] profiles = { "Default", "Profile 1", "Profile 2" };
        foreach (string profile in profiles)
        {
            string cookiesPath = Path.Combine(basePath, profile, "Network", "Cookies");
            Log($"Checking path: {cookiesPath}");
            if (File.Exists(cookiesPath))
            {
                Log($"Found Cookies database at: {cookiesPath}");
                return cookiesPath;
            }
        }

        Log("Cookies database not found in any common profile directories.");
        return null;
    }

    public static List<CookieData> GetCookies(string cookiesPath, string hostFilter = null)
    {
        Log($"Querying Cookies database: {cookiesPath}, HostFilter: {hostFilter ?? "None"}");
        try
        {
            if (!File.Exists(cookiesPath))
            {
                Log($"Cookies database not found: {cookiesPath}");
                throw new FileNotFoundException("Cookies database not found.", cookiesPath);
            }
            Log("Cookies database exists, connecting...");

            var cookies = new List<CookieData>();
            using SQLiteConnection conn = new SQLiteConnection($"Data Source={cookiesPath};Version=3;");
            Log("Opening database connection...");
            conn.Open();
            Log("Database connection opened");

            string query = "SELECT name, encrypted_value, host_key, path, is_secure, is_httponly, expires_utc FROM cookies";
            if (!string.IsNullOrEmpty(hostFilter))
                query += " WHERE host_key LIKE @hostFilter";
            Log($"Executing query: {query}");

            using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
            {
                if (!string.IsNullOrEmpty(hostFilter))
                {
                    cmd.Parameters.AddWithValue("@hostFilter", hostFilter);
                    Log($"Added host filter parameter: {hostFilter}");
                }

                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    Log("Reading query results...");
                    while (reader.Read())
                    {
                        string name = reader.GetString(0);
                        byte[] encryptedValue = reader[1] as byte[] ?? Array.Empty<byte>();
                        string hostKey = reader.GetString(2);
                        string path = reader.GetString(3);
                        bool isSecure = reader.GetInt32(4) == 1;
                        bool isHttpOnly = reader.GetInt32(5) == 1;
                        long expiresUtc = reader.GetInt64(6);

                        Log($"Processing cookie: Name={name}, Host={hostKey}, EncryptedValueLength={encryptedValue.Length}, ExpiresUtc={expiresUtc}");
                        cookies.Add(new CookieData
                        {
                            Name = name,
                            EncryptedValue = encryptedValue,
                            HostKey = hostKey,
                            Path = path,
                            IsSecure = isSecure,
                            IsHttpOnly = isHttpOnly,
                            ExpiresUtc = expiresUtc
                        });
                    }
                }
            }
            Log($"Query complete, retrieved {cookies.Count} cookies");
            return cookies;
        }
        catch (Exception ex)
        {
            Log($"GetCookies failed: {ex.GetType().Name}: {ex.Message}\nStack: {ex.StackTrace}");
            throw new Exception($"Failed to query cookies: {ex.Message}", ex);
        }
    }

    public static void SetCookiesInBrowser(IWebDriver driver, List<CookieData> cookies, string domainUrl, int expiryDays = 30)
    {
        Log($"Setting cookies in browser for domain: {domainUrl}, ExpiryDays: {expiryDays}");
        try
        {
            // Navigate to the domain to set cookies
            driver.Navigate().GoToUrl(domainUrl);
            Log($"Navigated to {domainUrl}");

            // Calculate expiry date dynamically
            DateTime currentDate = DateTime.Now;
            DateTime expiryDate = currentDate.AddDays(expiryDays);
            Log($"Current Date: {currentDate:yyyy-MM-dd HH:mm:ss}, Computed Expiry: {expiryDate:yyyy-MM-dd HH:mm:ss}");

            // Set cookies in the browser
            foreach (var cookie in cookies)
            {
                //public Cookie(
                //    string name,
                //    string value,
                //    string? domain,
                //    string? path,
                //    DateTime? expiry,
                //    bool secure,
                //    bool isHttpOnly,
                //    string? sameSite

                string value = Convert.ToBase64String(cookie.EncryptedValue);
                var seleniumCookie = new Cookie(
                    name : cookie.Name,
                    value : value,
                    domain :cookie.HostKey,
                    path : cookie.Path,
                    expiry : expiryDate,
                    secure : cookie.IsSecure,
                    isHttpOnly : cookie.IsHttpOnly,
                    sameSite : "Lax"
                );
                driver.Manage().Cookies.AddCookie(seleniumCookie);
                Log($"Set cookie: {cookie.Name} for {cookie.HostKey}, Value (Base64): {value.Substring(0, Math.Min(50, value.Length))}..., Expiry: {expiryDate:yyyy-MM-dd HH:mm:ss}");
            }

            // Verify cookies are set
            var browserCookies = driver.Manage().Cookies.AllCookies;
            Log($"Cookies in browser: {browserCookies.Count}");
            foreach (var browserCookie in browserCookies)
            {
                Log($"Browser Cookie: {browserCookie.Name} = {browserCookie.Value}, Expiry: {browserCookie.Expiry?.ToString("yyyy-MM-dd HH:mm:ss") ?? "None"}");
            }
        }
        catch (Exception ex)
        {
            Log($"SetCookiesInBrowser failed: {ex.GetType().Name}: {ex.Message}\nStack: {ex.StackTrace}");
            throw new Exception($"Failed to set cookies in browser: {ex.Message}", ex);
        }
    }

    public static void SetCookiesForAllDomains(IWebDriver driver, IList<CookieData> cookies, int expiryDays = 30)
    {
        Log($"Setting cookies for {cookies.Count} items across all domains");

        List<IGrouping<string, CookieData>> grouped = cookies
            .Where(c => !string.IsNullOrEmpty(c.HostKey))
            .GroupBy(c => c.HostKey.TrimStart('.'))
            .OrderBy(x => x.Key)
            .Where(x => x.Key.Contains("dice.com"))
            .ToList();

        foreach (var group in grouped)
        {
            string host = group.Key;

    
            string url = $"https://{host}";
            Log($"➡️ Navigating to {url} to set {group.Count()} cookies");

            try
            {
                driver.Navigate().GoToUrl(url);
            }
            catch (Exception navEx)
            {
                Log($"⚠️ Failed to navigate to {url}: {navEx.Message}");
                continue;
            }

            DateTime expiryDate = DateTime.Now.AddDays(expiryDays);
            foreach (var cookie in group)
            {
                try
                {
                    string value = Convert.ToBase64String(cookie.EncryptedValue);
                    
                    var seleniumCookie = new Cookie(
                        name: cookie.Name,
                        value: value,
                        domain: ".dice.com", // cookie.HostKey
                        path: cookie.Path,
                        expiry: expiryDate,
                        secure: cookie.IsSecure,
                        isHttpOnly: cookie.IsHttpOnly,
                        sameSite: "Lax"
                    );
                    driver.Manage().Cookies.AddCookie(seleniumCookie);
                    Log($"✅ Set cookie {seleniumCookie.Name} for {seleniumCookie.Domain}");
                }
                catch (Exception ex)
                {
                    Log($"⚠️ Failed to set cookie {cookie.Name} for {cookie.HostKey}: {ex.Message}");
                }
            }
        }
    }

}

class Shit
{
    public static void DoIt()
    {
        try
        {
            // Set up your ChromeDriver instance
            var options = new ChromeOptions();

            // Headless mode configuration
            options.AddArgument("--headless");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            // Additional options for better compatibility
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);

            IWebDriver driver = new ChromeDriver(options);
            EdgeCookieHelper.Log("ChromeDriver initialized");

            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                EdgeCookieHelper.Log($"User Profile: {userProfile}");
                string profileName = "Profile 2";
                string cookiesPath = Path.Combine(userProfile, $@"AppData\Local\Microsoft\Edge\User Data\{profileName}\Network\Cookies");
                EdgeCookieHelper.Log($"Cookies Path: {cookiesPath}, Exists: {File.Exists(cookiesPath)}");

                if (!File.Exists(cookiesPath))
                {
                    EdgeCookieHelper.Log("Cookies path not found, searching for alternatives...");
                    cookiesPath = EdgeCookieHelper.FindCookiesPath(userProfile);
                    if (cookiesPath == null)
                    {
                        throw new FileNotFoundException(
                            "Cookies database not found in any profile. " +
                            "Ensure Edge is installed and has been used to browse (to generate cookies). " +
                            "Check if the path exists: C:\\Users\\<YourUser>\\AppData\\Local\\Microsoft\\Edge\\User Data\\<Profile>\\Network\\Cookies"
                        );
                    }
                }

                string tempCookiesPath = Path.Combine(Path.GetTempPath(), "Cookies");
                EdgeCookieHelper.Log($"Temp Cookies Path: {tempCookiesPath}");
                File.Copy(cookiesPath, tempCookiesPath, true);
                EdgeCookieHelper.Log($"Copied Cookies to: {tempCookiesPath}");

                // Get cookies for marriott.com
                var cookies = EdgeCookieHelper.GetCookies(tempCookiesPath /*, "%.marriott.com" */);
                EdgeCookieHelper.Log($"Retrieved {cookies.Count} cookies for marriott.com");

                // Set cookies in your ChromeDriver instance with dynamic expiry
                // EdgeCookieHelper.SetCookiesInBrowser(driver, cookies, "https://www.dice.com", 30);
                EdgeCookieHelper.SetCookiesForAllDomains(driver, cookies);

                // Navigate to a page to test the session
                driver.Navigate().GoToUrl("https://www.dice.com/");
                EdgeCookieHelper.Log($"Title: {driver.Title}");
            }
            finally
            {
                driver.Quit();
                EdgeCookieHelper.Log("ChromeDriver closed");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Check log file: {EdgeCookieHelper.LogFilePath}");
        }
    }
}
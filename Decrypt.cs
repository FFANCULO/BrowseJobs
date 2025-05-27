using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using BrowseJobs;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

// ReSharper disable All

public interface ICookieDecryptorStrategy
{
    string GetCookieDbPath();
    string GetLocalStatePath();
}

public class ChromeCookieDecryptor : ICookieDecryptorStrategy
{
    public string GetCookieDbPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google", "Chrome", "User Data", "Default", "Network", "Cookies");

    public string GetLocalStatePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google", "Chrome", "User Data", "Local State");
}

public class EdgeCookieDecryptor : ICookieDecryptorStrategy
{
    public string GetCookieDbPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Edge", "User Data", "Profile 2", "Network", "Cookies");

    public string GetLocalStatePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Edge", "User Data", "Local State");
}

public class CookieDecryptionHelper
{
    private readonly ILogger _logger;
    private readonly ICookieDecryptorStrategy _strategy;

    public CookieDecryptionHelper(ICookieDecryptorStrategy strategy, ILogger logger)
    {
        _strategy = strategy;
        _logger = logger;
    }

    public IEnumerable<CookieData> GetCookies(string domainFilter = null)
    {
        _logger.LogInformation("Beginning cookie extraction with filter: {Filter}", domainFilter ?? "(none)");

        var dbPath = _strategy.GetCookieDbPath();
        _logger.LogInformation("Cookie DB path resolved: {Path}", dbPath);

        string tempCookiesPath = Path.Combine(Path.GetTempPath(), "Cookies");

        var tempDb = Path.Combine(tempCookiesPath, $"cookies_temp_{Guid.NewGuid()}.db");
        //File.Copy(dbPath, tempDb, true);
        File.Copy(dbPath, tempCookiesPath, true);

        _logger.LogInformation("Copied cookie DB to temp: {Path}", tempDb);

        var result = new List<CookieData>();

        using var conn = new SqliteConnection($"Data Source={tempCookiesPath};Mode=ReadOnly;");
        conn.Open();
        _logger.LogDebug("Opened SQLite connection to temporary DB.");

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT name, encrypted_value, host_key, path, is_secure, is_httponly, expires_utc FROM cookies";
        if (!string.IsNullOrEmpty(domainFilter))
        {
            cmd.CommandText += " WHERE host_key LIKE @hostFilter";
            cmd.Parameters.AddWithValue("@hostFilter", $"%{domainFilter}%");
        }

        _logger.LogDebug("Executing query: {Query}", cmd.CommandText);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string name = reader.GetString(0);
            byte[] encryptedValue = (byte[])reader["encrypted_value"];
            string host = reader.GetString(2);
            string path = reader.GetString(3);
            bool isSecure = reader.GetInt32(4) == 1;
            bool isHttpOnly = reader.GetInt32(5) == 1;
            long expiresUtc = reader.GetInt64(6);

            DateTime? expiry = null;
            if (expiresUtc > 0)
            {
                long ticks = expiresUtc * 10;
                expiry = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(ticks);
            }

            result.Add(new CookieData
            {
                Name = name,
                EncryptedValue = encryptedValue,
                HostKey = host,
                Path = path,
                IsSecure = isSecure,
                IsHttpOnly = isHttpOnly,
                Expiry = expiry
            });

            _logger.LogDebug("Extracted cookie: {Name} for domain: {Host}, Expiry: {Expiry}", name, host,
                expiry?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "None");
        }

        conn.Close();
        File.Delete(tempDb);
        _logger.LogInformation("Temporary DB deleted: {Path}", tempDb);
        _logger.LogInformation("Cookie extraction completed. Total cookies extracted: {Count}", result.Count);

        return result;
    }

    public void SetCookiesInBrowser(IWebDriver driver, IEnumerable<CookieData> cookies, string domainUrl)
    {
        _logger.LogInformation("Setting cookies in browser for domain: {Domain}", domainUrl);
        try
        {
            driver.Navigate().GoToUrl(domainUrl);
            _logger.LogInformation("Navigated to {Domain}", domainUrl);

            foreach (var cookie in cookies)
            {
                string value = Convert.ToBase64String(cookie.EncryptedValue);
                var seleniumCookie = new Cookie(
                    cookie.Name,
                    value,
                    cookie.HostKey,
                    cookie.Path,
                    cookie.Expiry,
                    cookie.IsSecure,
                    cookie.IsHttpOnly, "Lax"
                );
                driver.Manage().Cookies.AddCookie(seleniumCookie);
                _logger.LogDebug("Set cookie: {Name} for {Host}, Value (Base64): {Value}, Expiry: {Expiry}",
                    cookie.Name, cookie.HostKey, value.Substring(0, Math.Min(50, value.Length)),
                    cookie.Expiry?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "None");
            }

            var browserCookies = driver.Manage().Cookies.AllCookies;
            _logger.LogInformation("Cookies in browser: {Count}", browserCookies.Count);
            foreach (var browserCookie in browserCookies)
            {
                _logger.LogDebug("Browser Cookie: {Name} = {Value}, Expiry: {Expiry}",
                    browserCookie.Name, browserCookie.Value,
                    browserCookie.Expiry?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "None");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set cookies in browser");
            throw new Exception($"Failed to set cookies in browser: {ex.Message}", ex);
        }
    }

    public class CookieData
    {
        public string Name { get; set; }
        public byte[] EncryptedValue { get; set; }
        public string HostKey { get; set; }
        public string Path { get; set; }
        public bool IsSecure { get; set; }
        public bool IsHttpOnly { get; set; }
        public DateTime? Expiry { get; set; }
    }
}

class Prog2
{
    public static object? SafeClick(IWebDriver driver, IWebElement element)
    {
        try
        {
            var result =
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({ block: 'center' });",
                    element);
            element.Click();
            return result;
        }
        catch (ElementClickInterceptedException)
        {
            // fallback JS click
            var result = ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", element);
            return result;
        }
    }

    public static void ClickFirstEasyApply(IWebDriver driver)
    {
        ReadOnlyCollection<IWebElement> buttons =
            driver.FindElements(
                By.XPath("//span[normalize-space()='Easy Apply']/ancestor::*[self::button or self::a]"));
        foreach (IWebElement button in buttons)
        {
            if (button.Displayed && button.Enabled)
            {
                SafeClick(driver, button);
                break;
            }
        }
    }

    public static void DoSomeStuff(IWebDriver driver)
    {
        string originalWindow = driver.CurrentWindowHandle;
        int pageNumber = 0;
        try
        {
            next:

            foreach (var func in GetApplyButtons(driver))
            {
                var f = func();
                // Wait for a new window to appear
                new WebDriverWait(driver, TimeSpan.FromSeconds(10)).Until(d => d.WindowHandles.Count > 1);

                var newHandles = driver.WindowHandles.Except(new[] { originalWindow }).ToList();

                // Switch to the new window
                foreach (var handle in newHandles)
                {
                    driver.SwitchTo().Window(handle);
                    // GET Handle to EASY APPLY buttons
                    // GET Job Description verbiage 
                    // Use Grok and write Cover Letter

                    Thread.Sleep(TimeSpan.FromSeconds(2));

                    EasyApplyProcess(driver);


                    try
                    {
                        ((IJavaScriptExecutor)driver).ExecuteScript("window.open('', '_self').close();");
                        ((IJavaScriptExecutor)driver).ExecuteScript("window.open('', '_self').close();");
                        Console.WriteLine("✅ Tab closed via JS");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Failed to close via JS: {ex.Message}");
                    }

                    driver.SwitchTo().Window(originalWindow);
                }
            }

            Thread.Sleep(TimeSpan.FromSeconds(3));

            SvgClickHelper.ClickSvgAncestorButton2(driver, (++pageNumber).ToString());


            Thread.Sleep(TimeSpan.FromSeconds(3));

            goto next;
        }
        catch (Exception ex)
        {
        }
        finally
        {
            Thread.Sleep(TimeSpan.FromSeconds(5));
            driver.SwitchTo().Window(originalWindow);
        }
    }

    private static void EasyApplyProcess(IWebDriver driver)
    {
        WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));


        string currentUrl = driver.Url;

        var diceEasyApplyHelper = new DiceEasyApplyHelper(driver);
        diceEasyApplyHelper.ClickEasyApplyButton(currentUrl);

    }

    public static IEnumerable<Func<object?>> GetApplyButtons(IWebDriver driver)
    {
        var buttons =
            driver.FindElements(
                By.XPath("//span[normalize-space()='Easy Apply']/ancestor::*[self::button or self::a]"));
        foreach (var button in buttons)
        {
            if (button.Displayed && button.Enabled)
            {
                yield return () => SafeClick(driver, button);
            }
        }
    }


    public static void DoIt()
    {
        try
        {
            // Set up logger
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<CookieDecryptionHelper>();

            // Set up your ChromeDriver instance
            //var options = new ChromeOptions();
            //options.AddArgument("--headless");

            var options = new EdgeOptions();
            options.AddArgument("user-data-dir=C:\\Users\\peter\\AppData\\Local\\Microsoft\\Edge\\User Data");
            options.AddArgument("profile-directory=Profile 2");
            IWebDriver driver = new EdgeDriver(options);
            driver.Navigate().GoToUrl("https://www.dice.com");
            logger.LogInformation($"Page Title: {driver.Title}");

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            var searchBox = wait.Until(d => d.FindElement(By.Name("q")));

            searchBox.Clear();
            searchBox.SendKeys("Python developer");

            wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            var locationBox = wait.Until(d => d.FindElement(By.Name("location")));

            locationBox.Clear();
            locationBox.SendKeys("New York, NY, USA");

            // Wait for button with inner text 'Search'
            wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            var searchButton = wait.Until(d =>
                d.FindElement(By.XPath("//span[contains(., 'Search')]/ancestor::button"))
            );

            searchButton.Click();


            DoSomeStuff(driver);

            ClickFirstEasyApply(driver);


            driver.Quit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
/*
 * 
 * // Store original window
   string originalWindow = driver.CurrentWindowHandle;
   
   // Click the link (opens a new tab)
   element.Click();
   
   // Wait for a new window to appear
   new WebDriverWait(driver, TimeSpan.FromSeconds(10)).Until(d => d.WindowHandles.Count > 1);
   
   // Switch to the new window
   foreach (var handle in driver.WindowHandles)
   {
   if (handle != originalWindow)
   {
   driver.SwitchTo().Window(handle);
   break;
   }
   }
   
   // You are now in the new tab
   Console.WriteLine("✅ Switched to new tab: " + driver.Url);

*/
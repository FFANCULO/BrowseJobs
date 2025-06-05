using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using PuppeteerSharp;

namespace BrowseJobs;

public static class CookieBridge
{
    public static async Task<IWebDriver> LaunchEdgeWithCookiesAsync(string url)
    {
        // Connect to running Edge via DevTools (Edge must be started with --remote-debugging-port)
        var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserURL = "http://localhost:9222"
        });

        var page = await browser.NewPageAsync();
        await page.GoToAsync(url);

        // Get cookies using CDP
        var client = page.Client; // No more Target.CreateCDPSessionAsync()
        dynamic cookieData = await client.SendAsync("Network.getAllCookies") ?? new JsonElement();

        await browser.CloseAsync();

        //// Launch Selenium EdgeDriver
        //var options = new EdgeOptions();
        //// options.UseChromium = true; // no longer needed

        //// Optional tuning
        //options.AddArgument("--disable-blink-features=AutomationControlled");
        //options.AddExcludedArgument("enable-automation");

        var driver = LaunchIsolatedEdgeDriver();
        await driver.Navigate().GoToUrlAsync(url); // Navigate first to set domain context

        // Inject cookies
        InjectCookies(cookieData, driver);

        await driver.Navigate().RefreshAsync(); // Refresh to apply session cookies
        return driver;
    }

    private static void InjectCookies(dynamic cookieData, IWebDriver driver)
    {
        foreach (var cookie in cookieData.cookies)
        {
            try
            {
                var seleniumCookie = new Cookie(
                    (string)cookie.name,
                    (string)cookie.value,
                    (string)cookie.domain?.TrimStart('.')!,
                    (string)cookie.path,
                    cookie.expires != null
                        ? (DateTime?)DateTimeOffset.FromUnixTimeSeconds((long)cookie.expires).UtcDateTime
                        : null,
                    (bool?)cookie.secure ?? false,
                    (bool?)cookie.httpOnly ?? false,
                    (string)cookie.sameSite ?? null
                );
                driver.Manage().Cookies.AddCookie(seleniumCookie);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Skipping cookie {cookie.name}: {ex.Message}");
            }
        }
    }

    public static IWebDriver LaunchIsolatedEdgeDriver(int maxRetries = 3, bool deleteUserDirAfterUse = false)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            string tempUserDataDir = Path.Combine(Path.GetTempPath(), $"EdgeProfile_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempUserDataDir);

            try
            {
                var options = new EdgeOptions();
                options.AddArgument($"--user-data-dir={tempUserDataDir}");
                options.AddArgument("--profile-directory=Default");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-dev-shm-usage");
                options.AddArgument("--disable-gpu");
                options.AddArgument("--window-size=1920,1080");
                options.AddArgument("--disable-blink-features=AutomationControlled");
                options.AddExcludedArgument("enable-automation");
                options.AddAdditionalOption("useAutomationExtension", false);

                Console.WriteLine($"[+] Launching Edge with isolated profile at: {tempUserDataDir}");
                var driver = new EdgeDriver(options);

                if (deleteUserDirAfterUse)
                {
                    AppDomain.CurrentDomain.ProcessExit += (_, __) =>
                    {
                        try { Directory.Delete(tempUserDataDir, true); }
                        catch { /* ignored */ }
                    };
                }

                return driver;
            }
            catch (WebDriverException ex)
            {
                Console.WriteLine($"[!] Attempt {attempt}: Failed to start EdgeDriver. {ex.Message}");

                try { Directory.Delete(tempUserDataDir, true); } catch { /* ignored */ }

                if (attempt == maxRetries)
                    throw new InvalidOperationException("All EdgeDriver launch attempts failed.", ex);
            }
        }

        throw new InvalidOperationException("Unreachable code hit during EdgeDriver launch.");
    }
}
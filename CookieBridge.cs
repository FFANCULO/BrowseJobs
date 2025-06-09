using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using PuppeteerSharp;

namespace BrowseJobs;

public static class CookieBridge
{
    public static async Task<IWebDriver> LaunchEdgeWithCookiesAsync(string url, Process? process)
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

        process?.Kill(true);
        process?.WaitForExit(TimeSpan.FromMinutes(20));
        process?.Dispose();
        process = null;

        await browser.CloseAsync();
        browser.Dispose();


        if (cookieData is JsonElement element && element.TryGetProperty("cookies", out var cookies))
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            // Serialize the "cookies" property to JSON
            string json = JsonSerializer.Serialize(cookies, options);
            
        }


        return null;
        



        //// Launch Selenium EdgeDriver
        //var options = new EdgeOptions();
        //// options.UseChromium = true; // no longer needed

        //// Optional tuning
        //options.AddArgument("--disable-blink-features=AutomationControlled");
        //options.AddExcludedArgument("enable-automation");

        var (driver, service) = LaunchIsolatedEdgeDriver();
        await driver.Navigate().GoToUrlAsync(url); // Navigate first to set domain context

        // Inject cookies
        InjectCookies(cookieData, driver);

        await driver.Navigate().RefreshAsync(); // Refresh to apply session cookies
        return driver;
    }

    private static void InjectCookies(dynamic cookieData, IWebDriver driver)
    {
        foreach (var cookie in cookieData.cookies)
            try
            {
                var seleniumCookie = new Cookie(
                    (string)cookie.name,
                    (string)cookie.value,
                    (string)cookie.domain?.TrimStart('.')!,
                    (string)cookie.path,
                    cookie.expires != null
                        ? DateTimeOffset.FromUnixTimeSeconds((long)cookie.expires).UtcDateTime
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

    public static (IWebDriver Driver, EdgeDriverService Service) LaunchIsolatedEdgeDriver(bool cleanUpOnExit = true)
    {
        foreach (var proc in Process.GetProcessesByName("msedge"))
        {
            proc.Kill(true);
            proc.WaitForExit(TimeSpan.FromSeconds(20));
        }
            
        foreach (var proc in Process.GetProcessesByName("msedgedriver"))
        {
            proc.Kill(true);
            proc.WaitForExit(TimeSpan.FromSeconds(20));
        }

        Thread.Sleep(2000); // allow OS to settle

        return (null, null);

        string tempUserDataDir = Path.Combine(Path.GetTempPath(), "EdgeTemp_" + Guid.NewGuid());
        Directory.CreateDirectory(tempUserDataDir);


        var options = new EdgeOptions();
        options.AddArgument("--disable-features=EdgeLocalModelSupport");

        options.AddArgument($"--user-data-dir={tempUserDataDir}");
        options.AddArgument("--profile-directory=Default");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--window-size=1920,1080");
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);

        var service = EdgeDriverService.CreateDefaultService();
        service.HideCommandPromptWindow = true;

        IWebDriver driver = new EdgeDriver(options);
        Console.WriteLine($"[+] Edge launched with temp profile: {tempUserDataDir}");

        if (cleanUpOnExit)
            AppDomain.CurrentDomain.ProcessExit += (_, __) =>
            {
                try
                {
                    driver.Quit();
                    service.Dispose();
                    Directory.Delete(tempUserDataDir, true);
                    Console.WriteLine($"[-] Cleaned up Edge temp profile at: {tempUserDataDir}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Cleanup failed: {ex.Message}");
                }
            };

        return (driver, service);
    }
}
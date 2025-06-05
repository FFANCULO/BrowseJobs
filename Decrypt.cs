using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using BrowseJobs;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;

// ReSharper disable All

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
        // Begin Shizer
        var wait1 = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

        // Click the toggle button
        var toggleButton = wait1.Until(d => d.FindElement(By.Id("descriptionToggle")));
        toggleButton.Click();

        // Wait for the description container to load
        var desc = wait1.Until(d => d.FindElement(By.CssSelector("div[data-testid='jobDescriptionHtml']")));

        // Extract visible plain text
        string jobText = desc.Text;
        Console.WriteLine("----- JOB DESCRIPTION -----\n");
        Console.WriteLine(jobText);

        IGrokApiClient apiClient = new GrokApiClient();
        ResumeBuilder builder = new ResumeBuilder(apiClient);
        builder.WithJobRequirements(jobText);
        var resumeBuilder = builder.ExtractKeywordsAsync().Result;


        // END Shizer

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
            var logger = loggerFactory.CreateLogger<Prog2>();

            // Set up your ChromeDriver instance
            //var options = new ChromeOptions();
            //options.AddArgument("--headless");
            var proxy = new Proxy
            {
                Kind = ProxyKind.Manual,
                IsAutoDetect = false,
                HttpProxy = "198.23.239.134:6540",
                SslProxy = "198.23.239.134:6540"
            };

            var options = new EdgeOptions() { Proxy = proxy };
            string tempUserDataDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            options.AddArgument($"--user-data-dir={tempUserDataDir}");

            // options.AddArgument("user-data-dir=C:\\Users\\peter\\AppData\\Local\\Microsoft\\Edge\\User Data");
            options.AddArgument("profile-directory=Profile 2");


            // PCJ
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            //// Additional options for better compatibility
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);


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

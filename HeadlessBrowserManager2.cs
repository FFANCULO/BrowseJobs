using System;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.IO;
using System.Collections.Generic;

namespace HeadlessBrowser2
{
    public class HeadlessBrowserManager2 : IDisposable
    {
        private IWebDriver driver;
        private WebDriverWait wait;

        public HeadlessBrowserManager2(int timeoutSeconds = 30)
        {
            InitializeDriver();
            wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        }

        private void InitializeDriver()
        {
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

            driver = new ChromeDriver(options);
        }

        // Navigation methods
        public void NavigateTo(string url)
        {
            try
            {
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }
                driver.Navigate().GoToUrl(url);
                Console.WriteLine($"Navigated to: {url}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Navigation failed: {ex.Message}");
            }
        }

        public void GoBack()
        {
            driver.Navigate().Back();
        }

        public void GoForward()
        {
            driver.Navigate().Forward();
        }

        public void Refresh()
        {
            driver.Navigate().Refresh();
        }

        // Element interaction methods
        public void ClickElement(string selector, By selectorType = null)
        {
            try
            {
                var element = FindElement(selector, selectorType);
                element.Click();
                Console.WriteLine($"Clicked element: {selector}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Click failed: {ex.Message}");
            }
        }

        public void EnterText(string selector, string text, By selectorType = null, bool clearFirst = true)
        {
            try
            {
                var element = FindElement(selector, selectorType);
                if (clearFirst)
                {
                    element.Clear();
                }
                element.SendKeys(text);
                Console.WriteLine($"Entered text '{text}' into: {selector}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Text entry failed: {ex.Message}");
            }
        }

        public void SelectDropdownOption(string selector, string optionText, By selectorType = null)
        {
            try
            {
                var element = FindElement(selector, selectorType);
                var select = new SelectElement(element);
                select.SelectByText(optionText);
                Console.WriteLine($"Selected '{optionText}' from dropdown: {selector}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dropdown selection failed: {ex.Message}");
            }
        }

        public void SubmitForm(string formSelector, By selectorType = null)
        {
            try
            {
                var form = FindElement(formSelector, selectorType);
                form.Submit();
                Console.WriteLine($"Submitted form: {formSelector}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Form submission failed: {ex.Message}");
            }
        }

        // Information retrieval methods
        public string GetPageTitle()
        {
            return driver.Title;
        }

        public string GetCurrentUrl()
        {
            return driver.Url;
        }

        public string GetElementText(string selector, By selectorType = null)
        {
            try
            {
                var element = FindElement(selector, selectorType);
                return element.Text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get text failed: {ex.Message}");
                return string.Empty;
            }
        }

        public string GetElementAttribute(string selector, string attributeName, By selectorType = null)
        {
            try
            {
                var element = FindElement(selector, selectorType);
                return element.GetAttribute(attributeName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get attribute failed: {ex.Message}");
                return string.Empty;
            }
        }

        public List<string> GetAllElementsText(string selector, By selectorType = null)
        {
            var texts = new List<string>();
            try
            {
                var elements = FindElements(selector, selectorType);
                foreach (var element in elements)
                {
                    texts.Add(element.Text);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get all elements text failed: {ex.Message}");
            }
            return texts;
        }

        // Wait methods
        public void WaitForElement(string selector, By selectorType = null)
        {
            try
            {
                var by = GetBySelector(selector, selectorType);
                wait.Until(driver => driver.FindElement(by));
                Console.WriteLine($"Element found: {selector}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Wait for element failed: {ex.Message}");
            }
        }

        public void WaitForElementToBeClickable(string selector, By selectorType = null)
        {
            try
            {
                var by = GetBySelector(selector, selectorType);
                wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(by);
                        return element.Enabled && element.Displayed;
                    }
                    catch (NoSuchElementException)
                    {
                        return false;
                    }
                });
                Console.WriteLine($"Element is clickable: {selector}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Wait for clickable failed: {ex.Message}");
            }
        }

        public void WaitForPageLoad(int seconds = 5)
        {
            Task.Delay(TimeSpan.FromSeconds(seconds)).Wait();
        }

        public void WaitForElementToBeVisible(string selector, By selectorType = null)
        {
            try
            {
                var by = GetBySelector(selector, selectorType);
                wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(by);
                        return element.Displayed;
                    }
                    catch (NoSuchElementException)
                    {
                        return false;
                    }
                });
                Console.WriteLine($"Element is visible: {selector}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Wait for visible failed: {ex.Message}");
            }
        }

        public void WaitForElementToDisappear(string selector, By selectorType = null)
        {
            try
            {
                var by = GetBySelector(selector, selectorType);
                wait.Until(driver =>
                {
                    try
                    {
                        driver.FindElement(by);
                        return false; // Element still exists
                    }
                    catch (NoSuchElementException)
                    {
                        return true; // Element is gone
                    }
                });
                Console.WriteLine($"Element has disappeared: {selector}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Wait for disappear failed: {ex.Message}");
            }
        }

        public void WaitForTextToBe(string selector, string expectedText, By selectorType = null)
        {
            try
            {
                var by = GetBySelector(selector, selectorType);
                wait.Until(driver =>
                {
                    try
                    {
                        var element = driver.FindElement(by);
                        return element.Text.Equals(expectedText, StringComparison.OrdinalIgnoreCase);
                    }
                    catch (NoSuchElementException)
                    {
                        return false;
                    }
                });
                Console.WriteLine($"Text matches expected: {expectedText}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Wait for text failed: {ex.Message}");
            }
        }

        // Screenshot and debugging
        public void TakeScreenshot(string filePath)
        {
            try
            {
                var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
                screenshot.SaveAsFile(filePath);
                Console.WriteLine($"Screenshot saved: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Screenshot failed: {ex.Message}");
            }
        }

        public string GetPageSource()
        {
            return driver.PageSource;
        }

        public void ExecuteJavaScript(string script)
        {
            try
            {
                ((IJavaScriptExecutor)driver).ExecuteScript(script);
                Console.WriteLine("JavaScript executed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JavaScript execution failed: {ex.Message}");
            }
        }

        // Helper methods
        private IWebElement FindElement(string selector, By selectorType = null)
        {
            var by = GetBySelector(selector, selectorType);
            return driver.FindElement(by);
        }

        private System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> FindElements(string selector, By selectorType = null)
        {
            var by = GetBySelector(selector, selectorType);
            return driver.FindElements(by);
        }

        private By GetBySelector(string selector, By selectorType = null)
        {
            if (selectorType != null)
                return selectorType;

            // Auto-detect selector type
            if (selector.StartsWith("#"))
                return By.Id(selector.Substring(1));
            if (selector.StartsWith("."))
                return By.ClassName(selector.Substring(1));
            if (selector.Contains("[") && selector.Contains("]"))
                return By.CssSelector(selector);
            if (selector.StartsWith("//"))
                return By.XPath(selector);
            return By.CssSelector(selector);
        }

        public void Dispose()
        {
            try
            {
                driver?.Quit();
                driver?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Disposal error: {ex.Message}");
            }
        }
    }

    // Example usage class
    //public class Program
    //{
    //    public static async Task Main(string[] args)
    //    {
    //        using var browser = new HeadlessBrowserManager2();

    //        try
    //        {
    //            // Navigate to your website
    //            browser.NavigateTo("mywebsite.com");

    //            // Wait for page to load
    //            browser.WaitForPageLoad(3);

    //            Console.WriteLine($"Page Title: {browser.GetPageTitle()}");
    //            Console.WriteLine($"Current URL: {browser.GetCurrentUrl()}");

    //            // Example interactions - adjust selectors based on your website

    //            // Click a button by ID
    //            browser.ClickElement("#submit-button");

    //            // Enter text into a form field
    //            browser.EnterText("#username", "your-username");
    //            browser.EnterText("#password", "your-password");

    //            // Click submit button
    //            browser.ClickElement("input[type='submit']");

    //            // Wait for navigation or response
    //            browser.WaitForPageLoad(5);

    //            // Take a screenshot
    //            browser.TakeScreenshot("screenshot.png");

    //            // Get text from an element
    //            string resultText = browser.GetElementText(".result-message");
    //            Console.WriteLine($"Result: {resultText}");

    //            // Example of more complex interactions
    //            await DemoInteractions(browser);
    //        }
    //        catch (Exception ex)
    //        {
    //            Console.WriteLine($"Error: {ex.Message}");
    //        }
    //    }

    //    private static async Task DemoInteractions(HeadlessBrowserManager2 browser)
    //    {
    //        // Example: Login form
    //        Console.WriteLine("=== Login Example ===");
    //        browser.WaitForElement("#login-form");
    //        browser.EnterText("input[name='email']", "user@example.com");
    //        browser.EnterText("input[name='password']", "password123");
    //        browser.ClickElement("button[type='submit']");

    //        // Example: Search functionality
    //        Console.WriteLine("=== Search Example ===");
    //        browser.WaitForElement(".search-box");
    //        browser.EnterText(".search-box", "search query");
    //        browser.ClickElement(".search-button");

    //        // Example: Form with dropdown
    //        Console.WriteLine("=== Dropdown Example ===");
    //        browser.SelectDropdownOption("#country-select", "United States");

    //        // Example: Checkbox/Radio button
    //        Console.WriteLine("=== Checkbox Example ===");
    //        browser.ClickElement("input[type='checkbox'][name='terms']");

    //        // Example: Getting multiple elements
    //        Console.WriteLine("=== Get Multiple Elements ===");
    //        var productNames = browser.GetAllElementsText(".product-name");
    //        foreach (var name in productNames)
    //        {
    //            Console.WriteLine($"Product: {name}");
    //        }

    //        // Example: JavaScript execution
    //        Console.WriteLine("=== JavaScript Example ===");
    //        browser.ExecuteJavaScript("window.scrollTo(0, document.body.scrollHeight);");

    //        await Task.Delay(2000); // Wait 2 seconds

    //        // Example: Advanced waiting
    //        browser.WaitForElementToBeClickable("#dynamic-button");
    //        browser.ClickElement("#dynamic-button");
    //    }
    //}
}
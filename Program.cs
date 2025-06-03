// See https://aka.ms/new-console-template for more information

using System;
using System.Threading.Tasks;
using HeadlessBrowser2;

namespace BrowseJobs
{
     public class Program
    {
        public static async Task Main(string[] args)
        {
            var result = await xAI.CallGrok();

            // Shit.DoIt();
             Prog2.DoIt();


           // var extractCookies = CookieExtractor.ExtractCookies();

            // await NewMethod(out var resultText);
        }

        private static async Task NewMethod(string resultText)
        {
            using var browser = new HeadlessBrowserManager2();

            try
            {
                // Navigate to your website
                browser.NavigateTo("dice.com");

                // Wait for page to load
                browser.WaitForPageLoad(3);

                Console.WriteLine($"Page Title: {browser.GetPageTitle()}");
                Console.WriteLine($"Current URL: {browser.GetCurrentUrl()}");

                // Example interactions - adjust selectors based on your website

                // Click a button by ID
                browser.ClickElement("#submit-button");

                // Enter text into a form field
                browser.EnterText("#username", "your-username");
                browser.EnterText("#password", "your-password");

                // Click submit button
                browser.ClickElement("input[type='submit']");

                // Wait for navigation or response
                browser.WaitForPageLoad(5);

                // Take a screenshot
                browser.TakeScreenshot("screenshot.png");

                // Get text from an element
                resultText = browser.GetElementText(".result-message");
                Console.WriteLine($"Result: {resultText}");

                // Example of more complex interactions
                await DemoInteractions(browser);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static async Task DemoInteractions(HeadlessBrowserManager2 browser)
        {
            // Example: Login form
            Console.WriteLine("=== Login Example ===");
            browser.WaitForElement("#login-form");
            browser.EnterText("input[name='email']", "user@example.com");
            browser.EnterText("input[name='password']", "password123");
            browser.ClickElement("button[type='submit']");

            // Example: Search functionality
            Console.WriteLine("=== Search Example ===");
            browser.WaitForElement(".search-box");
            browser.EnterText(".search-box", "search query");
            browser.ClickElement(".search-button");

            // Example: Form with dropdown
            Console.WriteLine("=== Dropdown Example ===");
            browser.SelectDropdownOption("#country-select", "United States");

            // Example: Checkbox/Radio button
            Console.WriteLine("=== Checkbox Example ===");
            browser.ClickElement("input[type='checkbox'][name='terms']");

            // Example: Getting multiple elements
            Console.WriteLine("=== Get Multiple Elements ===");
            var productNames = browser.GetAllElementsText(".product-name");
            foreach (var name in productNames)
            {
                Console.WriteLine($"Product: {name}");
            }

            // Example: JavaScript execution
            Console.WriteLine("=== JavaScript Example ===");
            browser.ExecuteJavaScript("window.scrollTo(0, document.body.scrollHeight);");

            await Task.Delay(2000); // Wait 2 seconds

            // Example: Advanced waiting
            browser.WaitForElementToBeClickable("#dynamic-button");
            browser.ClickElement("#dynamic-button");
        }
    }
}
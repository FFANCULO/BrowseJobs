using System;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace BrowseJobs;

public class DiceEasyApplyHelper
{
    public IWebDriver Driver { get; }

    public DiceEasyApplyHelper(IWebDriver driver)
    {
        Driver = driver ?? throw new ArgumentNullException(nameof(driver), "WebDriver instance cannot be null.");
    }

    public bool ClickEasyApplyButton(string jobUrl)
    {
        try
        {
            // Validate URL
            if (string.IsNullOrEmpty(jobUrl))
            {
                Console.WriteLine("Error: Job URL is empty or null.");
                return false;
            }

          

            // Extract job-id from the URL
            string jobId = ExtractJobIdFromUrl(Driver.Url);
            if (string.IsNullOrEmpty(jobId))
            {
                Console.WriteLine("Error: Could not extract job-id from URL.");
                return false;
            }
            Console.WriteLine($"Extracted job-id: {jobId}");

            // Wait for the page to load
            WebDriverWait wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));

            // Locate the Easy Apply button using dynamic CSS selector
            string cssSelector = $"apply-button-wc[job-id='{jobId}']";
            IWebElement applyButton;

            try
            {
                // Wait until the button is present and clickable
                applyButton = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.CssSelector(cssSelector)));
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("Error: Easy Apply button not found or not clickable within the timeout period.");
                return false;
            }

            // Scroll to the button to ensure it's in view
            ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].scrollIntoView(true);", applyButton);

            // Attempt to click the Easy Apply button
            try
            {
                applyButton.Click();
                Console.WriteLine("Successfully clicked the Easy Apply button.");
            }
            catch (ElementClickInterceptedException)
            {
                // Fallback: Use JavaScript to click if direct click fails
                try
                {
                    ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].click();", applyButton);
                    Console.WriteLine("Clicked the Easy Apply button using JavaScript fallback.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error clicking the Easy Apply button: {ex.Message}");
                    return false;
                }
            }

            // Wait briefly to observe the result
            System.Threading.Thread.Sleep(2000);

            // Check for potential reCAPTCHA
            try
            {
                var recaptcha = Driver.FindElement(By.ClassName("grecaptcha-badge"));
                if (recaptcha.Displayed)
                {
                    Console.WriteLine("reCAPTCHA detected. Manual intervention may be required to proceed with the application.");
                }
            }
            catch (NoSuchElementException)
            {
                Console.WriteLine("No reCAPTCHA detected. Application process likely initiated.");
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }

    private string ExtractJobIdFromUrl(string url)
    {
        try
        {
            // Assuming URL format: https://www.dice.com/job-detail/{job-id}
            Uri uri = new Uri(url);
            string[] segments = uri.Segments;
            if (segments.Length > 0)
            {
                string jobId = segments[^1].TrimEnd('/');
                // Validate job-id format (UUID: 8-4-4-4-12 characters)
                if (Regex.IsMatch(jobId, @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"))
                {
                    return jobId;
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
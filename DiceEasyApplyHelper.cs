using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace BrowseJobs;

public class DiceEasyApplyHelper
{
    public DiceEasyApplyHelper(IWebDriver driver)
    {
        Driver = driver ?? throw new ArgumentNullException(nameof(driver), "WebDriver instance cannot be null.");
    }

    public IWebDriver Driver { get; }

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

            var jobId = ExtractJobIdFromUrl(Driver.Url);
            if (string.IsNullOrEmpty(jobId))
            {
                Console.WriteLine("Error: Could not extract job-id from URL.");
                return false;
            }

            Console.WriteLine($"Extracted job-id: {jobId}");

            // Wait for the page to load
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));

            // Locate the Easy Apply button using dynamic CSS selector
            var cssSelector = $"apply-button-wc[job-id='{jobId}']";
            IWebElement applyButton;

            try
            {
                // Wait until the button is present and clickable
                applyButton = wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector(cssSelector)));
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
                var applyButtonText = applyButton.Text;

                applyButton.Click();
                Thread.CurrentThread.HumanPause();
                Console.WriteLine($"{applyButtonText} Successfully clicked the Easy Apply button.");
                if (applyButtonText.Contains("Submitted"))
                    return true;
            }
            catch (ElementClickInterceptedException)
            {
                // Fallback: Use JavaScript to click if direct click fails
                try
                {
                    ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].click();", applyButton);
                    Thread.CurrentThread.HumanPause();
                    Console.WriteLine("Clicked the Easy Apply button using JavaScript fallback.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error clicking the Easy Apply button: {ex.Message}");
                    return false;
                }
            }

            // Wait briefly to observe the result

    
            var webButtonHelper = new WebButtonHelper(Driver);
            webButtonHelper.ClickNextButton();
            Thread.CurrentThread.HumanPause();

            File.WriteAllText("page_dump1.html", Driver.PageSource);



            var webButtonHelper2 = new WebButtonHelper2(Driver);
            webButtonHelper2.ClickSubmitButton();
            Thread.CurrentThread.HumanPause();



            // Check for potential reCAPTCHA
            try
            {
                var recaptcha = Driver.FindElement(By.ClassName("grecaptcha-badge"));
                if (recaptcha.Displayed)
                    Console.WriteLine(
                        "reCAPTCHA detected. Manual intervention may be required to proceed with the application.");
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
            var uri = new Uri(url);
            var segments = uri.Segments;
            if (segments.Length > 0)
            {
                var jobId = segments[^1].TrimEnd('/');
                // Validate job-id format (UUID: 8-4-4-4-12 characters)
                if (Regex.IsMatch(jobId,
                        @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")) return jobId;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
using System;
using System.IO;
using System.Linq;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace BrowseJobs
{
    public class WebButtonHelper2
    {
        private readonly IWebDriver _driver;
        private readonly WebDriverWait _wait;
        private readonly string _logFilePath;

        /// <summary>
        /// Initializes a new instance of the WebButtonHelper class.
        /// </summary>
        /// <param name="driver">The Selenium WebDriver instance.</param>
        /// <param name="timeoutInSeconds">Timeout for waiting operations in seconds (default is 15).</param>
        /// <param name="logFilePath">Path for logging (optional, defaults to console if null).</param>
        public WebButtonHelper2(IWebDriver driver, int timeoutInSeconds = 15, string logFilePath = null)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutInSeconds));
            _logFilePath = logFilePath;
        }

        /// <summary>
        /// Clicks a button with multiple selector fallbacks, retry logic, and form validation.
        /// Supports both CSS and XPath selectors for flexibility.
        /// </summary>
        /// <param name="primarySelector">Primary CSS or XPath selector for the button.</param>
        /// <param name="fallbackSelectors">Fallback CSS or XPath selectors if primary fails.</param>
        /// <param name="isXPath">Whether the selectors are XPath (true) or CSS (false).</param>
        /// <param name="retryCount">Number of retry attempts for stale elements.</param>
        /// <param name="validateForm">Whether to check form validation before clicking.</param>
        /// <returns>True if the click was successful, false otherwise.</returns>
        public bool ClickButton(
            string primarySelector = "button[type='submit']",
            string[] fallbackSelectors = null,
            bool isXPath = false,
            int retryCount = 3,
            bool validateForm = true)
        {
            var selectors = new[] { primarySelector }.Concat(fallbackSelectors ?? new string[0]).ToList();
            int attempts = 0;

            while (attempts < retryCount)
            {
                foreach (var selector in selectors)
                {
                    try
                    {
                        // Wait until the button is visible and clickable
                        var by = isXPath ? By.XPath(selector) : By.CssSelector(selector);
                        var button = _wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(by));

                        // Validate button state
                        if (button.Enabled && button.Displayed)
                        {
                            // Check for reCAPTCHA
                            if (IsRecaptchaPresent())
                            {
                                Log("Warning: reCAPTCHA detected. Automated click may be blocked.");
                                return false;
                            }

                            // Validate form if required
                            if (validateForm && !IsFormValid())
                            {
                                Log("Form validation failed. Required fields may be missing.");
                                return false;
                            }

                            // Scroll to the button
                            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({ behavior: 'smooth', block: 'center' });", button);

                            // Click the button
                            button.Click();
                            Log($"Successfully clicked the button using selector: {selector}");
                            return true;
                        }
                        else
                        {
                            Log($"Button with selector '{selector}' is disabled or not displayed.");
                            continue;
                        }
                    }
                    catch (StaleElementReferenceException)
                    {
                        Log($"Stale element detected for selector '{selector}'. Retrying...");
                        attempts++;
                        Thread.Sleep(1000); // Brief pause before retry
                        continue;
                    }
                    catch (WebDriverTimeoutException)
                    {
                        Log($"Timeout: Button with selector '{selector}' not found or not clickable.");
                        continue;
                    }
                    catch (WebDriverException ex)
                    {
                        Log($"Error clicking button with selector '{selector}': {ex.Message}");
                        continue;
                    }
                }

                attempts++;
                if (attempts < retryCount)
                {
                    Log($"Retrying click attempt {attempts + 1}/{retryCount}...");
                    Thread.Sleep(1000);
                }
            }

            Log("Failed to click the button after all attempts.");
            return false;
        }

        /// <summary>
        /// Checks if the button is present and clickable.
        /// </summary>
        /// <param name="selector">CSS or XPath selector for the button.</param>
        /// <param name="isXPath">Whether the selector is XPath (true) or CSS (false).</param>
        /// <returns>True if the button is present and clickable, false otherwise.</returns>
        public bool IsButtonClickable(string selector = "button[type='submit']", bool isXPath = false)
        {
            try
            {
                var by = isXPath ? By.XPath(selector) : By.CssSelector(selector);
                var button = _wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(by));
                bool isClickable = button.Enabled && button.Displayed;
                Log($"Button with selector '{selector}' is {(isClickable ? "clickable" : "not clickable")}.");
                return isClickable;
            }
            catch (WebDriverTimeoutException)
            {
                Log($"Button with selector '{selector}' is not visible or not clickable.");
                return false;
            }
        }

        /// <summary>
        /// Checks if reCAPTCHA is present on the page.
        /// </summary>
        /// <returns>True if reCAPTCHA badge is detected, false otherwise.</returns>
        private bool IsRecaptchaPresent()
        {
            try
            {
                var recaptcha = _driver.FindElements(By.ClassName("grecaptcha-badge"));
                bool isPresent = recaptcha.Any() && recaptcha.First().Displayed;
                if (isPresent)
                {
                    Log("reCAPTCHA badge detected on the page.");
                }
                return isPresent;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates if the form has no visible validation errors (e.g., required fields).
        /// </summary>
        /// <returns>True if the form appears valid, false otherwise.</returns>
        private bool IsFormValid()
        {
            try
            {
                // Check for required fields that are empty
                var requiredFields = _driver.FindElements(By.CssSelector("input[required], textarea[required], select[required]"));
                foreach (var field in requiredFields)
                {
                    if (field.Displayed && string.IsNullOrEmpty(field.GetAttribute("value")))
                    {
                        Log($"Required field '{field.GetAttribute("name") ?? "unknown"}' is empty.");
                        return false;
                    }
                }

                // Check for visible error messages
                var errorMessages = _driver.FindElements(By.CssSelector(".seds-inline-message.error, .error, [class*='error']"));
                if (errorMessages.Any(e => e.Displayed))
                {
                    Log("Form contains visible error messages.");
                    return false;
                }

                Log("Form appears valid with no empty required fields or visible errors.");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error validating form: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Logs a message to the console and optionally to a file.
        /// </summary>
        /// <param name="message">The message to log.</param>
        private void Log(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            Console.WriteLine(logMessage);

            if (!string.IsNullOrEmpty(_logFilePath))
            {
                try
                {
                    File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write to log file: {ex.Message}");
                }
            }
        }
    }
}
using System;
using System.IO;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace BrowseJobs
{
    public class WebButtonHelper
    {
        public IWebDriver Driver { get; }
        private readonly WebDriverWait _wait;
        private readonly string _logFilePath;

        /// <summary>
        /// Initializes a new instance of the WebButtonHelper class.
        /// </summary>
        /// <param name="driver">The Selenium WebDriver instance.</param>
        /// <param name="timeoutInSeconds">Timeout for waiting operations in seconds (default is 10).</param>
        /// <param name="logFilePath">Path for logging (optional, defaults to console if null).</param>
        public WebButtonHelper(IWebDriver driver, int timeoutInSeconds = 10, string logFilePath = null)
        {
            Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutInSeconds));
            _logFilePath = logFilePath;
        }

        /// <summary>
        /// Clicks the "Next" button with multiple selector fallbacks and retry logic.
        /// </summary>
        /// <param name="primarySelector">Primary CSS selector for the button.</param>
        /// <param name="fallbackSelectors">Fallback CSS selectors if primary fails.</param>
        /// <param name="retryCount">Number of retry attempts for stale elements.</param>
        /// <returns>True if the click was successful, false otherwise.</returns>
        public bool ClickNextButton(
            string primarySelector = "button.seds-button-primary.btn-next",
            string[] fallbackSelectors = null,
            int retryCount = 3)
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
                        var button = _wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(By.CssSelector(selector)));

                        // Verify button state
                        if (button.Enabled && button.Displayed)
                        {
                            // Scroll to the button
                            ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].scrollIntoView({ behavior: 'smooth', block: 'center' });", button);

                            // Check for reCAPTCHA badge
                            if (IsRecaptchaPresent())
                            {
                                Log("Warning: reCAPTCHA detected. Automated click may be blocked.");
                                return false;
                            }

                            // Click the button
                            button.Click();
                            Log($"Successfully clicked the 'Next' button using selector: {selector}");
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
                    System.Threading.Thread.Sleep(1000); // Brief pause before retry
                }
            }

            Log("Failed to click the 'Next' button after all attempts.");
            return false;
        }

        /// <summary>
        /// Checks if the "Next" button is present and clickable.
        /// </summary>
        /// <param name="cssSelector">CSS selector for the button.</param>
        /// <returns>True if the button is present and clickable, false otherwise.</returns>
        public bool IsNextButtonClickable(string cssSelector = "button.seds-button-primary.btn-next")
        {
            try
            {
                var button = _wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.CssSelector(cssSelector)));
                bool isClickable = button.Enabled && button.Displayed;
                Log($"Button with selector '{cssSelector}' is {(isClickable ? "clickable" : "not clickable")}.");
                return isClickable;
            }
            catch (WebDriverTimeoutException)
            {
                Log($"Button with selector '{cssSelector}' is not visible or not clickable.");
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
                var recaptcha = Driver.FindElements(By.ClassName("grecaptcha-badge"));
                return recaptcha.Any() && recaptcha.First().Displayed;
            }
            catch
            {
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
                    File.AppendAllText(_logFilePath, $"{logMessage}{Environment.NewLine}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write to log file: {ex.Message}");
                }
            }
        }
    }
}
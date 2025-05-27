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
        public IWebDriver Driver { get; }
        private readonly WebDriverWait _wait;
        private readonly string _logFilePath;

        public WebButtonHelper2(IWebDriver driver, int timeoutInSeconds = 20, string logFilePath = null)
        {
            Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutInSeconds));
            _logFilePath = logFilePath;
        }

        public bool ClickSubmitButton(
            string primarySelector = "button.seds-button-primary.btn-next",
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
                        var by = isXPath ? By.XPath(selector) : By.CssSelector(selector);
                        var button = _wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(by));

                        if (button.Enabled && button.Displayed)
                        {
                            if (IsRecaptchaPresent())
                            {
                                Log("Warning: reCAPTCHA detected. Pausing for manual completion. Press Enter when done.");
                                Console.ReadLine();
                            }

                            if (validateForm && !IsFormValid())
                            {
                                Log("Form validation failed. Required fields may be missing.");
                                return false;
                            }

                            ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].scrollIntoView({ behavior: 'smooth', block: 'center' });", button);
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
                        Thread.Sleep(1000);
                        continue;
                    }
                    catch (WebDriverTimeoutException)
                    {
                        Log($"Timeout: Button with selector '{selector}' not found or not clickable.");
                        CaptureScreenshot($"timeout_{selector.Replace(":", "_").Replace("/", "_")}");
                        LogDomState();
                        continue;
                    }
                    catch (WebDriverException ex)
                    {
                        Log($"Error clicking button with selector '{selector}': {ex.Message}");
                        CaptureScreenshot($"error_{selector.Replace(":", "_").Replace("/", "_")}");
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

        public bool IsButtonClickable(string selector = "button.seds-button-primary.btn-next", bool isXPath = false)
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

        private bool IsRecaptchaPresent()
        {
            try
            {
                var recaptcha = Driver.FindElements(By.CssSelector("div.grecaptcha-badge"));
                bool isPresent = recaptcha.Any() && recaptcha.FirstOrDefault()?.Displayed == true;
                if (isPresent)
                    Log("reCAPTCHA badge detected on the page.");
                return isPresent;
            }
            catch
            {
                return false;
            }
        }

        private bool IsFormValid()
        {
            try
            {
                var requiredFields = Driver.FindElements(By.CssSelector("input[required], textarea[required], select[required]"));
                foreach (var field in requiredFields)
                {
                    if (field.Displayed && string.IsNullOrEmpty(field.GetAttribute("value")))
                    {
                        Log($"Input field '{field.GetAttribute("name") ?? "unknown"}' is empty.");
                        return false;
                    }
                }

                var errorMessages = Driver.FindElements(By.CssSelector(".seds-inline-message.error, .error, [class*='error']"));
                if (errorMessages.Any(e => e.Displayed))
                {
                    Log("Form contains visible error messages.");
                    return false;
                }

                Log("Form submission ready.");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error validating form submission: {ex.Message}");
                return false;
            }
        }

        private void LogDomState()
        {
            try
            {
                var buttons = Driver.FindElements(By.XPath("//button"));
                Log("Listing all visible buttons in DOM:");
                foreach (var button in buttons)
                {
                    if (button.Displayed)
                    {
                        Log($"Button: Text='{button.Text}', Classes='{button.GetAttribute("class")}', Type='{button.GetAttribute("type")}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error logging DOM state: {ex.Message}");
            }
        }

        private void CaptureScreenshot(string name)
        {
            try
            {
                var screenshot = ((ITakesScreenshot)Driver).GetScreenshot();
                var fileName = $"screenshot_{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                screenshot.SaveAsFile(fileName);
                Log($"Screenshot saved: {fileName}");
            }
            catch (Exception ex)
            {
                Log($"Error capturing screenshot: {ex.Message}");
            }
        }

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
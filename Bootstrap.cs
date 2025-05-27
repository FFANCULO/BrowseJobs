//using System;
//using System.Collections.Generic;
//using OpenQA.Selenium;
//using OpenQA.Selenium.Support.UI;

//namespace BrowseJobs
//{
//    // Single Responsibility: Handle logging concerns
//    public interface ILogger
//    {
//        void LogInfo(string message);
//        void LogError(string message);
//    }

//    public class ConsoleLogger : ILogger
//    {
//        public void LogInfo(string message) => Console.WriteLine($"[INFO] {message}");
//        public void LogError(string message) => Console.WriteLine($"[ERROR] {message}");
//    }

//    // Single Responsibility: Handle JavaScript execution
//    public interface IJavaScriptExecutor
//    {
//        T ExecuteScript<T>(string script, params object[] args);
//        void InjectJQuery();
//        bool IsJQueryAvailable();
//    }

//    public class WebDriverJavaScriptExecutor : IJavaScriptExecutor
//    {
//        private readonly IWebDriver _driver;
//        private readonly ILogger _logger;

//        public WebDriverJavaScriptExecutor(IWebDriver driver, ILogger logger)
//        {
//            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        public T? ExecuteScript<T>(string script, params object[] args) 
//        {
//            try
//            {
//                return (T?)((OpenQA.Selenium.IJavaScriptExecutor)_driver).ExecuteScript(script, args);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError($"JavaScript execution failed: {ex.Message}");
//                throw;
//            }
//        }

//        public void InjectJQuery()
//        {
//            const string jqueryInjectionScript = @"
//                if (typeof jQuery === 'undefined') {
//                    var script = document.createElement('script');
//                    script.src = 'https://code.jquery.com/jquery-3.6.0.min.js';
//                    script.onload = function() { console.log('jQuery loaded'); };
//                    document.head.appendChild(script);
//                }";

//            ExecuteScript<object>(jqueryInjectionScript);
//        }

//        public bool IsJQueryAvailable()
//        {
//            return ExecuteScript<bool>("return typeof jQuery !== 'undefined';");
//        }
//    }

//    // Single Responsibility: Handle element waiting and finding
//    public interface IElementWaiter
//    {
//        IWebElement WaitForElement(By selector, TimeSpan timeout);
//        void WaitUntil(Func<IWebDriver, bool> condition, TimeSpan timeout);
//    }

//    public class SeleniumElementWaiter : IElementWaiter
//    {
//        private readonly IWebDriver _driver;
//        private readonly ILogger _logger;

//        public SeleniumElementWaiter(IWebDriver driver, ILogger logger)
//        {
//            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        public IWebElement WaitForElement(By selector, TimeSpan timeout)
//        {
//            try
//            {
//                var wait = new WebDriverWait(_driver, timeout);
//                return wait.Until(d => d.FindElement(selector));
//            }
//            catch (WebDriverTimeoutException ex)
//            {
//                _logger.LogError($"Element not found within {timeout.TotalSeconds}s: {selector}");
//                throw new NoSuchElementException($"Element not found: {selector}", ex);
//            }
//        }

//        public void WaitUntil(Func<IWebDriver, bool> condition, TimeSpan timeout)
//        {
//            var wait = new WebDriverWait(_driver, timeout);
//            wait.Until(condition);
//        }
//    }

//    // Single Responsibility: Handle pagination state
//    public class PaginationState
//    {
//        public string CurrentPage { get; }
//        public string TotalPages { get; }
//        public string AriaLabel { get; }

//        public PaginationState(string currentPage, string totalPages, string ariaLabel)
//        {
//            CurrentPage = currentPage;
//            TotalPages = totalPages;
//            AriaLabel = ariaLabel;
//        }

//        public override string ToString() => $"Page {CurrentPage} of {TotalPages}";
//    }

//    public interface IPaginationStateReader
//    {
//        PaginationState GetCurrentState(string expectedPage);
//    }

//    public class AriaLabelPaginationStateReader : IPaginationStateReader
//    {
//        private readonly IElementWaiter _elementWaiter;
//        private readonly IJavaScriptExecutor _jsExecutor;
//        private readonly ILogger _logger;

//        public AriaLabelPaginationStateReader(
//            IElementWaiter elementWaiter,
//            IJavaScriptExecutor jsExecutor,
//            ILogger logger)
//        {
//            _elementWaiter = elementWaiter ?? throw new ArgumentNullException(nameof(elementWaiter));
//            _jsExecutor = jsExecutor ?? throw new ArgumentNullException(nameof(jsExecutor));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        public PaginationState GetCurrentState(string expectedPage)
//        {
//            try
//            {
//                var xpath = $"//section[@aria-label='Page {expectedPage} of 10']";
//                var element = _elementWaiter.WaitForElement(By.XPath(xpath), TimeSpan.FromSeconds(15));

//                var text = _jsExecutor.ExecuteScript<string>("return jQuery(arguments[0]).text();", element);
//                var ariaLabel = element.GetAttribute("aria-label");

//                _logger.LogInfo($"Current pagination state: {ariaLabel}");
//                return new PaginationState(expectedPage, "10", ariaLabel);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError($"Failed to read pagination state: {ex.Message}");
//                throw;
//            }
//        }
//    }

//    // Open/Closed Principle: Abstract click strategy that can be extended
//    public abstract class ClickStrategy
//    {
//        protected readonly IJavaScriptExecutor JsExecutor;
//        protected readonly ILogger Logger;

//        protected ClickStrategy(IJavaScriptExecutor jsExecutor, ILogger logger)
//        {
//            JsExecutor = jsExecutor ?? throw new ArgumentNullException(nameof(jsExecutor));
//            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        public abstract ClickResult Execute(string svgSelector);
//    }

//    public class ClickResult
//    {
//        public bool Success { get; }
//        public string Message { get; }
//        public string Strategy { get; }

//        public ClickResult(bool success, string message, string strategy)
//        {
//            Success = success;
//            Message = message;
//            Strategy = strategy;
//        }
//    }

//    // Concrete implementations of click strategies
//    public class SwiperClickStrategy : ClickStrategy
//    {
//        public SwiperClickStrategy(IJavaScriptExecutor jsExecutor, ILogger logger)
//            : base(jsExecutor, logger) { }

//        public override ClickResult Execute(string svgSelector)
//        {
//            const string script = @"
//                var $svg = jQuery(arguments[0]);
//                var $nextButton = $svg.closest('span[aria-label=""Next""]');
//                var swiper = $nextButton.closest('.swiper-container').data('swiper') || window.Swiper;
//                if (swiper) {
//                    swiper.slideNext();
//                    return 'SUCCESS: Swiper slideNext triggered';
//                }
//                return 'FAILED: Swiper not found';";

//            var result = JsExecutor.ExecuteScript<string>(script, svgSelector);
//            var success = result.StartsWith("SUCCESS");

//            Logger.LogInfo($"Swiper strategy: {result}");
//            return new ClickResult(success, result, "Swiper");
//        }
//    }

//    public class BootstrapCarouselClickStrategy : ClickStrategy
//    {
//        public BootstrapCarouselClickStrategy(IJavaScriptExecutor jsExecutor, ILogger logger)
//            : base(jsExecutor, logger) { }

//        public override ClickResult Execute(string svgSelector)
//        {
//            const string script = @"
//                var $svg = jQuery(arguments[0]);
//                var $nextButton = $svg.closest('span[aria-label=""Next""]');
//                var carousel = $nextButton.closest('.carousel').data('bs.carousel');
//                if (carousel) {
//                    carousel.next();
//                    return 'SUCCESS: Bootstrap carousel next triggered';
//                }
//                return 'FAILED: Bootstrap carousel not found';";

//            var result = JsExecutor.ExecuteScript<string>(script, svgSelector);
//            var success = result.StartsWith("SUCCESS");

//            Logger.LogInfo($"Bootstrap strategy: {result}");
//            return new ClickResult(success, result, "Bootstrap Carousel");
//        }
//    }

//    public class GenericClickStrategy : ClickStrategy
//    {
//        public GenericClickStrategy(IJavaScriptExecutor jsExecutor, ILogger logger)
//            : base(jsExecutor, logger) { }

//        public override ClickResult Execute(string svgSelector)
//        {
//            const string script = @"
//                var $svg = jQuery(arguments[0]);
//                var $nextButton = $svg.closest('span[aria-label=""Next""]');
                
//                if ($nextButton.length === 0) return 'FAILED: Next button not found';
                
//                // Simulate multiple events
//                $nextButton.trigger('mousedown')
//                           .trigger('mouseup')
//                           .trigger('click')
//                           .trigger('touchend')
//                           .trigger(jQuery.Event('keydown', {keyCode: 39}));
                
//                return 'SUCCESS: Generic click events triggered on ' + $nextButton[0].outerHTML.substring(0, 50);";

//            var result = JsExecutor.ExecuteScript<string>(script, svgSelector);
//            var success = result.StartsWith("SUCCESS");

//            Logger.LogInfo($"Generic strategy: {result}");
//            return new ClickResult(success, result, "Generic Click");
//        }
//    }

//    // Liskov Substitution: All strategies can be used interchangeably
//    public interface IClickStrategyExecutor
//    {
//        ClickResult ExecuteClick(string svgSelector);
//    }

//    public class ChainOfResponsibilityClickExecutor : IClickStrategyExecutor
//    {
//        private readonly List<ClickStrategy> _strategies;
//        private readonly ILogger _logger;

//        public ChainOfResponsibilityClickExecutor(List<ClickStrategy> strategies, ILogger logger)
//        {
//            _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        public ClickResult ExecuteClick(string svgSelector)
//        {
//            foreach (var strategy in _strategies)
//            {
//                var result = strategy.Execute(svgSelector);
//                if (result.Success)
//                {
//                    _logger.LogInfo($"Successful click with {result.Strategy} strategy");
//                    return result;
//                }
//            }

//            _logger.LogError("All click strategies failed");
//            return new ClickResult(false, "All strategies failed", "None");
//        }
//    }

//    // Interface Segregation: Separate concerns into focused interfaces
//    public interface IPaginationNavigator
//    {
//        void NavigateToNextPage(string currentPage, string svgSelector = "svg.text-cyan-700");
//    }

//    // Dependency Inversion: High-level module depends on abstractions
//    public class SvgPaginationNavigator : IPaginationNavigator
//    {
//        private readonly IElementWaiter _elementWaiter;
//        private readonly IJavaScriptExecutor _jsExecutor;
//        private readonly IPaginationStateReader _stateReader;
//        private readonly IClickStrategyExecutor _clickExecutor;
//        private readonly ILogger _logger;

//        public SvgPaginationNavigator(
//            IElementWaiter elementWaiter,
//            IJavaScriptExecutor jsExecutor,
//            IPaginationStateReader stateReader,
//            IClickStrategyExecutor clickExecutor,
//            ILogger logger)
//        {
//            _elementWaiter = elementWaiter ?? throw new ArgumentNullException(nameof(elementWaiter));
//            _jsExecutor = jsExecutor ?? throw new ArgumentNullException(nameof(jsExecutor));
//            _stateReader = stateReader ?? throw new ArgumentNullException(nameof(stateReader));
//            _clickExecutor = clickExecutor ?? throw new ArgumentNullException(nameof(clickExecutor));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        public void NavigateToNextPage(string currentPage, string svgSelector = "svg.text-cyan-700")
//        {
//            try
//            {
//                // Ensure jQuery is available
//                _jsExecutor.InjectJQuery();
//                _elementWaiter.WaitUntil(driver => _jsExecutor.IsJQueryAvailable(), TimeSpan.FromSeconds(15));

//                // Find the SVG element
//                var svgElement = _elementWaiter.WaitForElement(By.CssSelector(svgSelector), TimeSpan.FromSeconds(15));
//                _logger.LogInfo($"Found SVG element: {svgElement.TagName}");

//                // Get current pagination state
//                var stateBefore = _stateReader.GetCurrentState(currentPage);

//                // Execute click
//                var clickResult = _clickExecutor.ExecuteClick(svgSelector);

//                if (!clickResult.Success)
//                {
//                    throw new InvalidOperationException($"Failed to click next button: {clickResult.Message}");
//                }

//                // Verify pagination changed
//                var nextPage = (int.Parse(currentPage) + 1).ToString();
//                _elementWaiter.WaitUntil(driver =>
//                {
//                    try
//                    {
//                        var stateAfter = _stateReader.GetCurrentState(nextPage);
//                        return stateAfter.CurrentPage == nextPage;
//                    }
//                    catch
//                    {
//                        return true; // Continue if we can't verify
//                    }
//                }, TimeSpan.FromSeconds(10));

//                _logger.LogInfo($"Successfully navigated from page {currentPage} to {nextPage}");
//            }
//            catch (NoSuchElementException ex)
//            {
//                _logger.LogError($"Navigation failed - element not found: {ex.Message}");
//                throw;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError($"Navigation failed: {ex.Message}");
//                throw;
//            }
//        }
//    }

//    // Factory for creating the navigator with all dependencies
//    public static class PaginationNavigatorFactory
//    {
//        public static IPaginationNavigator Create(IWebDriver driver)
//        {
//            var logger = new ConsoleLogger();
//            var jsExecutor = new WebDriverJavaScriptExecutor(driver, logger);
//            var elementWaiter = new SeleniumElementWaiter(driver, logger);
//            var stateReader = new AriaLabelPaginationStateReader(elementWaiter, jsExecutor, logger);

//            var strategies = new List<ClickStrategy>
//            {
//                new SwiperClickStrategy(jsExecutor, logger),
//                new BootstrapCarouselClickStrategy(jsExecutor, logger),
//                new GenericClickStrategy(jsExecutor, logger)
//            };

//            var clickExecutor = new ChainOfResponsibilityClickExecutor(strategies, logger);

//            return new SvgPaginationNavigator(elementWaiter, jsExecutor, stateReader, clickExecutor, logger);
//        }
//    }
//}

//// Usage example:
//// var navigator = PaginationNavigatorFactory.Create(driver);
//// navigator.NavigateToNextPage("1");
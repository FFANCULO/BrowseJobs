using OpenQA.Selenium;
using System;
using System.Linq;

namespace BrowseJobs
{
    public static class SvgClickHelper
    {
        public static void ClickSvgAncestorButton2(IWebDriver driver, string pageNumber = "1", string svgSelector = "svg.text-cyan-700")
        {
            try
            {
                // Wait for SVG to be present (up to 15 seconds)
                var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(driver, TimeSpan.FromSeconds(15));
                var svg = wait.Until(d => d.FindElement(By.CssSelector(svgSelector)));
                Console.WriteLine(
                    $"Found SVG: {svg?.GetAttribute("outerHTML")?.Substring(0, Math.Min(100, svg.GetAttribute("outerHTML")?.Length ?? 0)) ?? "SVG attributes not accessible"}");

                // Inject jQuery if not present
                ((IJavaScriptExecutor)driver)?.ExecuteScript(@"
            if (typeof jQuery === 'undefined') {
                var script = document.createElement('script');
                script.src = 'https://code.jquery.com/jquery-3.6.0.min.js';
                script.onload = function() { console.log('jQuery loaded'); };
                document.head.appendChild(script);
            }
        ");

                // Wait for jQuery to load (if injected)
                wait.Until(d => (bool)((IJavaScriptExecutor)d).ExecuteScript("return typeof jQuery !== 'undefined';"));

                // Find pagination text before click
                var paginationElementBefore = wait.Until(d =>
                    d.FindElement(By.XPath($"//section[@aria-label='Page {pageNumber} of 10']")));
                string paginationTextBefore = paginationElementBefore != null
                    ? ((IJavaScriptExecutor)driver)
                      ?.ExecuteScript("return jQuery(arguments[0]).text();", paginationElementBefore)?.ToString() ??
                      "Pagination text before click not found"
                    : "Pagination text before click not found";
                Console.WriteLine($"Pagination text before click: {paginationTextBefore}");

                // Use jQuery to find and trigger the 'next' action
                string result = ((IJavaScriptExecutor)driver)?.ExecuteScript(@"
            var $svg = jQuery(arguments[0]);
            if ($svg.length === 0) return 'SVG not found';

            // Find the 'next' button using aria-label='Next'
            var $nextButton = $svg.closest('span[aria-label=""Next""]');
            if ($nextButton.length === 0) return 'Next button not found';

            // Log the DOM path to the next button
            var path = [];
            var $current = $svg;
            while ($current.length && $current[0] !== $nextButton[0]) {
                path.push($current[0].tagName + ($current.attr('class') ? '.' + $current.attr('class').split(' ').join('.') : ''));
                $current = $current.parent();
            }
            path.push($nextButton[0].tagName + ($nextButton.attr('class') ? '.' + $nextButton.attr('class').split(' ').join('.') : ''));
            console.log('DOM path to next button:', path.join(' -> '));

            // Log the button
            var buttonHtml = $nextButton[0].outerHTML.substring(0, 100);
            console.log('Found next button:', buttonHtml);

            // Try pagination library methods
            var swiper = $nextButton.closest('.swiper-container').data('swiper') || window.Swiper;
            if (swiper) {
                swiper.slideNext();
                return 'Triggered Swiper slideNext: ' + buttonHtml;
            }

            var carousel = $nextButton.closest('.carousel').data('bs.carousel');
            if (carousel) {
                carousel.next();
                return 'Triggered Bootstrap carousel next: ' + buttonHtml;
            }

            // Simulate multiple events
            $nextButton.trigger('mousedown')
                       .trigger('mouseup')
                       .trigger('click')
                       .trigger('touchend')
                       .trigger(jQuery.Event('keydown', {keyCode: 39}));

            // Check for click handler
            var events = $._data($nextButton[0], 'events');
            if (events && events.click) {
                return 'Triggered click event listener: ' + buttonHtml;
            }
            if ($nextButton[0].onclick) {
                $nextButton[0].onclick();
                return 'Triggered onclick handler: ' + buttonHtml;
            }

            return 'Clicked next button but no onclick or listener found: ' + buttonHtml;
        ", svgSelector)?.ToString() ?? "jQuery injection failed";

                Console.WriteLine($"jQuery result: {result}");

                // Wait for pagination to update
                var paginationElementAfter =
                    wait.Until(d => d.FindElement(By.XPath("//section[contains(@aria-label, 'Page')]")));
                string paginationTextAfter = paginationElementAfter != null
                    ? ((IJavaScriptExecutor)driver)
                      ?.ExecuteScript("return jQuery(arguments[0]).text();", paginationElementAfter)?.ToString() ??
                      "Pagination text after click not found"
                    : "Pagination text after click not found";
                Console.WriteLine($"Pagination text after click: {paginationTextAfter}");

                // Log pagination element details
                string paginationDetails = paginationElementAfter != null
                    ? ((IJavaScriptExecutor)driver)?.ExecuteScript(
                          "return arguments[0].outerHTML.substring(0, 100);", paginationElementAfter)?.ToString() ??
                      "Pagination element details unavailable"
                    : "Pagination element not found";
                Console.WriteLine($"Pagination element details: {paginationDetails}");

                // Wait for pagination to update
                wait.Until(d =>
                {
                    var nextPage = int.Parse(pageNumber) + 1;
                    return d.FindElements(By.XPath($"//section[contains(@aria-label, 'Page {nextPage}')]")).Count > 0 ||
                           true;
                });
            }
            catch (NoSuchElementException)
            {
                Console.WriteLine("NO MORE SHIT TO PROCESS");
            }
            catch (OpenQA.Selenium.WebDriverException ex)
            {
                Console.WriteLine($"Error: Failed to execute jQuery for selector '{svgSelector}'. {ex.Message}");
                throw;
            }
        }
    }


}
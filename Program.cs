// See https://aka.ms/new-console-template for more information

using System.Threading.Tasks;
using PuppeteerSharp;

namespace BrowseJobs
{
    /// <summary>
    /// https://dashboard.webshare.io/proxy/list?authenticationMethod=%22username_password%22&connectionMethod=%22direct%22&proxyControl=%220%22&rowsPerPage=10&page=0&order=%22asc%22&orderBy=null&searchValue=%22%22&removeType=%22refresh_all%22
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Browser launchAndConnectAsync = await EdgeDevToolsLauncher.LaunchAndConnectAsync();

            await CookieBridge.LaunchEdgeWithCookiesAsync("https://www.dice.com/");

            //var driver = EdgeDriverFactory.LaunchWithAuthenticatedProxy(
            //    "198.23.239.134", 6540, "ifabpxsh", "ceepwsj6156a"
            //);

            var driver = EdgeDriverFactory.LaunchWithAuthenticatedProxy(
                "216.10.27.159", 6837, "ifabpxsh", "ceepwsj6156a"
            );
            await driver.Navigate().GoToUrlAsync("https://www.dice.com/");

            


            // var result = await XAi.CallGrok("\r\n            Analyze the following job requirements and extract key skills, qualifications, and job titles as a list of keywords. Focus on terms critical for ATS compliance, such as specific technologies, degrees, and experience levels. Return the keywords as a JSON list.\r\n\r\n            Job Requirements:\r\n            Position Details:\r\nIndustry: Banking\r\nTitle: Python Developer\r\nDuration: 12 months +\r\nLocation: New York, NY\r\n\r\nMust haves:\r\n\r\n6+ years Experience in a comparable Python development or technology role having experience in financial risk evironment.\r\n\r\nMust have technical/programming skills; Python and C++.\r\n\r\nDesired: TypeScript, Product knowledge, Investments and Quantitative Methods\r\n            ");
            // Shit.DoIt();
             Prog2.DoIt();


         
        }
    }
}
// See https://aka.ms/new-console-template for more information

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace BrowseJobs
{
    /// <summary>
    /// taskkill /F /IM msedge.exe
    /// https://dashboard.webshare.io/proxy/list?authenticationMethod=%22username_password%22&connectionMethod=%22direct%22&proxyControl=%220%22&rowsPerPage=10&page=0&order=%22asc%22&orderBy=null&searchValue=%22%22&removeType=%22refresh_all%22
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // await NewMethod();


            Prog2.DoIt();
        }

        private static async Task NewMethod()
        {
            var (browser, process) = await EdgeDevToolsLauncher.LaunchAndConnectAsync();

            await CookieBridge.LaunchEdgeWithCookiesAsync("https://www.dice.com/", process);

            //var driver = EdgeDriverFactory.LaunchWithAuthenticatedProxy(
            //    "198.23.239.134", 6540, "ifabpxsh", "ceepwsj6156a"
            //);

            //EdgeDriverFactory.CreateProxyAuthExtension(
            //    "216.10.27.159", 6837, "ifabpxsh", "ceepwsj6156a");


            var driver = EdgeDriverFactory.LaunchWithAuthenticatedProxy(
                "216.10.27.159", 6837, "ifabpxsh", "ceepwsj6156a"
            );
            await driver.Navigate().GoToUrlAsync("https://www.dice.com/");
        }
    }
}
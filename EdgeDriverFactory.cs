using System;
using System.IO;
using System.Text;
using OpenQA.Selenium.Edge;

namespace BrowseJobs;

public static class EdgeDriverFactory
{
    public static EdgeDriver LaunchWithAuthenticatedProxy(string proxyHost, int proxyPort, string proxyUser,
        string proxyPass)
    {
        var extensionDir = CreateProxyAuthExtension(proxyHost, proxyPort, proxyUser, proxyPass);

        var options = new EdgeOptions();
        //options.AddArgument("--disable-blink-features=AutomationControlled");
        //options.AddArgument($"--disable-extensions-except={extensionDir}");
        options.AddArgument($"--load-extension={extensionDir}");
        // options.AddExcludedArgument("enable-automation");

        var service = EdgeDriverService.CreateDefaultService();
        service.HideCommandPromptWindow = true;

        return new EdgeDriver(service, options);
    }

    public static string CreateProxyAuthExtension(string host, int port, string user, string pass)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edge_proxy_auth_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        // Build manifest.json dynamically (static content)
        var manifestBuilder = new StringBuilder();
        manifestBuilder.AppendLine("{");
        manifestBuilder.AppendLine(@"  ""version"": ""1.0.0"",");
        manifestBuilder.AppendLine(@"  ""manifest_version"": 2,");
        manifestBuilder.AppendLine(@"  ""name"": ""Edge Proxy Auth"",");
        manifestBuilder.AppendLine(@"  ""permissions"": [");
        manifestBuilder.AppendLine(@"    ""proxy"",");
        manifestBuilder.AppendLine(@"    ""tabs"",");
        manifestBuilder.AppendLine(@"    ""unlimitedStorage"",");
        manifestBuilder.AppendLine(@"    ""storage"",");
        manifestBuilder.AppendLine(@"    ""<all_urls>"",");
        manifestBuilder.AppendLine(@"    ""webRequest"",");
        manifestBuilder.AppendLine(@"    ""webRequestBlocking""");
        manifestBuilder.AppendLine(@"  ],");
        manifestBuilder.AppendLine(@"  ""background"": {");
        manifestBuilder.AppendLine(@"    ""scripts"": [""background.js""]");
        manifestBuilder.AppendLine("  }");
        manifestBuilder.AppendLine("}");

        // Build background.js dynamically (injecting proxy info)
        var backgroundBuilder = new StringBuilder();
        backgroundBuilder.AppendLine("var config = {");
        backgroundBuilder.AppendLine("  mode: 'fixed_servers',");
        backgroundBuilder.AppendLine("  rules: {");
        backgroundBuilder.AppendLine("    singleProxy: {");
        backgroundBuilder.AppendLine("      scheme: 'http',");
        backgroundBuilder.AppendLine($"      host: '{host}',");
        backgroundBuilder.AppendLine($"      port: {port}");
        backgroundBuilder.AppendLine("    },");
        backgroundBuilder.AppendLine("    bypassList: ['localhost']");
        backgroundBuilder.AppendLine("  }");
        backgroundBuilder.AppendLine("};");
        backgroundBuilder.AppendLine();
        backgroundBuilder.AppendLine("chrome.proxy.settings.set({ value: config, scope: 'regular' }, function() {});");
        backgroundBuilder.AppendLine();
        backgroundBuilder.AppendLine("chrome.webRequest.onAuthRequired.addListener(");
        backgroundBuilder.AppendLine("  function(details, callbackFn) {");
        backgroundBuilder.AppendLine("    callbackFn({");
        backgroundBuilder.AppendLine("      authCredentials: {");
        backgroundBuilder.AppendLine($"        username: '{user}',");
        backgroundBuilder.AppendLine($"        password: '{pass}'");
        backgroundBuilder.AppendLine("      }");
        backgroundBuilder.AppendLine("    });");
        backgroundBuilder.AppendLine("  },");
        backgroundBuilder.AppendLine("  { urls: ['<all_urls>'] },");
        backgroundBuilder.AppendLine("  ['blocking']");
        backgroundBuilder.AppendLine(");");

        // Write both files to disk
        File.WriteAllText(Path.Combine(dir, "manifest.json"), manifestBuilder.ToString());
        File.WriteAllText(Path.Combine(dir, "background.js"), backgroundBuilder.ToString());

        return dir;
    }
}
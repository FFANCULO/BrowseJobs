using System;
using System.IO;
using OpenQA.Selenium;
using OpenQA.Selenium.Edge;

namespace BrowseJobs;

public static class EdgeDriverFactory
{
    public static EdgeDriver LaunchWithAuthenticatedProxy(string proxyHost, int proxyPort, string proxyUser,
        string proxyPass)
    {
        var extensionDir = CreateProxyAuthExtension(proxyHost, proxyPort, proxyUser, proxyPass);

        var proxy = new Proxy
        {
            HttpProxy = $"{proxyHost}:{proxyPort}",
            SslProxy = $"{proxyHost}:{proxyPort}",
            Kind = ProxyKind.Manual,
            IsAutoDetect = false
        };

        var options = new EdgeOptions
        {
            Proxy = proxy
        };


        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddArgument($"--load-extension={extensionDir}");

        return new EdgeDriver(options);
    }

    private static string CreateProxyAuthExtension(string host, int port, string user, string pass)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edge_proxy_auth_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var manifest = @"{
  ""version"": ""1.0.0"",
  ""manifest_version"": 2,
  ""name"": ""Edge Proxy Auth"",
  ""permissions"": [""proxy"", ""tabs"", ""unlimitedStorage"", ""storage"", ""<all_urls>"", ""webRequest"", ""webRequestBlocking""],
  ""background"": {
    ""scripts"": [""background.js""]
  }
}";
        var background = $@"
var config = {{
    mode: ""fixed_servers"",
    rules: {{
        singleProxy: {{
            scheme: ""http"",
            host: ""{host}"",
            port: {port}
        }},
        bypassList: [""localhost""]
    }}
}};
chrome.proxy.settings.set({{value: config, scope: ""regular""}}, function() {{}});

chrome.webRequest.onAuthRequired.addListener(
    function(details, callbackFn) {{
        callbackFn({{
            authCredentials: {{username: ""{user}"", password: ""{pass}""}}
        }});
    }},
    {{urls: [""<all_urls>""]}},
    ['blocking']
);";

        File.WriteAllText(Path.Combine(dir, "manifest.json"), manifest);
        File.WriteAllText(Path.Combine(dir, "background.js"), background);

        return dir;
    }
}
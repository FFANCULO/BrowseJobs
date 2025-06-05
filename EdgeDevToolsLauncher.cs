using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace BrowseJobs;

public static class EdgeDevToolsLauncher
{
    private const int DebugPort = 9222;
    private static readonly string DevToolsUrl = $"http://localhost:{DebugPort}";
    private static string? _tempUserDataDir;

    public static async Task<Browser> LaunchAndConnectAsync()
    {
        StartEdgeWithDevTools();

        Console.WriteLine("[*] Waiting for Edge DevTools port...");
        await WaitForDevToolsAsync();

        Console.WriteLine("[*] Connecting PuppeteerSharp...");
        return (Browser)await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserURL = "http://localhost:9222"
        });

    }

    private static void StartEdgeWithDevTools()
    {
        _tempUserDataDir = Path.Combine(Path.GetTempPath(), "EdgeTempProfile_" + Guid.NewGuid());

        var edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
        if (!File.Exists(edgePath))
            throw new FileNotFoundException("Edge not found at default path", edgePath);

        var psi = new ProcessStartInfo
        {
            FileName = edgePath,
            Arguments = $"--remote-debugging-port={DebugPort} --user-data-dir=\"{_tempUserDataDir}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process.Start(psi);
    }

    private static async Task WaitForDevToolsAsync(int timeoutSeconds = 10)
    {
        var start = DateTime.Now;
        while ((DateTime.Now - start).TotalSeconds < timeoutSeconds)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", DebugPort);
                return;
            }
            catch
            {
                await Task.Delay(300);
            }
        }

        throw new TimeoutException("Timed out waiting for Edge DevTools on port 9222.");
    }
}
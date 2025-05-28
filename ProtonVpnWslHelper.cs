using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace BrowseJobs;

public class ProtonVpnWslHelper
{
    private string _currentServer = null;
    private readonly List<string> _states;
    private int _cycleCount = 0;
    private const int CommandTimeoutMs = 30000; // 30 seconds timeout for WSL commands
    private const int MaxConnectionRetries = 3;

    /// <summary>
    /// Initializes a new instance of the ProtonVpnWslHelper class.
    /// </summary>
    /// <param name="statesToCycle">List of US state abbreviations (e.g., CA, NY).</param>
    /// <exception cref="ArgumentException">Thrown if statesToCycle is empty or contains invalid abbreviations.</exception>
    public ProtonVpnWslHelper(IEnumerable<string> statesToCycle)
    {
        if (statesToCycle == null || !statesToCycle.Any())
            throw new ArgumentException("States list cannot be null or empty.", nameof(statesToCycle));

        _states = statesToCycle
            .Select(s => s?.Trim().ToUpper())
            .Where(s => IsValidStateAbbreviation(s))
            .Distinct()
            .ToList();

        if (!_states.Any())
            throw new ArgumentException("No valid US state abbreviations provided.", nameof(statesToCycle));
    }

    /// <summary>
    /// Lists up to a specified number of VPN servers for a given US state.
    /// </summary>
    /// <param name="stateAbbreviation">Two-letter US state abbreviation (e.g., CA).</param>
    /// <param name="limit">Maximum number of servers to return (default: 10).</param>
    /// <returns>A list of server names, or empty if none found.</returns>
    /// <exception cref="ArgumentException">Thrown if stateAbbreviation is invalid.</exception>
    public List<string> ListTopServersByState(string stateAbbreviation, int limit = 10)
    {
        if (!IsValidStateAbbreviation(stateAbbreviation))
        {
            Console.WriteLine($"Invalid state abbreviation: {stateAbbreviation}");
            return new List<string>();
        }

        if (limit <= 0)
        {
            Console.WriteLine("Limit must be positive; returning empty list.");
            return new List<string>();
        }

        string prefix = $"US-{stateAbbreviation.ToUpper()}";
        // Escape special characters to prevent command injection
        string safePrefix = Regex.Escape(prefix);
        string command = $"protonvpn-cli list | grep '^{safePrefix}' | head -n {limit}";
        string output = RunWslCommand(command);

        var servers = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(s => IsValidServerName(s))
            .ToList();

        if (!servers.Any())
            Console.WriteLine($"No servers found for state {stateAbbreviation}.");

        return servers;
    }

    /// <summary>
    /// Connects to a specified VPN server, disconnecting from the current server if necessary.
    /// </summary>
    /// <param name="serverName">The name of the server to connect to (e.g., US-CA#01).</param>
    /// <exception cref="ArgumentException">Thrown if serverName is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown if connection fails.</exception>
    public void ConnectToServer(string serverName)
    {
        if (!IsValidServerName(serverName))
            throw new ArgumentException($"Invalid server name: {serverName}", nameof(serverName));

        if (IsConnected())
        {
            Console.WriteLine($"Disconnecting from {_currentServer}...");
            Disconnect();
        }

        Console.WriteLine($"Connecting to {serverName}...");
        string safeServerName = serverName.Replace("\"", "\\\""); // Escape quotes
        RunWslCommand($"protonvpn-cli connect \"{safeServerName}\"");

        // Verify connection
        if (!IsConnected())
            throw new InvalidOperationException($"Failed to connect to {serverName}. Check protonvpn-cli status.");

        _currentServer = serverName;
    }

    /// <summary>
    /// Disconnects from the current VPN server.
    /// </summary>
    public void Disconnect()
    {
        if (!IsConnected())
        {
            Console.WriteLine("No active connection to disconnect.");
            _currentServer = null;
            return;
        }

        RunWslCommand("protonvpn-cli disconnect");
        _currentServer = null;

        // Verify disconnection
        if (IsConnected())
            Console.WriteLine("Warning: Disconnection may not have completed successfully.");
    }

    /// <summary>
    /// Retrieves the current VPN connection status.
    /// </summary>
    /// <returns>The status output from protonvpn-cli.</returns>
    public string GetStatus()
    {
        return RunWslCommand("protonvpn-cli status");
    }

    /// <summary>
    /// Checks if a VPN connection is active.
    /// </summary>
    /// <returns>True if connected, false otherwise.</returns>
    public bool IsConnected()
    {
        string status = GetStatus().ToLower();
        return status.Contains("connected") && !status.Contains("disconnected");
    }

    /// <summary>
    /// Cycles to the next available server in the state list, with retries on failure.
    /// </summary>
    public void CycleNextServer()
    {
        if (!_states.Any())
        {
            Console.WriteLine("No states available to cycle.");
            return;
        }

        for (int attempts = 0; attempts < _states.Count; attempts++)
        {
            int stateIndex = _cycleCount % _states.Count;
            string state = _states[stateIndex];
            var servers = ListTopServersByState(state);

            if (servers.Any())
            {
                int serverIndex = (_cycleCount / _states.Count) % servers.Count;
                string targetServer = servers[serverIndex];

                for (int retry = 0; retry < MaxConnectionRetries; retry++)
                {
                    try
                    {
                        Console.WriteLine($"[Cycle #{_cycleCount}, Attempt {retry + 1}/{MaxConnectionRetries}] Connecting to {targetServer} from state {state}...");
                        ConnectToServer(targetServer);
                        _cycleCount++;
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Connection failed: {ex.Message}");
                        if (retry < MaxConnectionRetries - 1)
                            Console.WriteLine("Retrying...");
                    }
                }

                Console.WriteLine($"Failed to connect to {targetServer} after {MaxConnectionRetries} attempts.");
            }
            else
            {
                Console.WriteLine($"No servers available for state {state}.");
            }

            _cycleCount++;
        }

        Console.WriteLine("❌ No valid servers found in any state during cycle.");
    }

    /// <summary>
    /// Executes a Bash command in WSL and returns the output.
    /// </summary>
    /// <param name="bashCommand">The Bash command to execute.</param>
    /// <returns>The command's standard output.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command fails.</exception>
    private string RunWslCommand(string bashCommand)
    {
        if (string.IsNullOrWhiteSpace(bashCommand))
            throw new ArgumentException("Bash command cannot be empty.", nameof(bashCommand));

        var psi = new ProcessStartInfo
        {
            FileName = "wsl.exe",
            Arguments = $"-e bash -c \"{bashCommand}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Set timeout
        if (!process.WaitForExit(CommandTimeoutMs))
        {
            process.Kill();
            throw new InvalidOperationException("WSL command timed out.");
        }

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();

        if (process.ExitCode != 0 || (!string.IsNullOrWhiteSpace(error) && !error.Contains("logout")))
        {
            throw new InvalidOperationException($"WSL command failed (ExitCode: {process.ExitCode}):\nError: {error}\nOutput: {output}");
        }

        return output.Trim();
    }

    /// <summary>
    /// Validates a US state abbreviation (two-letter code).
    /// </summary>
    private static bool IsValidStateAbbreviation(string state)
    {
        return !string.IsNullOrWhiteSpace(state) && Regex.IsMatch(state, @"^[A-Z]{2}$");
    }

    /// <summary>
    /// Validates a ProtonVPN server name (e.g., US-CA#01).
    /// </summary>
    private static bool IsValidServerName(string serverName)
    {
        return !string.IsNullOrWhiteSpace(serverName) && Regex.IsMatch(serverName, @"^US-[A-Z]{2}#[0-9]+$");
    }
}
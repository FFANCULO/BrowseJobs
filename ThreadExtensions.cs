using System;
using System.Security.Cryptography;
using System.Threading;

namespace BrowseJobs;

public static class ThreadExtensions
{
    /// <summary>
    /// Pauses execution for a human-like randomized delay using cryptographic randomness.
    /// </summary>
    /// <param name="thread">Extension context</param>
    /// <param name="baseSeconds">Target pause center (e.g., 5.0)</param>
    /// <param name="jitterSeconds">Jitter window ± around base (e.g., ±1.0)</param>
    public static void HumanPause(this Thread thread, double baseSeconds = 5.0, double jitterSeconds = 1.0)
    {
        double min = Math.Max(0.0, baseSeconds - jitterSeconds);
        double max = baseSeconds + jitterSeconds;
        double delay = GetCryptoRandomDouble(min, max);

        Console.WriteLine($"[HumanPause] Cryptographic sleep: {delay:F2} seconds...");
        Thread.Sleep(TimeSpan.FromSeconds(delay));
    }

    /// <summary>
    /// Generates a cryptographically secure random double between min and max.
    /// </summary>
    private static double GetCryptoRandomDouble(double min, double max)
    {
        using var rng = RandomNumberGenerator.Create();
        byte[] bytes = new byte[8];
        rng.GetBytes(bytes);
        ulong ulongVal = BitConverter.ToUInt64(bytes, 0);
        double normalized = ulongVal / (double)ulong.MaxValue;

        return min + (normalized * (max - min));
    }
}
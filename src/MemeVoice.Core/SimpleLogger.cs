// src/MemeVoice.Core/SimpleLogger.cs
using System;
using System.IO;

namespace MemeVoice.Core;

public static class SimpleLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MemeVoice", "log.txt");

    public static void LogError(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch
        {
            // Logging must never crash the app; silently drop if the file can't be written.
        }
    }
}

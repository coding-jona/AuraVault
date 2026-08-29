using System;
using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AuraVault.App;

/// <summary>Serilog setup with a redaction filter so secret-looking values never hit the log.</summary>
internal static class Logging
{
    public static Logger CreateBootstrapLogger()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AuraVault", "logs");
        Directory.CreateDirectory(dir);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.With(new RedactionEnricher())
            .WriteTo.Debug()
            .WriteTo.File(
                Path.Combine(dir, "aura-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private sealed class RedactionEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            foreach (var key in logEvent.Properties.Keys)
            {
                if (key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("token", StringComparison.OrdinalIgnoreCase))
                {
                    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, "«redacted»"));
                }
            }
        }
    }
}

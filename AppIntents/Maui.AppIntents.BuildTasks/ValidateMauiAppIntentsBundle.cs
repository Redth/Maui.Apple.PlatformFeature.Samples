using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Maui.AppIntents.BuildTasks;

public sealed class ValidateMauiAppIntentsBundle : Task
{
    private static readonly string[] RequiredBridgeSymbols =
    {
        "_MauiAppIntentBridgeSetDispatcher",
        "_MauiAppIntentBridgeDonate"
    };

    [Required]
    public string AppBundleDir { get; set; } = "";

    [Required]
    public string ModuleName { get; set; } = "";

    [Required]
    public string ValidationManifest { get; set; } = "";

    [Required]
    public string StampFile { get; set; } = "";

    public override bool Execute()
    {
        try
        {
            var metadataDir = Path.Combine(AppBundleDir ?? "", "Metadata.appintents");
            var actionsData = Path.Combine(metadataDir, "extract.actionsdata");
            var versionJson = Path.Combine(metadataDir, "version.json");
            var frameworkBinary = Path.Combine(AppBundleDir ?? "", "Frameworks", ModuleName + ".framework", ModuleName);

            RequireFile(ValidationManifest, "Generated App Intents validation manifest is missing.");
            if (Log.HasLoggedErrors)
            {
                return false;
            }

            var entries = ReadManifest().ToList();
            if (entries.Count == 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StampFile)!);
                File.WriteAllText(StampFile, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                Log.LogMessage(MessageImportance.Low, "No generated MAUI App Intents found to validate.");
                return true;
            }

            RequireFile(actionsData, "Generated App Intents metadata is missing from the app bundle.");
            RequireFile(versionJson, "Generated App Intents metadata version file is missing from the app bundle.");
            RequireFile(frameworkBinary, "Generated App Intents framework is missing from the app bundle.");

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            ValidateActionsData(actionsData, entries);
            ValidateBridgeSymbols(frameworkBinary);

            if (!Log.HasLoggedErrors)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StampFile)!);
                File.WriteAllText(StampFile, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                Log.LogMessage(MessageImportance.High, "Validated generated MAUI App Intents bundle outputs.");
            }

            return !Log.HasLoggedErrors;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true);
            return false;
        }
    }

    private void ValidateActionsData(string actionsData, IReadOnlyList<ManifestEntry> entries)
    {
        var text = File.ReadAllText(actionsData);
        if (text.IndexOf('{') < 0)
        {
            Log.LogWarning("Generated App Intents metadata at '{0}' is not a recognizable JSON payload; skipping identifier and shortcut phrase validation.", actionsData);
            return;
        }

        foreach (var entry in entries)
        {
            if (!ContainsJsonString(text, entry.Value))
            {
                Log.LogError("Generated App Intents metadata does not contain expected {0} '{1}'.", entry.Kind, entry.Value);
            }
        }
    }

    private IEnumerable<ManifestEntry> ReadManifest()
    {
        foreach (var line in File.ReadAllLines(ValidationManifest))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var separator = line.IndexOf('\t');
            if (separator <= 0 || separator == line.Length - 1)
            {
                Log.LogWarning("Ignoring malformed generated App Intents validation manifest line: {0}", line);
                continue;
            }

            yield return new ManifestEntry(line.Substring(0, separator), line.Substring(separator + 1));
        }
    }

    private void ValidateBridgeSymbols(string frameworkBinary)
    {
        var architectures = GetArchitectures(frameworkBinary);
        foreach (var symbol in RequiredBridgeSymbols)
        {
            if (architectures.Count == 0)
            {
                if (NmContainsSymbol(frameworkBinary, null, symbol))
                {
                    continue;
                }

                Log.LogError("Generated App Intents framework '{0}' does not export '{1}'.", frameworkBinary, symbol);
                continue;
            }

            var found = false;
            foreach (var architecture in architectures)
            {
                if (NmContainsSymbol(frameworkBinary, architecture, symbol))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Log.LogError("Generated App Intents framework '{0}' does not export '{1}' in any architecture slice ({2}).", frameworkBinary, symbol, string.Join(", ", architectures));
            }
        }
    }

    private List<string> GetArchitectures(string frameworkBinary)
    {
        var result = Run("/usr/bin/lipo", "-archs " + Quote(frameworkBinary));
        if (result.ExitCode != 0)
        {
            return new List<string>();
        }

        var output = (result.Output + " " + result.Error).Trim();
        var marker = "are:";
        var index = output.IndexOf(marker, StringComparison.Ordinal);
        if (index >= 0)
        {
            return SplitArchitectures(output.Substring(index + marker.Length));
        }

        marker = "architecture:";
        index = output.IndexOf(marker, StringComparison.Ordinal);
        if (index >= 0)
        {
            return SplitArchitectures(output.Substring(index + marker.Length));
        }

        return SplitArchitectures(output);
    }

    private static List<string> SplitArchitectures(string value)
    {
        return (value ?? "")
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(v => v.IndexOf('/') < 0 && v.IndexOf(':') < 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private bool NmContainsSymbol(string frameworkBinary, string? architecture, string symbol)
    {
        var args = string.IsNullOrEmpty(architecture)
            ? "-gU " + Quote(frameworkBinary)
            : "-arch " + Quote(architecture ?? "") + " -gU " + Quote(frameworkBinary);
        var result = Run("/usr/bin/nm", args);
        return (result.Output + result.Error).IndexOf(symbol, StringComparison.Ordinal) >= 0;
    }

    private static ProcessResult Run(string fileName, string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        var output = new StringBuilder();
        var error = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, output.ToString(), error.ToString());
    }

    private void RequireFile(string path, string message)
    {
        if (!File.Exists(path))
        {
            Log.LogError("{0} Expected file: {1}", message, path);
        }
    }

    private static bool ContainsJsonString(string json, string value)
    {
        return json.IndexOf(ToJsonStringLiteral(value), StringComparison.Ordinal) >= 0 ||
            json.IndexOf(value ?? "", StringComparison.Ordinal) >= 0;
    }

    private static string ToJsonStringLiteral(string value)
    {
        var sb = new StringBuilder();
        sb.Append('"');
        foreach (var ch in value ?? "")
        {
            switch (ch)
            {
                case '\\':
                    sb.Append(@"\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\b':
                    sb.Append(@"\b");
                    break;
                case '\f':
                    sb.Append(@"\f");
                    break;
                case '\n':
                    sb.Append(@"\n");
                    break;
                case '\r':
                    sb.Append(@"\r");
                    break;
                case '\t':
                    sb.Append(@"\t");
                    break;
                default:
                    if (char.IsControl(ch))
                    {
                        sb.Append("\\u");
                        sb.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static string Quote(string value)
    {
        return "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private sealed class ManifestEntry
    {
        public ManifestEntry(string kind, string value)
        {
            Kind = kind;
            Value = value;
        }

        public string Kind { get; }

        public string Value { get; }
    }

    private sealed class ProcessResult
    {
        public ProcessResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }

        public int ExitCode { get; }

        public string Output { get; }

        public string Error { get; }
    }
}

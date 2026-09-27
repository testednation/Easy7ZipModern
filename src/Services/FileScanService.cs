using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StarExtract.Services;

namespace StarExtract;

/// <summary>
/// Runs the File-Type Scanner pipeline: TrID signature analysis plus the optional
/// Google Magika AI detector, and can download + install any missing scanner on
/// demand through the shared HttpDownloader.
/// </summary>
public class FileScanService
{
    public const string TrIdExeUrl = "https://mark0.net/download/trid_w32.zip";
    public const string TrIdDefsUrl = "https://mark0.net/download/triddefs.zip";
    public const string MagikaUrl = "https://github.com/google/magika/releases/download/cli/v1.1.0/magika-cli-x86_64-pc-windows-msvc.zip";
    public const string MagikaVersion = "1.1.0";

    private readonly string _baseDir;
    private readonly HttpDownloader _downloader = new HttpDownloader();

    public FileScanService()
    {
        _baseDir = AppDomain.CurrentDomain.BaseDirectory;
    }

    public string TrIdExePath => Path.Combine(_baseDir, "bin", "trid.exe");
    public string TrIdDefsPath => Path.Combine(_baseDir, "bin", "triddefs.trd");
    public string MagikaExePath => Path.Combine(_baseDir, "bin", "magika", "magika.exe");

    public bool IsTrIdAvailable => File.Exists(TrIdExePath);
    public bool IsTrIdDefsAvailable => File.Exists(TrIdDefsPath);

    /// <summary>True when Magika is bundled next to the app or found on PATH.</summary>
    public bool IsMagikaAvailable => ResolveMagikaExe() != null;

    /// <summary>Magika bundled next to the app, else found on PATH, else null.</summary>
    public string ResolveMagikaExe()
    {
        if (File.Exists(MagikaExePath))
        {
            return MagikaExePath;
        }
        return FindFileOnPath("magika.exe");
    }

    /// <summary>Searches every directory on the PATH for an executable.</summary>
    public static string FindFileOnPath(string exeName)
    {
        string pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
        {
            return null;
        }
        foreach (string dir in pathVar.Split(';'))
        {
            try
            {
                string trimmed = dir == null ? null : dir.Trim().Trim('"');
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }
                string candidate = Path.Combine(trimmed, exeName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
            }
        }
        return null;
    }

    /// <summary>Runs TrID + (optionally) Magika against the target and returns a combined report.</summary>
    public string BuildScanReport(string targetFile, bool includeMagika)
    {
        var report = new StringBuilder();

        report.AppendLine("=== TrID Analysis ===");
        report.AppendLine();
        if (IsTrIdAvailable)
        {
            if (!IsTrIdDefsAvailable)
            {
                report.AppendLine("[!] triddefs.trd was not found beside trid.exe; identification quality will suffer.");
                report.AppendLine();
            }
            report.Append(RunCommand(TrIdExePath, "\"" + targetFile + "\""));
        }
        else
        {
            report.AppendLine("TrID was not found (expected bin\\trid.exe).");
            report.AppendLine("Reopen the scanner and accept the download prompt to install it.");
        }

        if (includeMagika)
        {
            report.AppendLine();
            report.AppendLine();
            report.AppendLine("=== Magika Analysis (Google AI) ===");
            report.AppendLine();
            string magikaExe = ResolveMagikaExe();
            if (magikaExe != null)
            {
                report.Append(RunCommand(magikaExe, "\"" + targetFile + "\""));
            }
            else
            {
                report.AppendLine("Magika is not installed (looked for bin\\magika\\magika.exe and on PATH).");
                report.AppendLine("Tick 'Use Magika' and accept the download prompt to add an AI-powered second opinion.");
            }
        }

        return report.ToString();
    }

    /// <summary>
    /// Downloads and installs the missing scanners (TrID, its definitions and/or the
    /// Magika CLI) through the shared downloader. Reports 0-100 progress plus a status
    /// message; throws on failure, OperationCanceledException on cancel.
    /// </summary>
    public Task DownloadMissingAsync(bool needTrId, bool needMagika, Action<double, string> progress, CancellationToken ct)
    {
        var batch = new DownloadBatch();

        if (needTrId && !IsTrIdAvailable)
        {
            batch.Steps.Add(new DownloadStep
            {
                Name = "TrID",
                Url = TrIdExeUrl,
                ExtractDir = Path.GetDirectoryName(TrIdExePath),
                WantedFile = "trid.exe"
            });
        }
        if (needTrId && !IsTrIdDefsAvailable)
        {
            batch.Steps.Add(new DownloadStep
            {
                Name = "TrID definitions",
                Url = TrIdDefsUrl,
                ExtractDir = Path.GetDirectoryName(TrIdDefsPath),
                WantedFile = "triddefs.trd"
            });
        }
        if (needMagika && ResolveMagikaExe() == null)
        {
            batch.Steps.Add(new DownloadStep
            {
                Name = "Magika CLI v" + MagikaVersion,
                Url = MagikaUrl,
                ExtractDir = Path.GetDirectoryName(MagikaExePath),
                WantedFile = "magika.exe"
            });
        }

        return batch.Steps.Count == 0 ? Task.CompletedTask : _downloader.DownloadAsync(batch, progress, ct);
    }

    // ---------------- helpers ----------------

    /// <summary>Runs a scanner executable and captures its combined stdout/stderr output.</summary>
    private static string RunCommand(string exePath, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exePath)
            };

            using (Process p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                p.WaitForExit();

                string result = output;
                if (!string.IsNullOrEmpty(error))
                {
                    result += "\r\n[Errors]:\r\n" + error;
                }
                if (p.ExitCode != 0)
                {
                    result += $"\r\n[Exit code: {p.ExitCode}]";
                    if (string.IsNullOrWhiteSpace(output) && string.IsNullOrWhiteSpace(error))
                    {
                        result += "\r\n(No output was produced. For Magika: the official Windows build requires a CPU with\r\nAVX2 instructions — older CPUs cannot run it. Uncheck 'Use Magika' to skip it.)";
                    }
                }
                return result;
            }
        }
        catch (Exception ex)
        {
            return $"Error running {Path.GetFileName(exePath)}: {ex.Message}";
        }
    }
}

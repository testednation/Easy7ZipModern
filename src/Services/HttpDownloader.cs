using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace StarExtract.Services;

/// <summary>A single step inside a <see cref="DownloadBatch"/>: fetch + unpack one archive.</summary>
public class DownloadStep
{
    public string Name;
    public string Url;
    /// <summary>Where to unpack. When null/empty the archive is downloaded only and
    /// its temp path is exposed via <see cref="ResultTempFile"/> for custom install.</summary>
    public string ExtractDir;
    /// <summary>File that must exist at the root of ExtractDir after install, hoisted from subfolders if needed.</summary>
    public string WantedFile;
    /// <summary>After a download-only step: path of the downloaded temp archive (caller must delete it).</summary>
    public string ResultTempFile;
}

/// <summary>A list of DownloadSteps plus per-step extraction hooks.</summary>
public class DownloadBatch
{
    public List<DownloadStep> Steps { get; } = new List<DownloadStep>();

    /// <summary>Optional extra processing after a step's archive is extracted.</summary>
    public Action<DownloadStep> AfterExtract;

    public static DownloadBatch Single(string name, string url, string extractDir, string wantedFile, Action<DownloadStep> afterExtract = null)
    {
        var batch = new DownloadBatch();
        batch.Steps.Add(new DownloadStep
        {
            Name = name,
            Url = url,
            ExtractDir = extractDir,
            WantedFile = wantedFile
        });
        batch.AfterExtract = afterExtract;
        return batch;
    }
}

/// <summary>
/// Shared HTTP downloader used by both the Components updater and the File-Type
/// Scanner: fetches archives, unpacks them with the bundled 7-Zip core, hoists
/// wanted files out of nested folders, and reports combined progress across all
/// steps in a batch (0-100) with per-step status text.
/// </summary>
public class HttpDownloader
{
    private readonly string _baseDir;

    public HttpDownloader()
    {
        _baseDir = AppDomain.CurrentDomain.BaseDirectory;
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
    }

    /// <summary>True when the download was canceled through the cancellation token.</summary>
    public bool WasCanceled { get; private set; }

    /// <summary>Downloads and installs every step in the batch. Throws on failure (except cancel).</summary>
    public async Task DownloadAsync(DownloadBatch batch, Action<double, string> progress, CancellationToken ct)
    {
        if (batch == null || batch.Steps.Count == 0)
        {
            return;
        }

        WasCanceled = false;
        string sevenZip = Path.Combine(_baseDir, "7z.exe");
        if (!File.Exists(sevenZip))
        {
            throw new FileNotFoundException("7z.exe is needed to unpack downloads but was not found next to the app.", sevenZip);
        }

        double stepShare = 100.0 / batch.Steps.Count;
        for (int i = 0; i < batch.Steps.Count; i++)
        {
            DownloadStep step = batch.Steps[i];
            double stepBase = stepShare * i;
            Report(progress, stepBase, "Downloading " + step.Name + "...");
            string tempFile = Path.Combine(Path.GetTempPath(), "ez7z_dl_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zip");

            try
            {
                using (var client = new WebClient())
                {
                    var sw = Stopwatch.StartNew();
                    long lastBytes = 0;
                    DateTime lastCheck = DateTime.UtcNow;
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        double pct = stepBase + stepShare * e.ProgressPercentage / 100.0;
                        string speed = "";
                        DateTime now = DateTime.UtcNow;
                        double secs = (now - lastCheck).TotalSeconds;
                        if (secs >= 0.5)
                        {
                            double kbps = (e.BytesReceived - lastBytes) / 1024.0 / secs;
                            speed = kbps > 1024.0 ? string.Format("({0:F1} MB/s)", kbps / 1024.0) : string.Format("({0:F0} KB/s)", kbps);
                            lastBytes = e.BytesReceived;
                            lastCheck = now;
                        }
                        string sizeText = e.TotalBytesToReceive > 0
                            ? string.Format("{0:F1} / {1:F1} MB", e.BytesReceived / 1048576.0, e.TotalBytesToReceive / 1048576.0)
                            : string.Format("{0:F1} MB", e.BytesReceived / 1048576.0);
                        Report(progress, pct, "Downloading " + step.Name + "... " + sizeText + " " + speed);
                    };
                    using (ct.Register(() => client.CancelAsync()))
                    {
                        await client.DownloadFileTaskAsync(step.Url, tempFile);
                    }
                }

                if (string.IsNullOrEmpty(step.ExtractDir))
                {
                    // Download-only step: hand the temp archive to the caller.
                    step.ResultTempFile = tempFile;
                    tempFile = null; // caller owns cleanup now
                }
                else
                {
                    Report(progress, stepBase + stepShare * 0.95, "Installing " + step.Name + "...");
                    Directory.CreateDirectory(step.ExtractDir);
                    RunSevenZip(sevenZip, "x -y -o\"" + step.ExtractDir + "\" \"" + tempFile + "\"");

                    // Archives may nest the payload in a subfolder — make sure the
                    // wanted file ends up at the location the app expects.
                    if (!string.IsNullOrEmpty(step.WantedFile))
                    {
                        EnsureWantedFileAtRoot(step.ExtractDir, step.WantedFile);
                        if (!File.Exists(Path.Combine(step.ExtractDir, step.WantedFile)))
                        {
                            throw new InvalidOperationException(step.Name + " archive did not contain " + step.WantedFile + ".");
                        }
                    }

                    if (batch.AfterExtract != null)
                    {
                        batch.AfterExtract(step);
                    }
                }
            }
            catch (WebException wex) when (ct.IsCancellationRequested)
            {
                WasCanceled = true;
                throw new OperationCanceledException("Download canceled.", wex, ct);
            }
            catch (OperationCanceledException)
            {
                WasCanceled = true;
                throw;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                }
            }
        }

        Report(progress, 100.0, "All downloads installed.");
    }

    // ---------------- helpers ----------------

    private static void EnsureWantedFileAtRoot(string dir, string fileName)
    {
        string rootPath = Path.Combine(dir, fileName);
        if (File.Exists(rootPath))
        {
            return;
        }
        string found = FindFileRecursive(dir, fileName);
        if (found != null && !string.Equals(found, rootPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(found, rootPath, overwrite: true);
        }
    }

    private static string FindFileRecursive(string root, string fileName)
    {
        try
        {
            foreach (string f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return f;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    private static void RunSevenZip(string sevenZipExe, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = sevenZipExe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using (Process p = Process.Start(psi))
        {
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                throw new InvalidOperationException("Extraction failed (7z exit " + p.ExitCode + ").");
            }
        }
    }

    /// <summary>Invokes the progress callback, swallowing any callback errors.
    /// (The callback is responsible for marshaling to the UI thread itself.)</summary>
    private static void Report(Action<double, string> progress, double pct, string message)
    {
        try
        {
            progress?.Invoke(Math.Max(0, Math.Min(100, pct)), message);
        }
        catch
        {
        }
    }
}

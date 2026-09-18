using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Easy7ZipModern.Models;

namespace Easy7ZipModern.Services;

public class UpdateService
{
    private readonly string _baseDir;

    /// <summary>Source repo for all UniExtract plugin bundles (user-requested fork).</summary>
    public const string UniExtractReleaseUrl = "https://github.com/gvp9000/UniExtract2/releases/download/v3.0.4/UniExtract2.zip";

    private static string ComponentsFilePath
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Easy7ZipModern");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, "components.json");
        }
    }

    public UpdateService()
    {
        _baseDir = AppDomain.CurrentDomain.BaseDirectory;
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
    }

    public ObservableCollection<ComponentItem> GetDefaultComponents()
    {
        var list = new ObservableCollection<ComponentItem>();
        var defaults = BuildDefaults();

        // Merge persisted user edits (edited URLs + custom plugins) over the defaults.
        try
        {
            if (File.Exists(ComponentsFilePath))
            {
                var serializer = new JavaScriptSerializer();
                var saved = serializer.Deserialize<List<ComponentItem>>(File.ReadAllText(ComponentsFilePath));
                if (saved != null)
                {
                    // 1. apply edited URLs to defaults (matched by Id)
                    foreach (var d in defaults)
                    {
                        var match = saved.Find(s => s.Id == d.Id && !s.IsCustom);
                        if (match != null && !string.IsNullOrWhiteSpace(match.DownloadUrl))
                        {
                            d.DownloadUrl = match.DownloadUrl;
                            d.IsUrlEdited = match.IsUrlEdited;
                        }
                    }
                    // 2. re-add user plugins
                    foreach (var s in saved)
                    {
                        if (s.IsCustom)
                        {
                            s.CanUpdate = !File.Exists(s.VerificationFile);
                            s.Status = File.Exists(s.VerificationFile) ? "Installed (Ready)" : "Not Installed";
                            s.InstalledVersion = File.Exists(s.VerificationFile) ? s.LatestVersion : "Missing";
                            list.Add(s);
                        }
                    }
                }
            }
        }
        catch
        {
            // Corrupt/missing components.json — fall back to pure defaults.
        }

        foreach (var d in defaults)
        {
            list.Add(d);
        }
        return list;
    }

    private List<ComponentItem> BuildDefaults()
    {
        var list = new List<ComponentItem>();

        list.Add(new ComponentItem
        {
            Id = "7z-core",
            Name = "7-Zip Core Engine",
            Category = "Core",
            Description = "High-speed compression/extraction engine supporting 7z, ZIP, RAR, TAR, GZ, XZ, and ISO.",
            DownloadUrl = "https://www.7-zip.org/a/7z2408-x64.exe",
            DestinationFolder = _baseDir,
            VerificationFile = Path.Combine(_baseDir, "7z.dll"),
            SupportedFormats = "7z, zip, rar, tar, gz, xz, bz2, iso, cab, wim",
            InstalledVersion = File.Exists(Path.Combine(_baseDir, "7z.dll")) ? "16.04" : "Missing",
            LatestVersion = "24.08",
            Status = File.Exists(Path.Combine(_baseDir, "7z.dll")) ? "Installed (Up to date)" : "Update Available",
            CanUpdate = true
        });

        list.Add(new ComponentItem
        {
            Id = "codecs-zstd",
            Name = "Zstandard, Brotli & LZ4 Codecs Pack",
            Category = "Codecs",
            Description = "High-speed modern compression codecs: Facebook Zstandard (.zst), Google Brotli (.br), and LZ4.",
            DownloadUrl = "https://github.com/mcmilk/7-Zip-zstd/releases/download/v26.02-v1.5.7-R2/Codecs-x64.7z",
            DestinationFolder = Path.Combine(_baseDir, "Codecs"),
            VerificationFile = Path.Combine(_baseDir, "Codecs", "zstd.dll"),
            SupportedFormats = "zst, tzst, br, lz4, lz5, lizard, flzma2",
            InstalledVersion = File.Exists(Path.Combine(_baseDir, "Codecs", "zstd.dll")) ? "26.02" : "Missing",
            LatestVersion = "26.02",
            Status = File.Exists(Path.Combine(_baseDir, "Codecs", "zstd.dll")) ? "Installed (Up to date)" : "Missing",
            CanUpdate = !File.Exists(Path.Combine(_baseDir, "Codecs", "zstd.dll"))
        });

        // All UniExtract plugin bundles now come from the maintained fork:
        // https://github.com/gvp9000/UniExtract2/releases
        list.Add(new ComponentItem
        {
            Id = "innounp",
            Name = "Inno Setup Unpacker Engine",
            Category = "UniExtract Plugin",
            Description = "Extracts files directly from Inno Setup installers without running the installer executable.",
            DownloadUrl = UniExtractReleaseUrl,
            DestinationFolder = _baseDir,
            VerificationFile = Path.Combine(_baseDir, "bin", "innounp.exe"),
            SupportedFormats = "Inno Setup .exe installers",
            InstalledVersion = File.Exists(Path.Combine(_baseDir, "bin", "innounp.exe")) ? "0.50" : "Missing",
            LatestVersion = "3.0.4",
            Status = File.Exists(Path.Combine(_baseDir, "bin", "innounp.exe")) ? "Installed" : "Missing",
            CanUpdate = !File.Exists(Path.Combine(_baseDir, "bin", "innounp.exe"))
        });

        list.Add(new ComponentItem
        {
            Id = "unshield",
            Name = "InstallShield Cabinet Unpacker",
            Category = "UniExtract Plugin",
            Description = "Extracts files from InstallShield Cabinet (.cab and .hdr) setup containers.",
            DownloadUrl = UniExtractReleaseUrl,
            DestinationFolder = _baseDir,
            VerificationFile = Path.Combine(_baseDir, "bin", "unshield.exe"),
            SupportedFormats = "InstallShield .cab, .hdr",
            InstalledVersion = File.Exists(Path.Combine(_baseDir, "bin", "unshield.exe")) ? "1.4" : "Missing",
            LatestVersion = "3.0.4",
            Status = File.Exists(Path.Combine(_baseDir, "bin", "unshield.exe")) ? "Installed" : "Missing",
            CanUpdate = !File.Exists(Path.Combine(_baseDir, "bin", "unshield.exe"))
        });

        list.Add(new ComponentItem
        {
            Id = "lessmsi",
            Name = "Microsoft MSI / MSP Extractor (LessMSI)",
            Category = "UniExtract Plugin",
            Description = "Unpacks Microsoft Windows Installer packages (.msi) and patches (.msp) without system installation.",
            DownloadUrl = UniExtractReleaseUrl,
            DestinationFolder = _baseDir,
            VerificationFile = Path.Combine(_baseDir, "bin", "lessmsi", "lessmsi.exe"),
            SupportedFormats = ".msi, .msp",
            InstalledVersion = File.Exists(Path.Combine(_baseDir, "bin", "lessmsi", "lessmsi.exe")) ? "1.6.1" : "Missing",
            LatestVersion = "3.0.4",
            Status = File.Exists(Path.Combine(_baseDir, "bin", "lessmsi", "lessmsi.exe")) ? "Installed" : "Missing",
            CanUpdate = !File.Exists(Path.Combine(_baseDir, "bin", "lessmsi", "lessmsi.exe"))
        });

        list.Add(new ComponentItem
        {
            Id = "peazip",
            Name = "PeaZip Archive Engine (PEA)",
            Category = "UniExtract Plugin",
            Description = "Decompresses PEA archives, ARC, and specialized multi-volume pack containers.",
            DownloadUrl = UniExtractReleaseUrl,
            DestinationFolder = _baseDir,
            VerificationFile = Path.Combine(_baseDir, "bin", "pea.exe"),
            SupportedFormats = ".pea, .arc",
            InstalledVersion = File.Exists(Path.Combine(_baseDir, "bin", "pea.exe")) ? "1.1.4" : "Missing",
            LatestVersion = "3.0.4",
            Status = File.Exists(Path.Combine(_baseDir, "bin", "pea.exe")) ? "Installed" : "Missing",
            CanUpdate = !File.Exists(Path.Combine(_baseDir, "bin", "pea.exe"))
        });

        list.Add(new ComponentItem
        {
            Id = "ewise",
            Name = "Wise Setup Installer Unpacker",
            Category = "UniExtract Plugin",
            Description = "Decompresses legacy Wise installer packages without executing the setup.",
            DownloadUrl = UniExtractReleaseUrl,
            DestinationFolder = _baseDir,
            VerificationFile = Path.Combine(_baseDir, "bin", "E_WISE_W.EXE"),
            SupportedFormats = "Wise Setup .exe",
            InstalledVersion = File.Exists(Path.Combine(_baseDir, "bin", "E_WISE_W.EXE")) ? "2002.7" : "Missing",
            LatestVersion = "3.0.4",
            Status = File.Exists(Path.Combine(_baseDir, "bin", "E_WISE_W.EXE")) ? "Installed" : "Missing",
            CanUpdate = !File.Exists(Path.Combine(_baseDir, "bin", "E_WISE_W.EXE"))
        });

        list.Add(new ComponentItem
        {
            Id = "enigma",
            Name = "Enigma VirtualBox Unpacker",
            Category = "UniExtract Plugin",
            Description = "Extracts embedded files from Enigma VirtualBox single-executable packages.",
            DownloadUrl = UniExtractReleaseUrl,
            DestinationFolder = _baseDir,
            VerificationFile = Path.Combine(_baseDir, "bin", "EnigmaVBUnpacker.exe"),
            SupportedFormats = "Enigma VirtualBox .exe",
            InstalledVersion = File.Exists(Path.Combine(_baseDir, "bin", "EnigmaVBUnpacker.exe")) ? "0.61" : "Missing",
            LatestVersion = "3.0.4",
            Status = File.Exists(Path.Combine(_baseDir, "bin", "EnigmaVBUnpacker.exe")) ? "Installed" : "Missing",
            CanUpdate = !File.Exists(Path.Combine(_baseDir, "bin", "EnigmaVBUnpacker.exe"))
        });

        list.Add(new ComponentItem
        {
            Id = "garbro",
            Name = "Game Archive Formats Engine (GARbro)",
            Category = "UniExtract Plugin",
            Description = "Unpacks visual novel and game data archives across over 100 formats.",
            DownloadUrl = UniExtractReleaseUrl,
            DestinationFolder = _baseDir,
            VerificationFile = Path.Combine(_baseDir, "bin", "GARbro", "ArcFormats.dll"),
            SupportedFormats = "Game archives & visual novel assets",
            InstalledVersion = File.Exists(Path.Combine(_baseDir, "bin", "GARbro", "ArcFormats.dll")) ? "1.5.44" : "Missing",
            LatestVersion = "3.0.4",
            Status = File.Exists(Path.Combine(_baseDir, "bin", "GARbro", "ArcFormats.dll")) ? "Installed" : "Missing",
            CanUpdate = !File.Exists(Path.Combine(_baseDir, "bin", "GARbro", "ArcFormats.dll"))
        });

        list.Add(new ComponentItem
        {
            Id = "identifiers",
            Name = "Format & Signature Detectors (TrID / ExeInfo)",
            Category = "Identifiers",
            Description = "Binary signature scanners to accurately detect file types even without extensions.",
            DownloadUrl = UniExtractReleaseUrl,
            DestinationFolder = _baseDir,
            VerificationFile = Path.Combine(_baseDir, "bin", "exeinfope.exe"),
            SupportedFormats = "Binary signatures, magic headers",
            InstalledVersion = File.Exists(Path.Combine(_baseDir, "bin", "exeinfope.exe")) ? "0.0.8" : "Missing",
            LatestVersion = "3.0.4",
            Status = File.Exists(Path.Combine(_baseDir, "bin", "exeinfope.exe")) ? "Installed" : "Missing",
            CanUpdate = !File.Exists(Path.Combine(_baseDir, "bin", "exeinfope.exe"))
        });

        return list;
    }

    public async Task DownloadComponentAsync(ComponentItem item, CancellationToken ct = default(CancellationToken))
    {
        if (item == null || string.IsNullOrWhiteSpace(item.DownloadUrl))
        {
            return;
        }

        item.IsDownloading = true;
        item.Status = "Downloading...";
        item.DownloadProgress = 0.0;
        item.DownloadSpeed = "Connecting...";

        string ext = Path.GetExtension(new Uri(item.DownloadUrl).AbsolutePath);
        if (string.IsNullOrEmpty(ext))
        {
            ext = ".zip";
        }
        string tempFile = Path.Combine(Path.GetTempPath(), item.Id + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ext);
        var sw = Stopwatch.StartNew();
        long lastBytes = 0;
        DateTime lastSpeedCheck = DateTime.UtcNow;

        try
        {
            using (var client = new WebClient())
            {
                client.DownloadProgressChanged += (s, e) =>
                {
                    item.DownloadProgress = e.ProgressPercentage;
                    double rec = e.BytesReceived / 1048576.0;
                    double tot = e.TotalBytesToReceive / 1048576.0;
                    item.ProgressText = string.Format("{0:F1} MB / {1:F1} MB ({2}%)", rec, tot, e.ProgressPercentage);
                    DateTime now = DateTime.UtcNow;
                    double secs = (now - lastSpeedCheck).TotalSeconds;
                    if (secs >= 0.5)
                    {
                        double kbps = (e.BytesReceived - lastBytes) / 1024.0 / secs;
                        item.DownloadSpeed = kbps > 1024.0 ? string.Format("{0:F2} MB/s", kbps / 1024.0) : string.Format("{0:F0} KB/s", kbps);
                        lastBytes = e.BytesReceived;
                        lastSpeedCheck = now;
                    }
                };
                ct.Register(() => client.CancelAsync());
                await client.DownloadFileTaskAsync(item.DownloadUrl, tempFile);
            }

            item.Status = "Extracting & Installing...";
            item.DownloadProgress = 100.0;
            item.DownloadSpeed = "Finalizing";
            await Task.Run(() => InstallDownloadedComponent(item, tempFile));

            // success = the verification file must exist
            if (File.Exists(item.VerificationFile))
            {
                item.Status = "Installed (Ready)";
                item.InstalledVersion = item.LatestVersion;
                item.CanUpdate = false;
            }
            else
            {
                item.Status = "Downloaded, but verification file not found. Check the URL/verification path.";
            }
        }
        catch (OperationCanceledException)
        {
            item.Status = "Canceled";
        }
        catch (Exception ex)
        {
            item.Status = "Error: " + ex.Message;
        }
        finally
        {
            item.IsDownloading = false;
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

    private void InstallDownloadedComponent(ComponentItem item, string tempFile)
    {
        string sevenZip = Path.Combine(_baseDir, "7z.exe");

        if (!Directory.Exists(item.DestinationFolder))
        {
            Directory.CreateDirectory(item.DestinationFolder);
        }

        if (item.Id == "7z-core")
        {
            // Self-extracting 7-Zip installer — extract its payload to a temp dir,
            // then copy the core binaries next to the app.
            string unpack = Path.Combine(Path.GetTempPath(), "7z_unpack_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(unpack);
            RunSevenZip(sevenZip, $"x -y -o\"{unpack}\" \"{tempFile}\"");
            try
            {
                if (File.Exists(Path.Combine(unpack, "7z.dll")))
                {
                    File.Copy(Path.Combine(unpack, "7z.dll"), Path.Combine(_baseDir, "7z.dll"), overwrite: true);
                    File.Copy(Path.Combine(unpack, "7z.exe"), Path.Combine(_baseDir, "7z.exe"), overwrite: true);
                }
            }
            catch
            {
            }
            try
            {
                Directory.Delete(unpack, recursive: true);
            }
            catch
            {
            }
            return;
        }

        if (item.Id == "codecs-zstd")
        {
            // Codec DLLs belong directly in the Codecs folder.
            RunSevenZip(sevenZip, $"x -y -o\"{item.DestinationFolder}\" \"{tempFile}\"");
            return;
        }

        // ---------- UniExtract plugin bundle + custom plugins ----------
        // The UniExtract2.zip bundle packs everything under a top-level folder
        // (bin/innounp.exe, bin/unshield.exe, ...). Extract the whole bundle into a
        // staging dir, then place the verification file's folder-tree into the
        // component's DestinationFolder so the paths match what ExtractorEngine expects.
        string stage = Path.Combine(Path.GetTempPath(), "ez7z_stage_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            RunSevenZip(sevenZip, $"x -y -o\"{stage}\" \"{tempFile}\"");

            // Find the verification file inside the staged bundle tree,
            // then copy its containing folder over the destination folder.
            // This keeps the layout the ExtractorEngine expects:
            //   bin\innounp.exe, bin\lessmsi\lessmsi.exe, bin\GARbro\ArcFormats.dll, ...
            string stagedVerify = FindFileIgnoreCase(stage, Path.GetFileName(item.VerificationFile));
            if (stagedVerify == null)
            {
                // Verification file not in the bundle — dump everything into destination anyway.
                CopyDirectory(stage, item.DestinationFolder);
                return;
            }

            string destVerifyDir = Path.GetDirectoryName(item.VerificationFile);
            Directory.CreateDirectory(destVerifyDir);
            string stagedDir = Path.GetDirectoryName(stagedVerify);
            CopyDirectory(stagedDir, destVerifyDir);

            // Belt & braces: if the file still isn't at the expected spot, search again.
            EnsureVerificationFileExists(item, stage);
        }
        finally
        {
            try
            {
                Directory.Delete(stage, recursive: true);
            }
            catch
            {
            }
        }
    }

    private void EnsureVerificationFileExists(ComponentItem item, string stage)
    {
        if (File.Exists(item.VerificationFile))
        {
            return;
        }
        // last-resort: search the staged tree for the file and copy its parent dir over
        string stagedVerify = FindFileIgnoreCase(stage, Path.GetFileName(item.VerificationFile));
        if (stagedVerify != null)
        {
            string destDir = Path.GetDirectoryName(item.VerificationFile);
            Directory.CreateDirectory(destDir);
            CopyDirectory(Path.GetDirectoryName(stagedVerify), destDir);
        }
    }

    private static void RunSevenZip(string sevenZipExe, string args)
    {
        if (!File.Exists(sevenZipExe))
        {
            throw new FileNotFoundException("7z.exe not found next to the application — needed to unpack components.", sevenZipExe);
        }
        var psi = new ProcessStartInfo
        {
            FileName = sevenZipExe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using (var p = Process.Start(psi))
        {
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                throw new InvalidOperationException($"Extraction failed (exit {p.ExitCode}).");
            }
        }
    }

    private static string FindDirIgnoreCase(string root, string dirName)
    {
        try
        {
            foreach (string d in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetFileName(d), dirName, StringComparison.OrdinalIgnoreCase))
                {
                    return d;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    private static string FindFileIgnoreCase(string root, string fileName)
    {
        try
        {
            foreach (string f in Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories))
            {
                return f;
            }
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

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (string f in Directory.GetFiles(src))
        {
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), overwrite: true);
        }
        foreach (string d in Directory.GetDirectories(src))
        {
            CopyDirectory(d, Path.Combine(dst, Path.GetFileName(d)));
        }
    }

    // ---------------- persistence ----------------

    public void SaveComponents(ObservableCollection<ComponentItem> components)
    {
        try
        {
            var toSave = new List<ComponentItem>();
            foreach (var c in components)
            {
                if (c.IsCustom || c.IsUrlEdited)
                {
                    toSave.Add(c);
                }
            }
            var serializer = new JavaScriptSerializer();
            File.WriteAllText(ComponentsFilePath, serializer.Serialize(toSave));
        }
        catch
        {
        }
    }

    public void ResetComponents()
    {
        try
        {
            if (File.Exists(ComponentsFilePath))
            {
                File.Delete(ComponentsFilePath);
            }
        }
        catch
        {
        }
    }
}

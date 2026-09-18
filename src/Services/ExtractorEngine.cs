using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Easy7ZipModern.Models;

namespace Easy7ZipModern.Services;

public class ExtractorEngine
{
    private readonly string _baseDir;
    private readonly string _sevenZipPath;
    private readonly string _binDir;
    private readonly PasswordVaultService _vaultService;

    public ExtractorEngine(PasswordVaultService vaultService = null)
    {
        _baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _sevenZipPath = Path.Combine(_baseDir, "7z.exe");
        _binDir = Path.Combine(_baseDir, "bin");
        _vaultService = vaultService;
        Environment.SetEnvironmentVariable("PATH", _baseDir + ";" + _binDir + ";" + Environment.GetEnvironmentVariable("PATH"));
    }

    public static readonly HashSet<string> ZipVariantExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Java & Android ZIP variants
        ".zip", ".zipx", ".jar", ".war", ".ear", ".aar", ".apk", ".apks", ".apkm", ".xapk", ".aab", ".jmod",
        // Office & Document ZIP variants
        ".docx", ".docm", ".dotx", ".dotm", ".xlsx", ".xlsm", ".xltx", ".xltm", ".xlsb",
        ".pptx", ".pptm", ".potx", ".potm", ".ppsx", ".ppsm", ".odt", ".ods", ".odp", ".odg",
        // Comic & eBook ZIP variants
        ".cbz", ".cbr", ".cb7", ".cbt", ".cba", ".epub",
        // Extension & Package ZIP variants
        ".xpi", ".crx", ".vsix", ".nupkg", ".snupkg", ".whl", ".egg", ".gem",
        ".msix", ".msixbundle", ".appx", ".appxbundle",
        // Game & Application ZIP containers
        ".pak", ".pk3", ".pk4", ".ipk", ".mcworld", ".mctemplate", ".mcpack", ".mcaddon",
        ".osz", ".osk", ".kmz", ".wal", ".wsz"
    };

    public static bool HasZipSignature(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return false;
        }
        try
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < 4) return false;

                byte[] head = new byte[4];
                int read = fs.Read(head, 0, 4);
                if (read == 4)
                {
                    // PK\x03\x04 (standard zip header)
                    if (head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04) return true;
                    // PK\x05\x06 (empty zip / EOCD)
                    if (head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x05 && head[3] == 0x06) return true;
                    // PK\x07\x08 (spanned zip header)
                    if (head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x07 && head[3] == 0x08) return true;
                }

                // Also check for End of Central Directory record (PK\x05\x06) in trailing bytes
                // (handles self-extracting ZIPs or embedded ZIP containers with arbitrary prefixes)
                if (fs.Length > 22)
                {
                    int scanLen = (int)Math.Min(fs.Length, 65557L);
                    fs.Seek(-scanLen, SeekOrigin.End);
                    byte[] tail = new byte[scanLen];
                    int tailRead = fs.Read(tail, 0, scanLen);
                    for (int i = 0; i <= tailRead - 4; i++)
                    {
                        if (tail[i] == 0x50 && tail[i + 1] == 0x4B && tail[i + 2] == 0x05 && tail[i + 3] == 0x06)
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch
        {
        }
        return false;
    }

    public string DetectEngine(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return "Unknown";
        }
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        switch (ext)
        {
            case ".msi":
            case ".msp":
                {
                    string lessmsi = Path.Combine(_binDir, "lessmsi", "lessmsi.exe");
                    return File.Exists(lessmsi) ? "LessMSI" : "7-Zip";
                }
            case ".cab":
            case ".hdr":
                try
                {
                    using (FileStream fs = File.OpenRead(filePath))
                    {
                        byte[] hdr = new byte[4];
                        if (fs.Read(hdr, 0, 4) == 4 && hdr[0] == 'I' && hdr[1] == 'S' && hdr[2] == 'c' && hdr[3] == '(')
                        {
                            string unshield = Path.Combine(_binDir, "unshield.exe");
                            if (File.Exists(unshield))
                            {
                                return "Unshield";
                            }
                        }
                    }
                }
                catch
                {
                }
                break;
        }

        if (ext == ".exe")
        {
            try
            {
                using (FileStream fs = File.OpenRead(filePath))
                {
                    byte[] buf = new byte[4096];
                    int len = fs.Read(buf, 0, buf.Length);
                    string head = Encoding.ASCII.GetString(buf, 0, len);
                    if (head.Contains("Inno") || FileContainsString(filePath, "Inno Setup"))
                    {
                        string innounp = Path.Combine(_binDir, "innounp.exe");
                        if (File.Exists(innounp))
                        {
                            return "InnoUnp";
                        }
                    }
                    if (FileContainsString(filePath, "WiseMain") || FileContainsString(filePath, "WISE Installation"))
                    {
                        string wise = Path.Combine(_binDir, "E_WISE_W.EXE");
                        if (File.Exists(wise))
                        {
                            return "Wise";
                        }
                    }
                }
            }
            catch
            {
            }
        }

        if (ext == ".pea")
        {
            string pea = Path.Combine(_binDir, "pea.exe");
            if (File.Exists(pea))
            {
                return "PeaZip";
            }
        }
        if (ext == ".rpa")
        {
            string unrpa = Path.Combine(_binDir, "unrpa.exe");
            if (File.Exists(unrpa))
            {
                return "UnRPA";
            }
        }

        if (ZipVariantExtensions.Contains(ext) || HasZipSignature(filePath))
        {
            return "7-Zip (ZIP Container)";
        }

        return "7-Zip";
    }

    private static bool FileContainsString(string path, string search)
    {
        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] buf = new byte[65536];
                byte[] needle = Encoding.ASCII.GetBytes(search);
                int len;
                while ((len = fs.Read(buf, 0, buf.Length)) > 0)
                {
                    if (IndexOfSequence(buf, len, needle) >= 0)
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
        }
        return false;
    }

    private static int IndexOfSequence(byte[] buffer, int length, byte[] needle)
    {
        for (int i = 0; i <= length - needle.Length; i++)
        {
            bool ok = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (buffer[i + j] != needle[j])
                {
                    ok = false;
                    break;
                }
            }
            if (ok)
            {
                return i;
            }
        }
        return -1;
    }

    public List<ArchiveItem> ListArchive(string archivePath, string password = null)
    {
        if (!File.Exists(archivePath))
        {
            return new List<ArchiveItem>();
        }

        string pwdArg = string.IsNullOrEmpty(password) ? "" : $" -p\"{password}\"";
        string defaultArgs = $"l -slt \"{archivePath}\"{pwdArg}";
        var items = ParseListOutput(defaultArgs, archivePath);

        // If 7-Zip couldn't detect or open the archive format (unknown extension, custom container),
        // treat any unknown format as ZIP and retry with -tzip flag
        if (items.Count == 0)
        {
            string zipArgs = $"l -slt -tzip \"{archivePath}\"{pwdArg}";
            var zipItems = ParseListOutput(zipArgs, archivePath);
            if (zipItems.Count > 0)
            {
                return zipItems;
            }
        }

        return items;
    }

    private List<ArchiveItem> ParseListOutput(string args, string archivePath)
    {
        var items = new List<ArchiveItem>();
        var psi = new ProcessStartInfo
        {
            FileName = _sevenZipPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        string archiveFullPath = Path.GetFullPath(archivePath);

        using (Process p = Process.Start(psi))
        {
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            string[] lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            ArchiveItem current = null;
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (current != null && !string.IsNullOrEmpty(current.Path))
                    {
                        // 7-Zip lists the archive itself as the first entry - skip it.
                        bool isSelf = string.Equals(current.Path, archivePath, StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(current.Path, archiveFullPath, StringComparison.OrdinalIgnoreCase);
                        if (!isSelf)
                        {
                            items.Add(current);
                        }
                        current = null;
                    }
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();

                if (key == "Path")
                {
                    if (current != null && !string.IsNullOrEmpty(current.Path))
                    {
                        bool isSelf = string.Equals(current.Path, archivePath, StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(current.Path, archiveFullPath, StringComparison.OrdinalIgnoreCase);
                        if (!isSelf)
                        {
                            items.Add(current);
                        }
                    }
                    current = new ArchiveItem
                    {
                        Path = val,
                        Name = Path.GetFileName(val),
                        Extension = Path.GetExtension(val)
                    };
                    continue;
                }
                if (current == null)
                {
                    continue;
                }
                switch (key)
                {
                    case "Folder":
                        current.IsDirectory = val == "+";
                        if (current.IsDirectory && string.IsNullOrEmpty(current.Name))
                        {
                            current.Name = val;
                        }
                        break;
                    case "Size":
                        if (long.TryParse(val, out long size))
                        {
                            current.Size = size;
                        }
                        break;
                    case "Packed Size":
                        if (long.TryParse(val, out long psize))
                        {
                            current.CompressedSize = psize;
                        }
                        break;
                    case "Modified":
                        if (DateTime.TryParse(val, out DateTime mod))
                        {
                            current.Modified = mod;
                        }
                        break;
                    case "Attributes":
                        current.Attributes = val;
                        if (val.Contains("D"))
                        {
                            current.IsDirectory = true;
                        }
                        break;
                    case "Encrypted":
                        current.IsEncrypted = val == "+";
                        break;
                }
            }
            if (current != null && !string.IsNullOrEmpty(current.Path))
            {
                bool isSelf = string.Equals(current.Path, archivePath, StringComparison.OrdinalIgnoreCase)
                           || string.Equals(current.Path, archiveFullPath, StringComparison.OrdinalIgnoreCase);
                if (!isSelf)
                {
                    items.Add(current);
                }
            }
        }
        return items;
    }

    /// <summary>Direct children of a folder inside a listed archive. Entries
    /// deeper than one level are collapsed into synthetic folder rows.</summary>
    public static List<ArchiveItem> ListChildren(List<ArchiveItem> entries, string folderPath)
    {
        folderPath = (folderPath ?? "").Replace('/', '\\').TrimEnd('\\');
        var result = new List<ArchiveItem>();
        var explicitDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Pass 1: names of explicit directory entries that are direct children,
        // so deeper files don't create a duplicate synthetic row for them.
        foreach (ArchiveItem entry in entries)
        {
            string p = (entry.Path ?? "").Replace('/', '\\').TrimEnd('\\');
            if (p.Length == 0)
            {
                continue;
            }
            if (folderPath.Length > 0)
            {
                if (!p.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase) ||
                    (p.Length > folderPath.Length && p[folderPath.Length] != '\\'))
                {
                    continue;
                }
            }
            string rest = folderPath.Length == 0 ? p : p.Substring(folderPath.Length).TrimStart('\\');
            if (rest.Length > 0 && rest.IndexOf('\\') < 0 && entry.IsDirectory)
            {
                explicitDirs.Add(rest);
            }
        }

        // Pass 2: build the child list.
        foreach (ArchiveItem entry in entries)
        {
            string p = (entry.Path ?? "").Replace('/', '\\').TrimEnd('\\');
            if (p.Length == 0)
            {
                continue;
            }
            if (folderPath.Length > 0)
            {
                if (!p.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (p.Length > folderPath.Length && p[folderPath.Length] != '\\')
                {
                    continue; // sibling like "docs2" when inside "docs"
                }
            }

            string rest = folderPath.Length == 0 ? p : p.Substring(folderPath.Length).TrimStart('\\');
            if (rest.Length == 0)
            {
                continue;
            }

            int slash = rest.IndexOf('\\');
            if (slash < 0)
            {
                if (addedNames.Add((entry.IsDirectory ? "d:" : "f:") + rest))
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        entry.Name = rest;
                    }
                    result.Add(entry);
                }
            }
            else
            {
                string dirName = rest.Substring(0, slash);
                string childPath = folderPath.Length == 0 ? dirName : folderPath + "\\" + dirName;
                if (!explicitDirs.Contains(dirName) && addedNames.Add("d:" + dirName))
                {
                    result.Add(new ArchiveItem { Name = dirName, Path = childPath, IsDirectory = true });
                }
            }
        }

        result.Sort((a, b) =>
        {
            int byType = b.IsDirectory.CompareTo(a.IsDirectory); // folders first
            return byType != 0
                ? byType
                : string.Compare(a.Name ?? "", b.Name ?? "", StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }

    /// <summary>Parent folder path ("" = archive root).</summary>
    public static string ParentFolderPath(string folderPath)
    {
        int idx = folderPath.LastIndexOf('\\');
        return idx < 0 ? "" : folderPath.Substring(0, idx);
    }

    public bool TestArchivePassword(string archivePath, string password)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _sevenZipPath,
                Arguments = string.Format("t -p\"{0}\" \"{1}\"", password ?? "", archivePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (Process p = Process.Start(psi))
            {
                p.WaitForExit(3000);
                return p.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    public async Task<ExtractionResult> ExtractAsync(string archivePath, string targetDir,
        Action<int, string> progressCallback, PasswordPromptHandler passwordPrompt = null, CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        var res = new ExtractionResult { OutputDirectory = targetDir, Success = false };

        if (!File.Exists(archivePath))
        {
            res.ErrorMessage = "Source archive file does not exist: " + archivePath;
            return res;
        }
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        res.EngineUsed = DetectEngine(archivePath);
        switch (res.EngineUsed)
        {
            case "InnoUnp":
                return await ExtractWithInnoUnp(archivePath, targetDir, progressCallback, sw, ct);
            case "Unshield":
                return await ExtractWithUnshield(archivePath, targetDir, progressCallback, sw, ct);
            case "LessMSI":
                return await ExtractWithLessMSI(archivePath, targetDir, progressCallback, sw, ct);
            default:
                return await ExtractWith7Zip(archivePath, targetDir, progressCallback, passwordPrompt, sw, ct);
        }
    }

    private async Task<ExtractionResult> ExtractWith7Zip(string archivePath, string targetDir,
        Action<int, string> progressCallback, PasswordPromptHandler passwordPrompt, Stopwatch sw, CancellationToken ct)
    {
        var res = new ExtractionResult { OutputDirectory = targetDir, EngineUsed = "7-Zip Core (+ Codecs)" };
        string workingPassword = null;
        bool needsPassword = false;

        // probe for encryption
        try
        {
            var probe = new ProcessStartInfo
            {
                FileName = _sevenZipPath,
                Arguments = $"t -p\"\" \"{archivePath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (Process p = Process.Start(probe))
            {
                string err = p.StandardError.ReadToEnd();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);
                if (p.ExitCode != 0 && (err.Contains("password") || output.Contains("password") || err.Contains("Can not open encrypted")))
                {
                    needsPassword = true;
                }
            }
        }
        catch
        {
        }

        if (needsPassword)
        {
            bool found = false;
            if (_vaultService != null)
            {
                foreach (string saved in _vaultService.Passwords)
                {
                    if (TestArchivePassword(archivePath, saved))
                    {
                        workingPassword = saved;
                        found = true;
                        break;
                    }
                }
            }
            if (!found && passwordPrompt != null)
            {
                workingPassword = passwordPrompt(archivePath, out bool remember);
                if (remember && _vaultService != null && !string.IsNullOrEmpty(workingPassword))
                {
                    _vaultService.AddPassword(workingPassword);
                }
            }
        }

        string args = $"x -y -bsp1 -o\"{targetDir}\" \"{archivePath}\"";
        if (!string.IsNullOrEmpty(workingPassword))
        {
            args += $" -p\"{workingPassword}\"";
        }

        var psi = new ProcessStartInfo
        {
            FileName = _sevenZipPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        int filesExtracted = 0;
        var sbErr = new StringBuilder();

        await Task.Run(() =>
        {
            using (var p = new Process { StartInfo = psi })
            {
                p.OutputDataReceived += (s, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data))
                    {
                        return;
                    }
                    string line = e.Data.Trim();
                    Match m = Regex.Match(line, "^\\s*(\\d+)%");
                    if (m.Success)
                    {
                        int pct = int.Parse(m.Groups[1].Value);
                        string item = line.Substring(m.Length).Trim();
                        progressCallback?.Invoke(pct, item);
                    }
                    else if (line.StartsWith("Extracting  "))
                    {
                        filesExtracted++;
                        progressCallback?.Invoke(-1, line.Substring(12).Trim());
                    }
                };
                p.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        sbErr.AppendLine(e.Data);
                    }
                };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                while (!p.HasExited)
                {
                    if (ct.IsCancellationRequested)
                    {
                        try { p.Kill(); } catch { }
                        break;
                    }
                    Thread.Sleep(50);
                }
                if (!p.HasExited)
                {
                    p.WaitForExit();
                }
                res.Success = p.ExitCode == 0;
                if (!res.Success)
                {
                    res.ErrorMessage = sbErr.Length > 0 ? sbErr.ToString() : "Extraction exited with code " + p.ExitCode;

                    // Fallback: retry with -tzip to treat unknown format or custom container as a ZIP archive
                    if (!ct.IsCancellationRequested)
                    {
                        string retryArgs = $"x -y -tzip -bsp1 -o\"{targetDir}\" \"{archivePath}\"";
                        if (!string.IsNullOrEmpty(workingPassword))
                        {
                            retryArgs += $" -p\"{workingPassword}\"";
                        }
                        var retryPsi = new ProcessStartInfo
                        {
                            FileName = _sevenZipPath,
                            Arguments = retryArgs,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.UTF8
                        };
                        int retryExtracted = 0;
                        using (var rp = new Process { StartInfo = retryPsi })
                        {
                            rp.OutputDataReceived += (s, e) =>
                            {
                                if (string.IsNullOrEmpty(e.Data)) return;
                                string line = e.Data.Trim();
                                Match m = Regex.Match(line, "^\\s*(\\d+)%");
                                if (m.Success)
                                {
                                    int pct = int.Parse(m.Groups[1].Value);
                                    string item = line.Substring(m.Length).Trim();
                                    progressCallback?.Invoke(pct, item);
                                }
                                else if (line.StartsWith("Extracting  "))
                                {
                                    retryExtracted++;
                                    progressCallback?.Invoke(-1, line.Substring(12).Trim());
                                }
                            };
                            rp.Start();
                            rp.BeginOutputReadLine();
                            while (!rp.HasExited)
                            {
                                if (ct.IsCancellationRequested)
                                {
                                    try { rp.Kill(); } catch { }
                                    break;
                                }
                                Thread.Sleep(50);
                            }
                            if (!rp.HasExited)
                            {
                                rp.WaitForExit();
                            }
                            if (rp.ExitCode == 0)
                            {
                                res.Success = true;
                                res.ErrorMessage = null;
                                res.EngineUsed = "7-Zip (ZIP Container)";
                                filesExtracted = retryExtracted;
                            }
                        }
                    }
                }
            }
        });

        sw.Stop();
        res.Duration = sw.Elapsed;
        res.FileCount = filesExtracted;
        return res;
    }

    private async Task<ExtractionResult> ExtractWithInnoUnp(string archivePath, string targetDir,
        Action<int, string> progressCallback, Stopwatch sw, CancellationToken ct)
    {
        var res = new ExtractionResult { OutputDirectory = targetDir, EngineUsed = "UniExtract InnoUnp" };
        string innounp = Path.Combine(_binDir, "innounp.exe");
        var psi = new ProcessStartInfo
        {
            FileName = innounp,
            Arguments = $"-x -y -d\"{targetDir}\" \"{archivePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        int count = 0;
        await Task.Run(() =>
        {
            using (Process p = Process.Start(psi))
            {
                while (!p.StandardOutput.EndOfStream)
                {
                    string line = p.StandardOutput.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        count++;
                        progressCallback?.Invoke(-1, line.Trim());
                    }
                }
                p.WaitForExit();
                res.Success = p.ExitCode == 0;
            }
        });
        sw.Stop();
        res.Duration = sw.Elapsed;
        res.FileCount = count;
        return res;
    }

    private async Task<ExtractionResult> ExtractWithUnshield(string archivePath, string targetDir,
        Action<int, string> progressCallback, Stopwatch sw, CancellationToken ct)
    {
        var res = new ExtractionResult { OutputDirectory = targetDir, EngineUsed = "UniExtract Unshield" };
        string unshield = Path.Combine(_binDir, "unshield.exe");
        var psi = new ProcessStartInfo
        {
            FileName = unshield,
            Arguments = $"x -d \"{targetDir}\" \"{archivePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        int count = 0;
        await Task.Run(() =>
        {
            using (Process p = Process.Start(psi))
            {
                while (!p.StandardOutput.EndOfStream)
                {
                    string line = p.StandardOutput.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        count++;
                        progressCallback?.Invoke(-1, line.Trim());
                    }
                }
                p.WaitForExit();
                res.Success = p.ExitCode == 0;
            }
        });
        sw.Stop();
        res.Duration = sw.Elapsed;
        res.FileCount = count;
        return res;
    }

    private async Task<ExtractionResult> ExtractWithLessMSI(string archivePath, string targetDir,
        Action<int, string> progressCallback, Stopwatch sw, CancellationToken ct)
    {
        var res = new ExtractionResult { OutputDirectory = targetDir, EngineUsed = "UniExtract LessMSI" };
        string lessmsi = Path.Combine(_binDir, "lessmsi", "lessmsi.exe");
        var psi = new ProcessStartInfo
        {
            FileName = lessmsi,
            Arguments = $"x \"{archivePath}\" \"{targetDir}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        int count = 0;
        await Task.Run(() =>
        {
            using (Process p = Process.Start(psi))
            {
                while (!p.StandardOutput.EndOfStream)
                {
                    string line = p.StandardOutput.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        count++;
                        progressCallback?.Invoke(-1, line.Trim());
                    }
                }
                p.WaitForExit();
                res.Success = p.ExitCode == 0;
            }
        });
        sw.Stop();
        res.Duration = sw.Elapsed;
        res.FileCount = count;
        return res;
    }
}

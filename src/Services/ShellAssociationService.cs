using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Easy7ZipModern.Models;
using Microsoft.Win32;

namespace Easy7ZipModern.Services;

public class ShellAssociationService
{
    // Notify Explorer that file associations / context menu entries changed.
    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = false)]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    /// <summary>Tells Explorer to refresh context menus and file associations.</summary>
    public static void NotifyShell()
    {
        try { SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); }
        catch { /* non-critical */ }
    }

    private static readonly string[] ArchiveExtensions = new[]
    {
        // Standard & compressed archives
        ".zip", ".zipx", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".tbz2", ".bzip2",
        ".xz", ".txz", ".zst", ".tzst", ".zstd", ".lzma", ".tlz", ".lz", ".lzh", ".lha",
        ".arj", ".cab", ".wim", ".swm", ".esd", ".iso", ".img", ".vhd", ".vhdx", ".vmdk",
        ".dmg", ".hfs", ".xar", ".pkg", ".deb", ".rpm", ".pea", ".chm",
        // Java & Android ZIP variants
        ".jar", ".war", ".ear", ".aar", ".apk", ".apks", ".apkm", ".xapk", ".aab", ".jmod",
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

    private static readonly string[] ShellTargets = new[]
    {
        "*", "Directory", "Folder", "AllFilesystemObjects"
    };

    private static string GetExePath()
    {
        return Assembly.GetEntryAssembly()?.Location ?? Process.GetCurrentProcess().MainModule.FileName;
    }

    private static string Get7zGPath()
    {
        string dir = Path.GetDirectoryName(GetExePath());
        string sevenZipG = Path.Combine(dir, "7zG.exe");
        return File.Exists(sevenZipG) ? sevenZipG : GetExePath();
    }

    public static bool IsContextMenuRegistered()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\Easy7Zip"))
            {
                if (key != null) return true;
            }
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\Easy7Zip_Open"))
            {
                if (key != null) return true;
            }
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\Easy7Zip_ExtractTo"))
            {
                if (key != null) return true;
            }
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\Easy7Zip_Add7z"))
            {
                if (key != null) return true;
            }
        }
        catch
        {
        }
        return false;
    }

    public static bool RegisterAllFilesContextMenu(AppSettings settings = null)
    {
        if (settings == null)
        {
            settings = AppSettings.Load();
        }

        string exe = GetExePath();
        string sevenZipG = Get7zGPath();
        string iconVal = settings.ContextMenuIcons ? $"\"{exe}\",0" : null;
        string sevenZipGIcon = settings.ContextMenuIcons ? $"\"{sevenZipG}\",0" : null;

        try
        {
            // First remove any stale registrations
            UnregisterAllFilesContextMenu();

            foreach (string target in ShellTargets)
            {
                if (settings.ContextMenuCascaded)
                {
                    string rootPath = $@"Software\Classes\{target}\shell\Easy7Zip";
                    using (RegistryKey root = Registry.CurrentUser.CreateSubKey(rootPath))
                    {
                        root.SetValue("", "Easy 7-Zip Modern");
                        root.SetValue("MUIVerb", "Easy 7-Zip Modern");
                        if (!string.IsNullOrEmpty(iconVal))
                        {
                            root.SetValue("Icon", iconVal);
                        }
                        root.SetValue("SubCommands", "");

                        using (RegistryKey shell = root.CreateSubKey("shell"))
                        {
                            RegisterMenuItems(shell, exe, sevenZipG, iconVal, sevenZipGIcon, settings, target);
                        }
                    }
                }
                else
                {
                    // Non-cascaded: register items directly under target\shell (in main context menu)
                    string basePath = $@"Software\Classes\{target}\shell";
                    using (RegistryKey shell = Registry.CurrentUser.CreateSubKey(basePath))
                    {
                        RegisterUncascadedItems(shell, exe, sevenZipG, iconVal, sevenZipGIcon, settings, target);
                    }
                }
            }

            // Also attempt registration in LocalMachine if running elevated
            TryRegisterLocalMachine(settings, exe, sevenZipG, iconVal, sevenZipGIcon);

            NotifyShell();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void RegisterMenuItems(RegistryKey shell, string exe, string sevenZipG, string exeIcon, string sevenZipGIcon, AppSettings settings, string target)
    {
        if (settings.ContextMenuOpen)
        {
            using (RegistryKey item = shell.CreateSubKey("01_open"))
            {
                item.SetValue("MUIVerb", "Open in Easy 7-Zip Modern");
                if (!string.IsNullOrEmpty(exeIcon)) item.SetValue("Icon", exeIcon);
                using (RegistryKey cmd = item.CreateSubKey("command"))
                {
                    cmd.SetValue("", $"\"{exe}\" \"%1\"");
                }
            }
        }

        if (settings.ContextMenuExtractFiles)
        {
            using (RegistryKey item = shell.CreateSubKey("02_extract"))
            {
                item.SetValue("MUIVerb", "Extract files...");
                if (!string.IsNullOrEmpty(sevenZipGIcon)) item.SetValue("Icon", sevenZipGIcon);
                using (RegistryKey cmd = item.CreateSubKey("command"))
                {
                    cmd.SetValue("", $"\"{sevenZipG}\" x \"%1\"");
                }
            }
        }

        if (settings.ContextMenuExtractHere)
        {
            using (RegistryKey item = shell.CreateSubKey("03_extracthere"))
            {
                item.SetValue("MUIVerb", "Extract Here");
                if (!string.IsNullOrEmpty(exeIcon)) item.SetValue("Icon", exeIcon);
                using (RegistryKey cmd = item.CreateSubKey("command"))
                {
                    cmd.SetValue("", $"\"{exe}\" /extracthere \"%1\"");
                }
            }
        }

        if (settings.ContextMenuExtractTo)
        {
            using (RegistryKey item = shell.CreateSubKey("04_extractto"))
            {
                item.SetValue("MUIVerb", "Extract to dedicated folder (Quick Extract)");
                if (!string.IsNullOrEmpty(exeIcon)) item.SetValue("Icon", exeIcon);
                using (RegistryKey cmd = item.CreateSubKey("command"))
                {
                    cmd.SetValue("", $"\"{exe}\" /extract \"%1\"");
                }
            }
        }

        if (settings.ContextMenuTest)
        {
            using (RegistryKey item = shell.CreateSubKey("05_test"))
            {
                item.SetValue("MUIVerb", "Test archive");
                if (!string.IsNullOrEmpty(sevenZipGIcon)) item.SetValue("Icon", sevenZipGIcon);
                using (RegistryKey cmd = item.CreateSubKey("command"))
                {
                    cmd.SetValue("", $"\"{sevenZipG}\" t \"%1\"");
                }
            }
        }

        if (settings.ContextMenuAdd)
        {
            using (RegistryKey item = shell.CreateSubKey("06_add"))
            {
                item.SetValue("MUIVerb", "Add to archive...");
                if (!string.IsNullOrEmpty(sevenZipGIcon)) item.SetValue("Icon", sevenZipGIcon);
                using (RegistryKey cmd = item.CreateSubKey("command"))
                {
                    cmd.SetValue("", $"\"{sevenZipG}\" a \"%1\"");
                }
            }
        }

        if (settings.ContextMenuAdd7z)
        {
            using (RegistryKey item = shell.CreateSubKey("07_add7z"))
            {
                item.SetValue("MUIVerb", "Add to archive.7z");
                if (!string.IsNullOrEmpty(sevenZipGIcon)) item.SetValue("Icon", sevenZipGIcon);
                using (RegistryKey cmd = item.CreateSubKey("command"))
                {
                    cmd.SetValue("", $"\"{exe}\" /add7z \"%1\"");
                }
            }
        }

        if (settings.ContextMenuAddZip)
        {
            using (RegistryKey item = shell.CreateSubKey("08_addzip"))
            {
                item.SetValue("MUIVerb", "Add to archive.zip");
                if (!string.IsNullOrEmpty(sevenZipGIcon)) item.SetValue("Icon", sevenZipGIcon);
                using (RegistryKey cmd = item.CreateSubKey("command"))
                {
                    cmd.SetValue("", $"\"{exe}\" /addzip \"%1\"");
                }
            }
        }

        if (settings.ContextMenuCrcSha)
        {
            using (RegistryKey crc = shell.CreateSubKey("09_crc"))
            {
                crc.SetValue("MUIVerb", "CRC SHA");
                if (!string.IsNullOrEmpty(sevenZipGIcon)) crc.SetValue("Icon", sevenZipGIcon);
                crc.SetValue("SubCommands", "");

                using (RegistryKey crcShell = crc.CreateSubKey("shell"))
                {
                    CreateCrcSubItem(crcShell, "crc_all", "*", $"\"{sevenZipG}\" h -scrc* \"%1\"");
                    CreateCrcSubItem(crcShell, "crc_sha256", "SHA-256", $"\"{sevenZipG}\" h -scrcSHA256 \"%1\"");
                    CreateCrcSubItem(crcShell, "crc_sha1", "SHA-1", $"\"{sevenZipG}\" h -scrcSHA1 \"%1\"");
                    CreateCrcSubItem(crcShell, "crc_crc32", "CRC-32", $"\"{sevenZipG}\" h -scrcCRC32 \"%1\"");
                    CreateCrcSubItem(crcShell, "crc_crc64", "CRC-64", $"\"{sevenZipG}\" h -scrcCRC64 \"%1\"");
                }
            }
        }

        if (settings.ContextMenuScanFileType)
        {
            using (RegistryKey item = shell.CreateSubKey("10_scan"))
            {
                item.SetValue("MUIVerb", "Scan file type (TrID)");
                if (!string.IsNullOrEmpty(exeIcon)) item.SetValue("Icon", exeIcon);
                using (RegistryKey cmd = item.CreateSubKey("command"))
                {
                    cmd.SetValue("", $"\"{exe}\" /scan \"%1\"");
                }
            }
        }
    }

    private static void CreateCrcSubItem(RegistryKey parentShell, string keyName, string label, string command)
    {
        using (RegistryKey sub = parentShell.CreateSubKey(keyName))
        {
            sub.SetValue("MUIVerb", label);
            using (RegistryKey cmd = sub.CreateSubKey("command"))
            {
                cmd.SetValue("", command);
            }
        }
    }

    private static void RegisterUncascadedItems(RegistryKey shell, string exe, string sevenZipG, string exeIcon, string sevenZipGIcon, AppSettings settings, string target)
    {
        if (settings.ContextMenuOpen)
        {
            using (RegistryKey item = shell.CreateSubKey("Easy7Zip_Open"))
            {
                item.SetValue("MUIVerb", "Easy 7-Zip: Open");
                if (!string.IsNullOrEmpty(exeIcon)) item.SetValue("Icon", exeIcon);
                using (RegistryKey cmd = item.CreateSubKey("command")) { cmd.SetValue("", $"\"{exe}\" \"%1\""); }
            }
        }
        if (settings.ContextMenuExtractFiles)
        {
            using (RegistryKey item = shell.CreateSubKey("Easy7Zip_Extract"))
            {
                item.SetValue("MUIVerb", "Easy 7-Zip: Extract files...");
                if (!string.IsNullOrEmpty(sevenZipGIcon)) item.SetValue("Icon", sevenZipGIcon);
                using (RegistryKey cmd = item.CreateSubKey("command")) { cmd.SetValue("", $"\"{sevenZipG}\" x \"%1\""); }
            }
        }
        if (settings.ContextMenuExtractHere)
        {
            using (RegistryKey item = shell.CreateSubKey("Easy7Zip_ExtractHere"))
            {
                item.SetValue("MUIVerb", "Easy 7-Zip: Extract Here");
                if (!string.IsNullOrEmpty(exeIcon)) item.SetValue("Icon", exeIcon);
                using (RegistryKey cmd = item.CreateSubKey("command")) { cmd.SetValue("", $"\"{exe}\" /extracthere \"%1\""); }
            }
        }
        if (settings.ContextMenuExtractTo)
        {
            using (RegistryKey item = shell.CreateSubKey("Easy7Zip_ExtractTo"))
            {
                item.SetValue("MUIVerb", "Easy 7-Zip: Extract to Folder");
                if (!string.IsNullOrEmpty(exeIcon)) item.SetValue("Icon", exeIcon);
                using (RegistryKey cmd = item.CreateSubKey("command")) { cmd.SetValue("", $"\"{exe}\" /extract \"%1\""); }
            }
        }
        if (settings.ContextMenuTest)
        {
            using (RegistryKey item = shell.CreateSubKey("Easy7Zip_Test"))
            {
                item.SetValue("MUIVerb", "Easy 7-Zip: Test archive");
                if (!string.IsNullOrEmpty(sevenZipGIcon)) item.SetValue("Icon", sevenZipGIcon);
                using (RegistryKey cmd = item.CreateSubKey("command")) { cmd.SetValue("", $"\"{sevenZipG}\" t \"%1\""); }
            }
        }
        if (settings.ContextMenuAdd)
        {
            using (RegistryKey item = shell.CreateSubKey("Easy7Zip_Add"))
            {
                item.SetValue("MUIVerb", "Easy 7-Zip: Add to archive...");
                if (!string.IsNullOrEmpty(sevenZipGIcon)) item.SetValue("Icon", sevenZipGIcon);
                using (RegistryKey cmd = item.CreateSubKey("command")) { cmd.SetValue("", $"\"{sevenZipG}\" a \"%1\""); }
            }
        }
        if (settings.ContextMenuAdd7z)
        {
            using (RegistryKey item = shell.CreateSubKey("Easy7Zip_Add7z"))
            {
                item.SetValue("MUIVerb", "Easy 7-Zip: Add to .7z");
                if (!string.IsNullOrEmpty(sevenZipGIcon)) item.SetValue("Icon", sevenZipGIcon);
                using (RegistryKey cmd = item.CreateSubKey("command")) { cmd.SetValue("", $"\"{exe}\" /add7z \"%1\""); }
            }
        }
        if (settings.ContextMenuAddZip)
        {
            using (RegistryKey item = shell.CreateSubKey("Easy7Zip_AddZip"))
            {
                item.SetValue("MUIVerb", "Easy 7-Zip: Add to .zip");
                if (!string.IsNullOrEmpty(sevenZipGIcon)) item.SetValue("Icon", sevenZipGIcon);
                using (RegistryKey cmd = item.CreateSubKey("command")) { cmd.SetValue("", $"\"{exe}\" /addzip \"%1\""); }
            }
        }
        if (settings.ContextMenuCrcSha)
        {
            using (RegistryKey item = shell.CreateSubKey("Easy7Zip_Sha256"))
            {
                item.SetValue("MUIVerb", "Easy 7-Zip: Check SHA-256");
                if (!string.IsNullOrEmpty(sevenZipGIcon)) item.SetValue("Icon", sevenZipGIcon);
                using (RegistryKey cmd = item.CreateSubKey("command")) { cmd.SetValue("", $"\"{sevenZipG}\" h -scrcSHA256 \"%1\""); }
            }
            using (RegistryKey item = shell.CreateSubKey("Easy7Zip_Crc32"))
            {
                item.SetValue("MUIVerb", "Easy 7-Zip: Check CRC-32");
                if (!string.IsNullOrEmpty(sevenZipGIcon)) item.SetValue("Icon", sevenZipGIcon);
                using (RegistryKey cmd = item.CreateSubKey("command")) { cmd.SetValue("", $"\"{sevenZipG}\" h -scrcCRC32 \"%1\""); }
            }
            using (RegistryKey item = shell.CreateSubKey("Easy7Zip_CrcAll"))
            {
                item.SetValue("MUIVerb", "Easy 7-Zip: Check All Hashes (*)");
                if (!string.IsNullOrEmpty(sevenZipGIcon)) item.SetValue("Icon", sevenZipGIcon);
                using (RegistryKey cmd = item.CreateSubKey("command")) { cmd.SetValue("", $"\"{sevenZipG}\" h -scrc* \"%1\""); }
            }
        }
        if (settings.ContextMenuScanFileType)
        {
            using (RegistryKey item = shell.CreateSubKey("Easy7Zip_Scan"))
            {
                item.SetValue("MUIVerb", "Easy 7-Zip: Scan file type (TrID)");
                if (!string.IsNullOrEmpty(exeIcon)) item.SetValue("Icon", exeIcon);
                using (RegistryKey cmd = item.CreateSubKey("command")) { cmd.SetValue("", $"\"{exe}\" /scan \"%1\""); }
            }
        }
    }

    private static void TryRegisterLocalMachine(AppSettings settings, string exe, string sevenZipG, string iconVal, string sevenZipGIcon)
    {
        try
        {
            foreach (string target in ShellTargets)
            {
                if (settings.ContextMenuCascaded)
                {
                    string rootPath = $@"Software\Classes\{target}\shell\Easy7Zip";
                    using (RegistryKey root = Registry.LocalMachine.CreateSubKey(rootPath))
                    {
                        if (root == null) continue;
                        root.SetValue("", "Easy 7-Zip Modern");
                        root.SetValue("MUIVerb", "Easy 7-Zip Modern");
                        if (!string.IsNullOrEmpty(iconVal)) root.SetValue("Icon", iconVal);
                        root.SetValue("SubCommands", "");

                        using (RegistryKey shell = root.CreateSubKey("shell"))
                        {
                            RegisterMenuItems(shell, exe, sevenZipG, iconVal, sevenZipGIcon, settings, target);
                        }
                    }
                }
                else
                {
                    string basePath = $@"Software\Classes\{target}\shell";
                    using (RegistryKey shell = Registry.LocalMachine.CreateSubKey(basePath))
                    {
                        if (shell != null)
                        {
                            RegisterUncascadedItems(shell, exe, sevenZipG, iconVal, sevenZipGIcon, settings, target);
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore failure if not elevated
        }
    }

    public static bool UnregisterAllFilesContextMenu()
    {
        try
        {
            string[] uncascadedSuffixes = new[]
            {
                "Easy7Zip", "Easy7Zip_Open", "Easy7Zip_Extract", "Easy7Zip_ExtractHere",
                "Easy7Zip_ExtractTo", "Easy7Zip_Test", "Easy7Zip_Add", "Easy7Zip_Add7z",
                "Easy7Zip_AddZip", "Easy7Zip_Sha256", "Easy7Zip_Sha1", "Easy7Zip_Crc32",
                "Easy7Zip_Crc64", "Easy7Zip_CrcAll", "Easy7Zip_Crc", "Easy7Zip_Scan"
            };

            foreach (string target in ShellTargets)
            {
                foreach (string suffix in uncascadedSuffixes)
                {
                    try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{target}\shell\{suffix}", throwOnMissingSubKey: false); } catch { }
                    try { Registry.LocalMachine.DeleteSubKeyTree($@"Software\Classes\{target}\shell\{suffix}", throwOnMissingSubKey: false); } catch { }
                }
            }
            NotifyShell();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool RegisterDoubleclickQuickExtract()
    {
        try
        {
            string exe = GetExePath();
            string progId = "Easy7ZipModern.Archive";
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + progId))
            {
                key.SetValue("", "Archive File (Easy 7-Zip)");
                using (RegistryKey icon = key.CreateSubKey("DefaultIcon"))
                {
                    icon.SetValue("", exe + ",0");
                }
                using (RegistryKey shell = key.CreateSubKey("shell"))
                {
                    shell.SetValue("", "quickextract");
                    using (RegistryKey qe = shell.CreateSubKey("quickextract"))
                    {
                        qe.SetValue("", "Quick Extract Here");
                        using (RegistryKey cmd = qe.CreateSubKey("command"))
                        {
                            cmd.SetValue("", $"\"{exe}\" /extract \"%1\"");
                        }
                    }
                    using (RegistryKey open = shell.CreateSubKey("open"))
                    {
                        open.SetValue("", "Open in Easy 7-Zip Modern");
                        using (RegistryKey cmd = open.CreateSubKey("command"))
                        {
                            cmd.SetValue("", $"\"{exe}\" \"%1\"");
                        }
                    }
                }
            }
            foreach (string ext in ArchiveExtensions)
            {
                using (RegistryKey extKey = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + ext))
                {
                    extKey.SetValue("", progId);
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool RegisterOpenInBrowser()
    {
        try
        {
            string progId = "Easy7ZipModern.Archive";
            using (RegistryKey shell = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + progId + "\\shell"))
            {
                shell.SetValue("", "open");
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool RegisterClassic7ZipShellExtension()
    {
        try
        {
            string dir = Path.GetDirectoryName(GetExePath());
            string dll = Path.Combine(dir, "7-zip.dll");
            if (!File.Exists(dll)) return false;

            var psi = new ProcessStartInfo
            {
                FileName = "regsvr32.exe",
                Arguments = $"/s \"{dll}\"",
                UseShellExecute = true,
                CreateNoWindow = true
            };
            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
            }

            // Configure 7-Zip options in registry
            using (RegistryKey opt = Registry.CurrentUser.CreateSubKey(@"Software\7-Zip\Options"))
            {
                opt.SetValue("CascadedMenu", 1, RegistryValueKind.DWord);
                opt.SetValue("ContextMenu", 0x3FFF, RegistryValueKind.DWord);
                opt.SetValue("Icons", 1, RegistryValueKind.DWord);
            }

            // Register handlers for * and Directory
            string clsid = "{23170F69-40C1-278A-1000-000100020000}";
            foreach (string target in ShellTargets)
            {
                using (RegistryKey cmh = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{target}\shellex\ContextMenuHandlers\7-Zip"))
                {
                    cmh.SetValue("", clsid);
                }
            }
            NotifyShell();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool UnregisterClassic7ZipShellExtension()
    {
        try
        {
            string dir = Path.GetDirectoryName(GetExePath());
            string dll = Path.Combine(dir, "7-zip.dll");
            if (File.Exists(dll))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "regsvr32.exe",
                    Arguments = $"/u /s \"{dll}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit();
                }
            }

            foreach (string target in ShellTargets)
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{target}\shellex\ContextMenuHandlers\7-Zip", false);
                }
                catch { }
            }
            NotifyShell();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Checks whether the classic 7-Zip COM shell extension is registered.</summary>
    public static bool IsClassic7ZipRegistered()
    {
        try
        {
            string clsid = "{23170F69-40C1-278A-1000-000100020000}";
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shellex\ContextMenuHandlers\7-Zip"))
            {
                if (key != null)
                {
                    string val = key.GetValue("") as string;
                    if (string.Equals(val, clsid, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"*\shellex\ContextMenuHandlers\7-Zip"))
            {
                if (key != null)
                {
                    string val = key.GetValue("") as string;
                    if (string.Equals(val, clsid, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        catch { }
        return false;
    }
}

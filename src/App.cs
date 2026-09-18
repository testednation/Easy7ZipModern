using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Easy7ZipModern.Models;
using Easy7ZipModern.Services;

namespace Easy7ZipModern;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string fileArg = null;
        bool extractMode = false;
        bool extractHereMode = false;
        bool add7zMode = false;
        bool addZipMode = false;
        bool registerMode = false;
        bool registerMainMode = false;
        bool registerCascadedMode = false;
        bool unregisterMode = false;
        bool scanMode = false;

        if (e.Args.Length > 0)
        {
            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = e.Args[i];
                if (arg.Equals("/extract", StringComparison.OrdinalIgnoreCase) || arg.Equals("-extract", StringComparison.OrdinalIgnoreCase))
                {
                    extractMode = true;
                }
                else if (arg.Equals("/extracthere", StringComparison.OrdinalIgnoreCase) || arg.Equals("-extracthere", StringComparison.OrdinalIgnoreCase))
                {
                    extractHereMode = true;
                }
                else if (arg.Equals("/add7z", StringComparison.OrdinalIgnoreCase) || arg.Equals("-add7z", StringComparison.OrdinalIgnoreCase))
                {
                    add7zMode = true;
                }
                else if (arg.Equals("/addzip", StringComparison.OrdinalIgnoreCase) || arg.Equals("-addzip", StringComparison.OrdinalIgnoreCase))
                {
                    addZipMode = true;
                }
                else if (arg.Equals("/scan", StringComparison.OrdinalIgnoreCase) || arg.Equals("-scan", StringComparison.OrdinalIgnoreCase))
                {
                    scanMode = true;
                }
                else if (arg.Equals("/registermain", StringComparison.OrdinalIgnoreCase) || arg.Equals("-registermain", StringComparison.OrdinalIgnoreCase) ||
                         arg.Equals("/registernoncascaded", StringComparison.OrdinalIgnoreCase) || arg.Equals("-registernoncascaded", StringComparison.OrdinalIgnoreCase))
                {
                    registerMainMode = true;
                }
                else if (arg.Equals("/registercascaded", StringComparison.OrdinalIgnoreCase) || arg.Equals("-registercascaded", StringComparison.OrdinalIgnoreCase))
                {
                    registerCascadedMode = true;
                }
                else if (arg.Equals("/register", StringComparison.OrdinalIgnoreCase) || arg.Equals("-register", StringComparison.OrdinalIgnoreCase) ||
                         arg.Equals("/registercontextmenu", StringComparison.OrdinalIgnoreCase) || arg.Equals("-registercontextmenu", StringComparison.OrdinalIgnoreCase))
                {
                    registerMode = true;
                }
                else if (arg.Equals("/unregister", StringComparison.OrdinalIgnoreCase) || arg.Equals("-unregister", StringComparison.OrdinalIgnoreCase) ||
                         arg.Equals("/unregistercontextmenu", StringComparison.OrdinalIgnoreCase) || arg.Equals("-unregistercontextmenu", StringComparison.OrdinalIgnoreCase))
                {
                    unregisterMode = true;
                }
                else if (!arg.StartsWith("/") && !arg.StartsWith("-"))
                {
                    fileArg = arg.Trim('"');
                }
            }
        }

        if (registerMainMode)
        {
            AppSettings s = AppSettings.Load();
            s.ContextMenuCascaded = false;
            s.Save();
            ShellAssociationService.RegisterAllFilesContextMenu(s);
            ShellAssociationService.RegisterDoubleclickQuickExtract();
            Shutdown();
            return;
        }

        if (registerCascadedMode)
        {
            AppSettings s = AppSettings.Load();
            s.ContextMenuCascaded = true;
            s.Save();
            ShellAssociationService.RegisterAllFilesContextMenu(s);
            ShellAssociationService.RegisterDoubleclickQuickExtract();
            Shutdown();
            return;
        }

        if (registerMode)
        {
            AppSettings s = AppSettings.Load();
            ShellAssociationService.RegisterAllFilesContextMenu(s);
            ShellAssociationService.RegisterDoubleclickQuickExtract();
            Shutdown();
            return;
        }

        if (unregisterMode)
        {
            ShellAssociationService.UnregisterAllFilesContextMenu();
            Shutdown();
            return;
        }

        if (add7zMode && !string.IsNullOrEmpty(fileArg))
        {
            PerformAddArchive(fileArg, "7z");
            Shutdown();
            return;
        }

        if (addZipMode && !string.IsNullOrEmpty(fileArg))
        {
            PerformAddArchive(fileArg, "zip");
            Shutdown();
            return;
        }

        AppSettings settings = AppSettings.Load();

        // Apply the saved theme app-wide before any window opens.
        ThemeService.Apply(ThemeService.GetByName(settings.ThemeName));

        if (scanMode && !string.IsNullOrEmpty(fileArg) && File.Exists(fileArg))
        {
            var scanWin = new ScanWindow(fileArg);
            scanWin.Show();
        }
        else if (extractHereMode && !string.IsNullOrEmpty(fileArg) && File.Exists(fileArg))
        {
            var quick = new QuickExtractWindow(fileArg, extractHere: true);
            quick.Show();
        }
        else if (!string.IsNullOrEmpty(fileArg) && File.Exists(fileArg) && (extractMode || settings.EnableDoubleClickQuickExtract))
        {
            var quick = new QuickExtractWindow(fileArg, extractHere: false);
            quick.Show();
        }
        else
        {
            var main = new MainWindow(fileArg, extractMode);
            main.Show();
        }
    }

    private static void PerformAddArchive(string target, string format)
    {
        if (string.IsNullOrEmpty(target)) return;
        bool isDir = Directory.Exists(target);
        bool isFile = File.Exists(target);
        if (!isDir && !isFile) return;

        string baseDir = isDir
            ? Path.GetDirectoryName(target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : Path.GetDirectoryName(target);
        if (string.IsNullOrEmpty(baseDir)) baseDir = Directory.GetCurrentDirectory();

        string name = isDir
            ? Path.GetFileName(target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : Path.GetFileNameWithoutExtension(target);
        if (string.IsNullOrEmpty(name)) name = Path.GetFileName(target);

        string ext = format.Equals("zip", StringComparison.OrdinalIgnoreCase) ? ".zip" : ".7z";
        string outArchive = Path.Combine(baseDir, name + ext);

        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        string sevenZipG = Path.Combine(appDir, "7zG.exe");
        string sevenZip = Path.Combine(appDir, "7z.exe");
        string exe = File.Exists(sevenZipG) ? sevenZipG : sevenZip;

        string formatSwitch = format.Equals("zip", StringComparison.OrdinalIgnoreCase) ? "-tzip" : "-t7z";
        string args = $"a {formatSwitch} \"{outArchive}\" \"{target}\"";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = baseDir,
                UseShellExecute = false
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to compress: " + ex.Message, "Easy 7-Zip Modern", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

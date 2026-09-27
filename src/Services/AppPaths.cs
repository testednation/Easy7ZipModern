using System;
using System.IO;

namespace StarExtract.Services;

/// <summary>
/// Central home for per-user data locations. The product is now "Star Extract";
/// existing users' settings/components/vault are migrated once from the old
/// %AppData%\StarExtract folder on first launch.
/// </summary>
public static class AppPaths
{
    public const string LegacyFolderName = "StarExtract";
    public const string FolderName = "StarExtract";

    private static bool _migrated;

    /// <summary>%AppData%\StarExtract — created on demand, migrated from the legacy folder.</summary>
    public static string DataFolder
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                FolderName);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }
    }

    public static string SettingsFile => Path.Combine(DataFolder, "settings.json");
    public static string ComponentsFile => Path.Combine(DataFolder, "components.json");
    public static string VaultFile => Path.Combine(DataFolder, "vault.dat");

    /// <summary>
    /// One-time migration: copies settings.json, components.json and vault.dat from
    /// %AppData%\StarExtract when the new folder does not have them yet. The old
    /// folder is left untouched so uninstalling the legacy app keeps its data.
    /// </summary>
    public static void EnsureMigrated()
    {
        if (_migrated)
        {
            return;
        }
        _migrated = true;

        try
        {
            string legacyDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                LegacyFolderName);
            if (!Directory.Exists(legacyDir))
            {
                return;
            }

            string newDir = DataFolder;
            foreach (string fileName in new[] { "settings.json", "components.json", "vault.dat" })
            {
                string legacyFile = Path.Combine(legacyDir, fileName);
                string newFile = Path.Combine(newDir, fileName);
                if (File.Exists(legacyFile) && !File.Exists(newFile))
                {
                    File.Copy(legacyFile, newFile, overwrite: false);
                }
            }
        }
        catch
        {
            // Migration is best-effort; the app recreates any missing files with defaults.
        }
    }
}

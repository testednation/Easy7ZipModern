using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Easy7ZipModern.Models;
using Easy7ZipModern.Services;

namespace Easy7ZipModern;

public partial class QuickExtractWindow : Window
{
    private readonly string _archivePath;
    private readonly AppSettings _settings;
    private readonly PasswordVaultService _vaultService;
    private readonly ExtractorEngine _engine;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();

    public QuickExtractWindow(string archivePath, bool extractHere = false)
    {
        InitializeComponent();
        _archivePath = archivePath;
        _settings = AppSettings.Load();
        _vaultService = new PasswordVaultService();
        _engine = new ExtractorEngine(_vaultService);

        TxtTitle.Text = "Extracting: " + Path.GetFileName(archivePath);
        string dir = Path.GetDirectoryName(archivePath);
        string name = Path.GetFileNameWithoutExtension(archivePath);
        string targetDir = extractHere ? dir : Path.Combine(dir, name);
        TxtDest.Text = "Destination: " + targetDir;

        Loaded += async (s, e) => await StartExtractionAsync(targetDir);
    }

    private async Task StartExtractionAsync(string targetDir)
    {
        string detectedEngine = _engine.DetectEngine(_archivePath);
        TxtEngine.Text = "Engine: " + detectedEngine;

        ExtractionResult result = await _engine.ExtractAsync(_archivePath, targetDir, (pct, item) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (pct >= 0)
                {
                    PbExtract.IsIndeterminate = false;
                    PbExtract.Value = pct;
                    TxtPercent.Text = pct + "%";
                }
                else
                {
                    PbExtract.IsIndeterminate = true;
                }
                if (!string.IsNullOrEmpty(item))
                {
                    TxtCurrentItem.Text = item;
                }
            });
        }, PromptPassword, _cts.Token);

        if (result.Success)
        {
            TxtPercent.Text = "100%";
            PbExtract.Value = 100;
            TxtCurrentItem.Text = $"Extracted {result.FileCount} items successfully!";
            if (_settings.DeleteSourceAfterExtraction)
            {
                try { File.Delete(_archivePath); } catch { }
            }
            if (_settings.OpenFolderAfterExtraction && Directory.Exists(targetDir))
            {
                try { Process.Start("explorer.exe", targetDir); } catch { }
            }
            await Task.Delay(600);
            Close();
        }
        else
        {
            TxtCurrentItem.Text = "Extraction failed: " + (result.ErrorMessage ?? "Unknown error");
            BtnCancel.Content = "Close";
        }
    }

    private string PromptPassword(string archive, out bool remember)
    {
        string pwd = null;
        bool rem = false;
        Dispatcher.Invoke(() =>
        {
            var prompt = new PasswordPromptWindow(archive) { Owner = this };
            if (prompt.ShowDialog() == true)
            {
                pwd = prompt.Password;
                rem = prompt.RememberPassword;
            }
        });
        remember = rem;
        return pwd;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        Close();
    }
}

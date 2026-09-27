using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using StarExtract.Models;
using StarExtract.Services;

namespace StarExtract
{
    public partial class ScanWindow : Window
    {
        private readonly string _targetFile;
        private readonly AppSettings _settings;
        private readonly FileScanService _scanService;
        private CancellationTokenSource _downloadCts;

        public ScanWindow(string targetFile)
        {
            InitializeComponent();
            _targetFile = targetFile;
            _settings = AppSettings.Load();
            _scanService = new FileScanService();
            TxtTitle.Text = $"File Signature Analysis: {Path.GetFileName(targetFile)}";
            ChkUseMagika.IsChecked = _settings.ScanUseMagika;
            Loaded += ScanWindow_Loaded;
        }

        private async void ScanWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await RunScanAsync();
        }

        private async Task RunScanAsync()
        {
            SetOptionsEnabled(false);
            HideDownloadStrip();

            bool hasTrId = _scanService.IsTrIdAvailable;
            bool wantsMagika = ChkUseMagika.IsChecked == true;

            if (!hasTrId || (wantsMagika && !_scanService.IsMagikaAvailable))
            {
                ShowMissingScannerPrompt(hasTrId, wantsMagika);
                return;
            }

            TxtResults.Text = "Scanning...";
            try
            {
                string result = await Task.Run(() => _scanService.BuildScanReport(_targetFile, wantsMagika));
                TxtResults.Text = result;
            }
            catch (Exception ex)
            {
                TxtResults.Text = "Scan failed: " + ex.Message;
            }
            SetOptionsEnabled(true);
        }

        private void ShowMissingScannerPrompt(bool hasTrId, bool wantsMagika)
        {
            TxtResults.Text = BuildMissingReport(hasTrId, wantsMagika);
            SetOptionsEnabled(true);
            PnlDownload.Visibility = Visibility.Visible;
            BtnScanCancel.Visibility = Visibility.Collapsed;
            BtnScanDownload.Visibility = Visibility.Visible;
            TxtScanStatus.Text = BuildMissingSummary(hasTrId, wantsMagika);
        }

        private static string BuildMissingSummary(bool hasTrId, bool wantsMagika)
        {
            if (!hasTrId)
            {
                return wantsMagika
                    ? "TrID and Magika are not installed — download them now?"
                    : "TrID is not installed — download it now?";
            }
            return "Magika is not installed — download it now for AI-based detection?";
        }

        private string BuildMissingReport(bool hasTrId, bool wantsMagika)
        {
            string report = "";
            if (!hasTrId)
            {
                report += "=== TrID Analysis ===\r\n";
                report += "TrID was not found (expected bin\\trid.exe).\r\n";
                if (!_scanService.IsTrIdDefsAvailable)
                {
                    report += "The signature definitions (triddefs.trd) are missing too.\r\n";
                }
                report += "\r\n";
            }
            if (wantsMagika)
            {
                report += "=== Magika Analysis (Google AI) ===\r\n";
                report += "Magika is not installed (looked for bin\\magika\\magika.exe and on PATH).\r\n";
            }
            report += "\r\nClick '⬇️ Download & Install' to fetch the missing scanner(s) automatically.";
            return report;
        }

        private async void BtnScanDownload_Click(object sender, RoutedEventArgs e)
        {
            bool wantsMagika = ChkUseMagika.IsChecked == true;
            bool needTrId = !_scanService.IsTrIdAvailable || !_scanService.IsTrIdDefsAvailable;
            bool needMagika = wantsMagika && !_scanService.IsMagikaAvailable;

            BtnScanDownload.Visibility = Visibility.Collapsed;
            BtnScanCancel.Visibility = Visibility.Visible;
            SetOptionsEnabled(false);
            _downloadCts = new CancellationTokenSource();

            try
            {
                await _scanService.DownloadMissingAsync(needTrId, needMagika,
                    (pct, msg) => Dispatcher.Invoke(() =>
                    {
                        PbScanDownload.Value = pct;
                        TxtScanStatus.Text = msg;
                    }),
                    _downloadCts.Token);
            }
            catch (OperationCanceledException)
            {
                TxtScanStatus.Text = "Download canceled.";
            }
            catch (Exception ex)
            {
                TxtScanStatus.Text = "Download failed: " + ex.Message;
            }
            finally
            {
                BtnScanCancel.Visibility = Visibility.Collapsed;
                _downloadCts.Dispose();
                _downloadCts = null;
            }

            if (_scanService.IsTrIdAvailable && (!wantsMagika || _scanService.IsMagikaAvailable))
            {
                await RunScanAsync();
            }
            else
            {
                // Something is still missing — put the prompt back up.
                ShowMissingScannerPrompt(_scanService.IsTrIdAvailable, wantsMagika);
            }
        }

        private void BtnScanCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_downloadCts != null)
            {
                try
                {
                    _downloadCts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private async void ChkUseMagika_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }
            _settings.ScanUseMagika = ChkUseMagika.IsChecked == true;
            _settings.Save();
            await RunScanAsync();
        }

        private void SetOptionsEnabled(bool enabled)
        {
            ChkUseMagika.IsEnabled = enabled;
        }

        private void HideDownloadStrip()
        {
            PnlDownload.Visibility = Visibility.Collapsed;
            PbScanDownload.Value = 0;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

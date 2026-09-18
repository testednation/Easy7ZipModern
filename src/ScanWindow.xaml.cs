using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace Easy7ZipModern
{
    public partial class ScanWindow : Window
    {
        private string _targetFile;

        public ScanWindow(string targetFile)
        {
            InitializeComponent();
            _targetFile = targetFile;
            TxtTitle.Text = $"File Signature Analysis: {Path.GetFileName(targetFile)}";
            Loaded += ScanWindow_Loaded;
        }

        private async void ScanWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TxtResults.Text = "Scanning...";
            string result = await Task.Run(() => RunScan());
            TxtResults.Text = result;
        }

        private string RunScan()
        {
            string output = "";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // 1. Run TrID
            string tridExe = Path.Combine(baseDir, "bin", "trid.exe");
            if (File.Exists(tridExe))
            {
                output += "=== TrID Analysis ===\r\n\r\n";
                output += RunCommand(tridExe, $"\"{_targetFile}\"");
            }
            else
            {
                output += "=== TrID Analysis ===\r\nTrID executable not found in bin folder.\r\n";
            }

            // 2. Run Magika (optional, if installed in PATH or bin)
            string magikaExe = Path.Combine(baseDir, "bin", "magika.exe");
            if (!File.Exists(magikaExe))
            {
                // try to see if magika is in PATH
                try
                {
                    output += "\r\n\r\n=== Magika Analysis ===\r\n\r\n";
                    output += RunCommand("magika", $"\"{_targetFile}\"");
                }
                catch
                {
                    output += "Magika is not installed or not in PATH.\r\n";
                }
            }
            else
            {
                output += "\r\n\r\n=== Magika Analysis ===\r\n\r\n";
                output += RunCommand(magikaExe, $"\"{_targetFile}\"");
            }

            return output;
        }

        private string RunCommand(string exePath, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
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
                    return result;
                }
            }
            catch (Exception ex)
            {
                return $"Error running {Path.GetFileName(exePath)}: {ex.Message}";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

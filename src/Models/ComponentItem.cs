using System.ComponentModel;

namespace Easy7ZipModern.Models;

public class ComponentItem : INotifyPropertyChanged
{
    private string _installedVersion;
    private string _latestVersion;
    private string _status;
    private double _downloadProgress;
    private string _downloadSpeed;
    private string _progressText;
    private bool _isDownloading;
    private bool _canUpdate;

    public string Id { get; set; }
    public string Name { get; set; }
    public string Category { get; set; }
    public string Description { get; set; }
    public string DownloadUrl { get; set; }
    public string DestinationFolder { get; set; }
    public string VerificationFile { get; set; }
    public string SupportedFormats { get; set; }

    /// <summary>True when this entry was added by the user (survives restarts via components.json).</summary>
    public bool IsCustom { get; set; }

    /// <summary>True when the user edited the DownloadUrl (persisted so it survives restarts).</summary>
    public bool IsUrlEdited { get; set; }

    public string InstalledVersion
    {
        get => _installedVersion;
        set { _installedVersion = value; OnPropertyChanged(nameof(InstalledVersion)); }
    }

    public string LatestVersion
    {
        get => _latestVersion;
        set { _latestVersion = value; OnPropertyChanged(nameof(LatestVersion)); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); }
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set { _downloadProgress = value; OnPropertyChanged(nameof(DownloadProgress)); }
    }

    public string DownloadSpeed
    {
        get => _downloadSpeed;
        set { _downloadSpeed = value; OnPropertyChanged(nameof(DownloadSpeed)); }
    }

    public string ProgressText
    {
        get => _progressText;
        set { _progressText = value; OnPropertyChanged(nameof(ProgressText)); }
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set { _isDownloading = value; OnPropertyChanged(nameof(IsDownloading)); }
    }

    public bool CanUpdate
    {
        get => _canUpdate;
        set { _canUpdate = value; OnPropertyChanged(nameof(CanUpdate)); }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

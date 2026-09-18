using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Easy7ZipModern.Models;
using Easy7ZipModern.Services;
using Microsoft.Win32;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Button = System.Windows.Controls.Button;
using WinForms = System.Windows.Forms;

namespace Easy7ZipModern;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly PasswordVaultService _vault;
    private readonly ExtractorEngine _engine;
    private readonly UpdateService _updater;
    private ThemeService.Theme _theme;

    private string _currentLoadedArchive;
    private List<ArchiveItem> _archiveEntries = new List<ArchiveItem>();
    private readonly Stack<string> _backStack = new Stack<string>();
    private readonly Stack<string> _fwdStack = new Stack<string>();
    private string _currentFolderPath = "";
    private bool _suppressTreeSelection;
    private FolderTreeNode _selectedTreeNode;

    /// <summary>Folder node for the Archive Browser tree panel; two-way synced
    /// with the file grid (tree click → navigate, grid nav → select + scroll).</summary>
    public sealed class FolderTreeNode
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }
        public ObservableCollection<FolderTreeNode> Children { get; } = new ObservableCollection<FolderTreeNode>();
    }

    /// <summary>One clickable segment of the breadcrumb path bar.</summary>
    public sealed class FolderCrumb
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsLast { get; set; }
        public Visibility SeparatorVisibility => IsLast ? Visibility.Collapsed : Visibility.Visible;
    }
    private ObservableCollection<ComponentItem> _componentsList;

    public MainWindow(string initialFile = null, bool quickExtractMode = false)
    {
        InitializeComponent();
        TryLoadWindowIcon();

        _settings = AppSettings.Load();
        _vault = new PasswordVaultService();
        _engine = new ExtractorEngine(_vault);
        _updater = new UpdateService();

        ApplyTheme(ThemeService.GetByName(_settings.ThemeName));
        ApplyUiScale(_settings.UiScale);
        InitComponentsTab();
        InitSettingsTab();

        Loaded += (s, e) =>
        {
            if (!string.IsNullOrEmpty(initialFile) && File.Exists(initialFile))
            {
                if (quickExtractMode || _settings.EnableDoubleClickQuickExtract)
                {
                    var quick = new QuickExtractWindow(initialFile) { Owner = this };
                    quick.ShowDialog();
                }
                else
                {
                    LoadArchive(initialFile);
                }
            }
        };
    }

    // ------------------------------------------------------------------
    //  UI zoom (Ctrl +/-/0, Ctrl+wheel, title-bar buttons) — scales all text
    //  and controls together via a LayoutTransform on the window content.
    // ------------------------------------------------------------------

    private const double MinUiScale = 0.5;
    private const double MaxUiScale = 2.0;
    private static readonly double[] ZoomSteps = { 0.5, 0.67, 0.75, 0.8, 0.9, 1.0, 1.1, 1.25, 1.5, 1.75, 2.0 };

    private void ApplyUiScale(double scale)
    {
        double clamped = Math.Max(MinUiScale, Math.Min(MaxUiScale, scale));
        if (double.IsNaN(clamped) || double.IsInfinity(clamped) || clamped <= 0)
        {
            clamped = 1.0;
        }
        UiScaleTransform.ScaleX = clamped;
        UiScaleTransform.ScaleY = clamped;
        TxtZoomLevel.Text = (int)Math.Round(clamped * 100) + "%";
        _settings.UiScale = clamped;
    }

    private void ZoomIn()
    {
        double cur = UiScaleTransform.ScaleX;
        double next = ZoomSteps.FirstOrDefault(s => s > cur + 0.001);
        ApplyUiScale(next == 0 ? MaxUiScale : next);
        _settings.Save();
    }

    private void ZoomOut()
    {
        double cur = UiScaleTransform.ScaleX;
        double next = 1.0;
        for (int i = ZoomSteps.Length - 1; i >= 0; i--)
        {
            if (ZoomSteps[i] < cur - 0.0001)
            {
                next = ZoomSteps[i];
                break;
            }
        }
        ApplyUiScale(next);
        _settings.Save();
    }

    private void ZoomReset()
    {
        ApplyUiScale(1.0);
        _settings.Save();
    }

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
    {
        ZoomIn();
    }

    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
    {
        ZoomOut();
    }
    private void BtnZoomReset_Click(object sender, RoutedEventArgs e)
    {
        ZoomReset();
    }

    private void ToggleShortcutsOverlay()
    {
        ShortcutsOverlay.Visibility = ShortcutsOverlay.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ShortcutsOverlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Clicking the dim backdrop closes the overlay.
        if (e.OriginalSource is DependencyObject src && FindAncestor<Border>(src) == null)
        {
            ToggleShortcutsOverlay();
        }
    }

    private static T FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        DependencyObject cur = start;
        while (cur != null && cur is not T)
        {
            cur = System.Windows.Media.VisualTreeHelper.GetParent(cur);
        }
        return cur as T;
    }

    private void BtnShortcutsClose_Click(object sender, RoutedEventArgs e)
    {
        ShortcutsOverlay.Visibility = Visibility.Collapsed;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        // Help overlay toggles work regardless of focus (F1 anywhere;
        // Shift+/ only outside text input so typing isn't swallowed).
        bool inTextBox = Keyboard.FocusedElement is TextBox;
        if (e.Key == Key.F1 || (e.Key == Key.OemQuestion && !inTextBox &&
            (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift))
        {
            ToggleShortcutsOverlay();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && ShortcutsOverlay.Visibility == Visibility.Visible)
        {
            ShortcutsOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
            return;
        }

        // Alt+Left / Alt+Right: history navigation in the archive browser.
        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            if (e.Key == Key.Left)
            {
                BtnBack_Click(null, null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Right)
            {
                BtnForward_Click(null, null);
                e.Handled = true;
                return;
            }
        }

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        if (!ctrl)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.OemPlus:
            case Key.Add:
                ZoomIn();
                e.Handled = true;
                break;
            case Key.OemMinus:
            case Key.Subtract:
                ZoomOut();
                e.Handled = true;
                break;
            case Key.D0:
            case Key.NumPad0:
                ZoomReset();
                e.Handled = true;
                break;
        }
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (e.Delta > 0)
            {
                ZoomIn();
            }
            else if (e.Delta < 0)
            {
                ZoomOut();
            }
            e.Handled = true;
        }
    }

    private void TryLoadWindowIcon()
    {
        try
        {
            string exePath = Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(exePath))
            {
                using (var ico = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
                {
                    if (ico != null)
                    {
                        Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                            ico.Handle,
                            new Int32Rect(0, 0, ico.Width, ico.Height),
                            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                    }
                }
            }
        }
        catch
        {
            // cosmetic only
        }
    }

    // ------------------------------------------------------------------
    //  Theme
    // ------------------------------------------------------------------

    private void ApplyTheme(ThemeService.Theme theme)
    {
        _theme = theme;
        ThemeService.Apply(theme);
        BtnToggleTheme.Content = theme.Icon + " " + theme.Name;
    }

    private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        ThemeService.Theme next = ThemeService.GetNext(_theme);
        ApplyTheme(next);
        _settings.IsDarkMode = next.Name != "Light";
        _settings.ThemeName = next.Name;
        _settings.Save();
    }

    // ------------------------------------------------------------------
    //  Navigation
    // ------------------------------------------------------------------

    private void NavTab_Checked(object sender, RoutedEventArgs e)
    {
        if (PageBrowser == null)
        {
            return;
        }
        PageBrowser.Visibility = TabNavBrowser.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageExtractor.Visibility = TabNavExtractor.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageComponents.Visibility = TabNavComponents.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageSettings.Visibility = TabNavSettings.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    // ------------------------------------------------------------------
    //  Archive browser
    // ------------------------------------------------------------------

    public void LoadArchive(string archivePath)
    {
        if (!File.Exists(archivePath))
        {
            return;
        }
        _currentLoadedArchive = archivePath;
        TabNavBrowser.IsChecked = true;
        NavTab_Checked(null, null);
        try
        {
            _archiveEntries = _engine.ListArchive(archivePath);
            if (_archiveEntries.Count == 0 && _settings.AutoTrySavedPasswords)
            {
                foreach (string pwd in _vault.Passwords)
                {
                    List<ArchiveItem> tryList = _engine.ListArchive(archivePath, pwd);
                    if (tryList.Count > 0)
                    {
                        _archiveEntries = tryList;
                        break;
                    }
                }
            }
            _backStack.Clear();
            _fwdStack.Clear();
            NavigateRoot();
            BuildFolderTree();
            RefreshArchiveStatus();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Error opening archive: " + ex.Message, "Easy 7-Zip Modern",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ------------------------------------------------------------------
    //  Folder navigation (Up / Back / Forward, breadcrumbs, double-click)
    //  Listing logic lives in ExtractorEngine.ListChildren / ParentFolderPath.
    // ------------------------------------------------------------------

    private void BuildFolderView()
    {
        if (string.IsNullOrEmpty(_currentLoadedArchive))
        {
            DgArchiveItems.ItemsSource = null;
            BrCrumb.ItemsSource = null;
            TvFolders.ItemsSource = null;
            _selectedTreeNode = null;
            BtnUpFolder.IsEnabled = false;
            BtnBack.IsEnabled = false;
            BtnForward.IsEnabled = false;
            return;
        }

        string filter = (TxtSearch.Text ?? "").Trim();
        IEnumerable<ArchiveItem> view = ExtractorEngine.ListChildren(_archiveEntries, _currentFolderPath);
        if (filter.Length > 0)
        {
            string f = filter.ToLowerInvariant();
            view = view.Where(i => (i.Name ?? "").ToLowerInvariant().Contains(f));
        }

        var rows = new List<ArchiveItem>();
        if (_currentFolderPath.Length > 0)
        {
            rows.Add(new ArchiveItem { Name = "..", Path = ExtractorEngine.ParentFolderPath(_currentFolderPath), IsDirectory = true });
        }
        rows.AddRange(view);
        DgArchiveItems.ItemsSource = rows;

        BtnUpFolder.IsEnabled = _currentFolderPath.Length > 0;
        BtnBack.IsEnabled = _backStack.Count > 0;
        BtnForward.IsEnabled = _fwdStack.Count > 0;
        BuildBreadcrumbs();
        SyncTreeToSelection();
    }

    private void NavigateTo(string folderPath, bool pushHistory = true)
    {
        if (!ArchiveLoaded())
        {
            return;
        }
        folderPath = (folderPath ?? "").TrimEnd('\\');
        if (pushHistory && folderPath != _currentFolderPath)
        {
            _backStack.Push(_currentFolderPath);
            _fwdStack.Clear();
        }
        _currentFolderPath = folderPath;
        TxtSearch.Clear();
        BuildFolderView();
    }

    private void NavigateRoot()
    {
        _currentFolderPath = "";
        TxtSearch.Clear();
        BuildFolderView();
    }

    private void NavigateUp()
    {
        if (_currentFolderPath.Length > 0)
        {
            NavigateTo(ExtractorEngine.ParentFolderPath(_currentFolderPath));
        }
    }

    private void BuildBreadcrumbs()
    {
        var crumbs = new List<FolderCrumb>
        {
            new FolderCrumb
            {
                Name = Path.GetFileName(_currentLoadedArchive),
                FullPath = "",
                IsLast = _currentFolderPath.Length == 0
            }
        };
        string acc = "";
        foreach (string part in _currentFolderPath.Split('\\'))
        {
            if (part.Length == 0)
            {
                continue;
            }
            acc = acc.Length == 0 ? part : acc + "\\" + part;
            crumbs.Add(new FolderCrumb { Name = part, FullPath = acc, IsLast = acc == _currentFolderPath });
        }
        BrCrumb.ItemsSource = crumbs;
    }

    // ------------------------------------------------------------------
    //  Folder tree panel (synced with the file grid)
    // ------------------------------------------------------------------

    /// <summary>Rebuilds the folder tree from the archive listing. One virtual
    /// root per archive (named after it), children = every folder path.</summary>
    private void BuildFolderTree()
    {
        var root = new FolderTreeNode
        {
            Name = string.IsNullOrEmpty(_currentLoadedArchive) ? "(no archive)" : Path.GetFileName(_currentLoadedArchive),
            FullPath = "",
            IsExpanded = true
        };
        var dirNodes = new Dictionary<string, FolderTreeNode>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = root
        };

        foreach (ArchiveItem entry in _archiveEntries)
        {
            string p = (entry.Path ?? "").Replace('/', '\\').TrimEnd('\\');
            if (p.Length == 0)
            {
                continue;
            }
            string acc = "";
            foreach (string part in p.Split('\\'))
            {
                if (part.Length == 0)
                {
                    continue;
                }
                string parentAcc = acc;
                acc = acc.Length == 0 ? part : acc + "\\" + part;
                bool isLast = acc == p;
                if (!entry.IsDirectory && isLast)
                {
                    break; // files get no tree node
                }
                if (!dirNodes.TryGetValue(acc, out FolderTreeNode node))
                {
                    node = new FolderTreeNode { Name = part, FullPath = acc };
                    dirNodes[parentAcc].Children.Add(node);
                    dirNodes[acc] = node;
                }
            }
        }

        SortTreeNodes(root.Children);
        _selectedTreeNode = root;
        _suppressTreeSelection = true;
        try
        {
            TvFolders.ItemsSource = new List<FolderTreeNode> { root };
        }
        finally
        {
            _suppressTreeSelection = false;
        }
    }

    private static void SortTreeNodes(ObservableCollection<FolderTreeNode> nodes)
    {
        if (nodes.Count < 2)
        {
            return;
        }
        var sorted = nodes.OrderBy(n => n.Name ?? "", StringComparer.OrdinalIgnoreCase).ToList();
        nodes.Clear();
        foreach (FolderTreeNode n in sorted)
        {
            nodes.Add(n);
            SortTreeNodes(n.Children);
        }
    }

    /// <summary>Grid navigation → expand + select + scroll the matching tree node.</summary>
    private void SyncTreeToSelection()
    {
        if (TvFolders.ItemsSource == null || TreePanel.Visibility != Visibility.Visible)
        {
            return;
        }
        if (!(TvFolders.Items.Count > 0 && TvFolders.Items[0] is FolderTreeNode root))
        {
            return;
        }

        _suppressTreeSelection = true;
        FolderTreeNode target = root;
        try
        {
            root.IsExpanded = true;
            if (_currentFolderPath.Length > 0)
            {
                string acc = "";
                foreach (string part in _currentFolderPath.Split('\\'))
                {
                    if (part.Length == 0)
                    {
                        continue;
                    }
                    acc = acc.Length == 0 ? part : acc + "\\" + part;
                    FolderTreeNode next = target.Children.FirstOrDefault(
                        c => string.Equals(c.Name, part, StringComparison.OrdinalIgnoreCase));
                    if (next == null)
                    {
                        break;
                    }
                    next.IsExpanded = true;
                    target = next;
                }
            }
            if (_selectedTreeNode != null && _selectedTreeNode != target)
            {
                _selectedTreeNode.IsSelected = false;
            }
            target.IsSelected = true;
            _selectedTreeNode = target;
        }
        finally
        {
            _suppressTreeSelection = false;
        }

        // Containers for newly expanded levels generate after layout — defer.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            TreeViewItem tvi = FindTreeContainer(TvFolders.ItemContainerGenerator, TvFolders.Items, target);
            tvi?.BringIntoView();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static TreeViewItem FindTreeContainer(ItemContainerGenerator gen, ItemCollection items, FolderTreeNode target)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (!(items[i] is FolderTreeNode node))
            {
                continue;
            }
            var container = gen.ContainerFromIndex(i) as TreeViewItem;
            if (node == target)
            {
                return container;
            }
            if (container != null && container.IsExpanded && node.Children.Count > 0)
            {
                TreeViewItem inner = FindTreeContainer(container.ItemContainerGenerator, container.Items, target);
                if (inner != null)
                {
                    return inner;
                }
            }
        }
        return null;
    }

    private void TvFolders_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_suppressTreeSelection)
        {
            return;
        }
        if (e.NewValue is FolderTreeNode node)
        {
            _selectedTreeNode = node;
            NavigateTo(node.FullPath);
        }
    }

    private void BtnToggleTree_Click(object sender, RoutedEventArgs e)
    {
        bool show = TreePanel.Visibility != Visibility.Visible;
        TreePanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        TreeSplitter.Visibility = TreePanel.Visibility;
        BtnToggleTree.Content = show ? "🗂 Tree ✓" : "🗂 Tree";
        if (show)
        {
            SyncTreeToSelection();
        }
    }

    private void UpdateItemCountStatus()
    {
        TxtStatusItems.Text = $"{_archiveEntries.Count(i => !i.IsDirectory)} files · {_archiveEntries.Count(i => i.IsDirectory)} folders";
    }

    private void RefreshArchiveStatus()
    {
        long total = _archiveEntries.Sum(i => i.Size);
        UpdateItemCountStatus();
        TxtStatusTotalSize.Text = "Size: " + ArchiveItem.FormatSize(total);
        TxtStatusArchiveType.Text = "Engine: " + _engine.DetectEngine(_currentLoadedArchive);
        bool enc = _archiveEntries.Any(i => i.IsEncrypted);
        TxtStatusEncryption.Text = enc ? "🔒 Encrypted" : "🔓 Decrypted";
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        BuildFolderView();
    }

    private void BtnCrumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
        {
            NavigateTo(path);
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_backStack.Count > 0)
        {
            _fwdStack.Push(_currentFolderPath);
            NavigateTo(_backStack.Pop(), pushHistory: false);
        }
    }

    private void BtnForward_Click(object sender, RoutedEventArgs e)
    {
        if (_fwdStack.Count > 0)
        {
            _backStack.Push(_currentFolderPath);
            NavigateTo(_fwdStack.Pop(), pushHistory: false);
        }
    }

    private void DgArchiveItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int sel = DgArchiveItems.SelectedItems?.Count ?? 0;
        UpdateItemCountStatus();
        if (sel > 0)
        {
            TxtStatusItems.Text += $" · {sel} selected";
        }
    }

    private void DgArchiveItems_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Back)
        {
            NavigateUp();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            OpenSelectedItem();
            e.Handled = true;
        }
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open Archive File",
            Filter = "All Supported Archives & ZIP Containers (*.7z, *.zip, *.rar, *.apk, *.docx, *.xlsx, *.epub, *.cbz...)|*.7z;*.zip;*.zipx;*.rar;*.tar;*.gz;*.tgz;*.bz2;*.tbz2;*.xz;*.txz;*.zst;*.iso;*.cab;*.wim;*.jar;*.war;*.aar;*.apk;*.xapk;*.aab;*.docx;*.xlsx;*.pptx;*.odt;*.ods;*.odp;*.epub;*.cbz;*.cbr;*.xpi;*.crx;*.vsix;*.nupkg;*.whl;*.msix;*.appx;*.pak;*.pea;*.exe;*.msi|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            LoadArchive(dlg.FileName);
        }
    }

    private void BtnExtractTo_Click(object sender, RoutedEventArgs e)
    {
        if (!ArchiveLoaded()) return;
        TxtSourceArchive.Text = _currentLoadedArchive;
        string dir = Path.GetDirectoryName(_currentLoadedArchive);
        string name = Path.GetFileNameWithoutExtension(_currentLoadedArchive);
        TxtDestDir.Text = Path.Combine(dir, name);
        TxtSmartEngine.Text = _engine.DetectEngine(_currentLoadedArchive);
        TabNavExtractor.IsChecked = true;
        NavTab_Checked(null, null);
    }

    private void BtnQuickExtractHere_Click(object sender, RoutedEventArgs e)
    {
        if (!ArchiveLoaded()) return;
        var quick = new QuickExtractWindow(_currentLoadedArchive) { Owner = this };
        quick.ShowDialog();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select files to compress into new archive",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true || dlg.FileNames.Length <= 0)
        {
            return;
        }
        var save = new SaveFileDialog
        {
            Title = "Save New Archive As",
            Filter = "7-Zip Archive (*.7z)|*.7z|ZIP Archive (*.zip)|*.zip",
            FileName = "Archive.7z"
        };
        if (save.ShowDialog() == true)
        {
            string sevenZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe");
            string files = string.Join("\" \"", dlg.FileNames);
            var psi = new ProcessStartInfo
            {
                FileName = sevenZip,
                Arguments = $"a \"{save.FileName}\" \"{files}\" -mx=9",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process.Start(psi)) { }
            LoadArchive(save.FileName);
        }
    }

    private void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        if (!ArchiveLoaded()) return;
        string sevenZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe");
        var psi = new ProcessStartInfo
        {
            FileName = sevenZip,
            Arguments = $"t \"{_currentLoadedArchive}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using (var p = Process.Start(psi))
        {
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode == 0)
            {
                System.Windows.MessageBox.Show("Integrity test passed! There are no errors in archive.",
                    "Integrity Test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("Integrity test reported errors (or a password is required):\n" + output,
                    "Test Results", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (DgArchiveItems.SelectedItem is ArchiveItem item && item.Name != ".." && ArchiveLoaded()
            && System.Windows.MessageBox.Show(
                $"Are you sure you want to delete '{item.Name}' from the archive?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            string sevenZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe");
            var psi = new ProcessStartInfo
            {
                FileName = sevenZip,
                Arguments = $"d \"{_currentLoadedArchive}\" \"{item.Path}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process.Start(psi)) { }
            ReloadArchiveKeepingFolder();
        }
    }

    private void BtnUpFolder_Click(object sender, RoutedEventArgs e)
    {
        NavigateUp();
    }

    private bool ArchiveLoaded()
    {
        if (string.IsNullOrEmpty(_currentLoadedArchive))
        {
            System.Windows.MessageBox.Show("Open an archive first — click 'Open' or drag & drop one anywhere in this window.",
                "Easy 7-Zip Modern", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        return true;
    }

    private void DgArchiveItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OpenSelectedItem();
    }

    private void OpenSelectedItem()
    {
        if (!(DgArchiveItems.SelectedItem is ArchiveItem item) || !ArchiveLoaded())
        {
            return;
        }
        if (item.IsDirectory)
        {
            NavigateTo(item.Path ?? "");
            return;
        }
        try
        {
            string tmp = Path.Combine(Path.GetTempPath(), "Easy7Zip_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tmp);
            string sevenZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe");
            var psi = new ProcessStartInfo
            {
                FileName = sevenZip,
                Arguments = $"e -y -o\"{tmp}\" \"{_currentLoadedArchive}\" \"{item.Path}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process.Start(psi)) { }
            string outPath = Path.Combine(tmp, item.Name);
            if (File.Exists(outPath))
            {
                Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Could not open item: " + ex.Message, "Open File",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ------------------------------------------------------------------
    //  Drag & drop (whole window; browser page shows a drop overlay)
    // ------------------------------------------------------------------

    private static string[] GetDroppedFiles(System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return null;
        }
        return e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
    }

    private void PageBrowser_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = GetDroppedFiles(e) != null ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void PageBrowser_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (GetDroppedFiles(e) != null)
        {
            BrowserDropOverlay.Visibility = Visibility.Visible;
        }
    }

    private void PageBrowser_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        BrowserDropOverlay.Visibility = Visibility.Collapsed;
    }

    private void PageBrowser_Drop(object sender, System.Windows.DragEventArgs e)
    {
        BrowserDropOverlay.Visibility = Visibility.Collapsed;
        HandleArchiveDrop(e);
        e.Handled = true;
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = GetDroppedFiles(e) != null ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (GetDroppedFiles(e) == null)
        {
            return;
        }
        // Browser page has its own overlay; on other tabs flash the status bar hint.
        if (TabNavBrowser.IsChecked != true)
        {
            TxtStatusItems.Text = "⬇ Drop to open the archive…";
        }
    }

    private void Window_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (TabNavBrowser.IsChecked != true)
        {
            UpdateItemCountStatus();
        }
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        HandleArchiveDrop(e);
        e.Handled = true;
    }

    /// <summary>
    /// Plain archives open in the browser; everything else (installers, MSI, …)
    /// routes to the Smart Extractor so the UniExtract engines can handle it.
    /// </summary>
    private void HandleArchiveDrop(System.Windows.DragEventArgs e)
    {
        string[] files = GetDroppedFiles(e);
        if (files == null || files.Length == 0)
        {
            return;
        }
        string path = files[0];

        if (Directory.Exists(path))
        {
            string[] inner = Directory.GetFiles(path);
            if (inner.Length == 0)
            {
                return;
            }
            path = inner[0];
        }

        if (IsPlainArchive(path))
        {
            LoadArchive(path);
        }
        else
        {
            SetExtractorSource(path);
            TabNavExtractor.IsChecked = true;
            NavTab_Checked(null, null);
        }
    }

    private static readonly HashSet<string> PlainArchiveExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Standard & compressed archives
        ".zip", ".zipx", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".tbz2", ".bzip2",
        ".xz", ".txz", ".zst", ".tzst", ".zstd", ".lzma", ".tlz", ".lz", ".lz4", ".lzh", ".lha",
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

    private static bool IsPlainArchive(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string ext = Path.GetExtension(path);

        // Explicit installer formats route to Smart Extractor
        if (string.Equals(ext, ".msi", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ext, ".msp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Executables route to Smart Extractor
        if (string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Known archives and all ZIP variants
        if (PlainArchiveExtensions.Contains(ext))
        {
            return true;
        }

        // Treat any unknown format as a ZIP container / archive
        return true;
    }

    // ------------------------------------------------------------------
    //  Smart extractor
    // ------------------------------------------------------------------

    private void DropZone_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = GetDroppedFiles(e) != null ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, System.Windows.DragEventArgs e)
    {
        string[] files = GetDroppedFiles(e);
        if (files != null && files.Length > 0)
        {
            SetExtractorSource(files[0]);
        }
        e.Handled = true;
    }

    private void BtnBrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Archive or Installer to Extract",
            Filter = "All Supported Archives & Installers|*.7z;*.zip;*.zipx;*.rar;*.tar;*.gz;*.zst;*.exe;*.msi;*.msp;*.cab;*.iso;*.jar;*.apk;*.docx;*.xlsx;*.pptx;*.epub;*.cbz;*.pak;*.pea|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            SetExtractorSource(dlg.FileName);
        }
    }

    private void BtnBrowseDest_Click(object sender, RoutedEventArgs e)
    {
        using (var dlg = new WinForms.FolderBrowserDialog { Description = "Select Extraction Destination Folder" })
        {
            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            {
                TxtDestDir.Text = dlg.SelectedPath;
            }
        }
    }

    private void SetExtractorSource(string path)
    {
        TxtSourceArchive.Text = path;
        string dir = Path.GetDirectoryName(path);
        string name = Path.GetFileNameWithoutExtension(path);
        TxtDestDir.Text = Path.Combine(dir ?? "", name);
        TxtSmartEngine.Text = _engine.DetectEngine(path);
    }

    private async void BtnStartSmartExtract_Click(object sender, RoutedEventArgs e)
    {
        string source = TxtSourceArchive.Text.Trim();
        if (string.IsNullOrEmpty(source) || !File.Exists(source))
        {
            System.Windows.MessageBox.Show("Please select a valid archive or installer file.",
                "Smart Extractor", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return;
        }

        string target = TxtDestDir.Text.Trim();
        if (string.IsNullOrEmpty(target))
        {
            target = Path.Combine(Path.GetDirectoryName(source) ?? "", Path.GetFileNameWithoutExtension(source));
        }
        if (ChkSmartSubfolder.IsChecked == true)
        {
            target = Path.Combine(target, Path.GetFileNameWithoutExtension(source));
        }

        BtnStartSmartExtract.IsEnabled = false;
        SmartProgressPanel.Visibility = Visibility.Visible;
        SmartProgressBar.IsIndeterminate = true;
        TxtSmartPercent.Text = "0%";
        TxtExtractorLog.Clear();
        TxtExtractorLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Starting extraction: {Path.GetFileName(source)}\r\n");
        TxtExtractorLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Engine: {_engine.DetectEngine(source)}\r\n");
        TxtExtractorLog.ScrollToEnd();

        var cts = new System.Threading.CancellationTokenSource();
        ExtractionResult res = await _engine.ExtractAsync(source, target, (pct, item) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (pct >= 0)
                {
                    SmartProgressBar.IsIndeterminate = false;
                    SmartProgressBar.Value = pct;
                    TxtSmartPercent.Text = pct + "%";
                }
                if (!string.IsNullOrEmpty(item))
                {
                    TxtSmartCurrentFile.Text = item;
                    TxtExtractorLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {item}\r\n");
                    TxtExtractorLog.ScrollToEnd();
                }
            });
        }, PromptPassword, cts.Token);

        BtnStartSmartExtract.IsEnabled = true;
        SmartProgressBar.IsIndeterminate = false;
        SmartProgressBar.Value = 100;
        TxtSmartPercent.Text = "100%";
        if (res.Success)
        {
            TxtSmartCurrentFile.Text = $"Extracted {res.FileCount} items in {res.Duration.TotalSeconds:F1}s!";
            TxtExtractorLog.AppendText($"[{DateTime.Now:HH:mm:ss}] SUCCESS: Extracted to {target}\r\n");
            TxtExtractorLog.ScrollToEnd();
            if (ChkDeleteSourceAfter.IsChecked == true)
            {
                try { File.Delete(source); } catch { }
            }
            if (ChkOpenFolderAfter.IsChecked == true && Directory.Exists(target))
            {
                try { Process.Start("explorer.exe", target); } catch { }
            }
        }
        else
        {
            TxtSmartCurrentFile.Text = "Extraction failed: " + (res.ErrorMessage ?? "Unknown error");
            TxtExtractorLog.AppendText($"[{DateTime.Now:HH:mm:ss}] FAILED: {res.ErrorMessage}\r\n");
            TxtExtractorLog.ScrollToEnd();
        }
    }

    /// <summary>Re-lists the archive after a delete but stays in the current folder.</summary>
    private void ReloadArchiveKeepingFolder()
    {
        try
        {
            _archiveEntries = _engine.ListArchive(_currentLoadedArchive);
            BuildFolderView();
            RefreshArchiveStatus();
        }
        catch
        {
            NavigateRoot();
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

    // ------------------------------------------------------------------
    //  Components & plugins tab
    // ------------------------------------------------------------------

    private void InitComponentsTab()
    {
        _componentsList = _updater.GetDefaultComponents();
        IcComponents.ItemsSource = _componentsList;
        IcPluginUrls.ItemsSource = _componentsList;
    }

    private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        BtnCheckUpdates.IsEnabled = false;
        foreach (ComponentItem comp in _componentsList)
        {
            comp.Status = "Checking...";
            await System.Threading.Tasks.Task.Delay(50);
            if (File.Exists(comp.VerificationFile))
            {
                comp.Status = "Installed (Ready)";
                comp.CanUpdate = comp.InstalledVersion != comp.LatestVersion;
                if (comp.CanUpdate)
                {
                    comp.Status = "Update Available";
                }
            }
            else
            {
                comp.Status = "Not Installed";
                comp.CanUpdate = true;
            }
        }
        BtnCheckUpdates.IsEnabled = true;
    }

    private async void BtnUpdateAll_Click(object sender, RoutedEventArgs e)
    {
        BtnUpdateAll.IsEnabled = false;
        foreach (ComponentItem comp in _componentsList)
        {
            if (comp.CanUpdate)
            {
                await _updater.DownloadComponentAsync(comp);
            }
        }
        _updater.SaveComponents(_componentsList);
        BtnUpdateAll.IsEnabled = true;
    }

    private async void BtnDownloadComponent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ComponentItem comp } btn)
        {
            btn.IsEnabled = false;
            await _updater.DownloadComponentAsync(comp);
            _updater.SaveComponents(_componentsList);
            btn.IsEnabled = comp.CanUpdate;
        }
    }

    private void BtnSaveUrls_Click(object sender, RoutedEventArgs e)
    {
        foreach (ComponentItem comp in _componentsList)
        {
            comp.IsUrlEdited = true;
        }
        _updater.SaveComponents(_componentsList);
        System.Windows.MessageBox.Show("Plugin source URLs saved. New downloads will use these addresses.",
            "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnResetUrls_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show("Reset all plugin URLs to defaults and remove custom plugins?",
            "Reset", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _updater.ResetComponents();
            InitComponentsTab();
        }
    }

    private void BtnAddPlugin_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtNewPluginName.Text.Trim();
        string url = TxtNewPluginUrl.Text.Trim();
        string verify = TxtNewPluginVerify.Text.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
        {
            System.Windows.MessageBox.Show("Plugin name and download URL are required.",
                "Add Plugin", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            System.Windows.MessageBox.Show("The download URL must be a valid http(s) address.",
                "Add Plugin", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return;
        }

        // Default destination: bin\<name>\ under the app folder.
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string verifyPath = string.IsNullOrEmpty(verify)
            ? null
            : Path.IsPathRooted(verify)
                ? verify
                : (verify.Contains("\\") || verify.Contains("/")
                    ? Path.Combine(baseDir, verify.Replace('/', '\\'))
                    : Path.Combine(baseDir, "bin", name, verify));

        var plugin = new ComponentItem
        {
            Id = "custom-" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Name = name,
            Category = "Custom Plugin",
            Description = "User-added plugin. Files extract into " + (verify == null ? $"bin\\{name}\\" : Path.GetDirectoryName(verifyPath) ?? $"bin\\{name}\\") + ".",
            DownloadUrl = url,
            DestinationFolder = string.IsNullOrEmpty(verify) ? Path.Combine(baseDir, "bin", name) : Path.GetDirectoryName(verifyPath),
            VerificationFile = verifyPath ?? Path.Combine(baseDir, "bin", name, name + ".exe"),
            SupportedFormats = "Custom",
            InstalledVersion = File.Exists(verifyPath ?? Path.Combine(baseDir, "bin", name)) ? "Installed" : "Missing",
            LatestVersion = "1.0",
            Status = "Not Installed",
            CanUpdate = true,
            IsCustom = true,
            IsUrlEdited = true
        };

        _componentsList.Insert(0, plugin);
        _updater.SaveComponents(_componentsList);

        TxtNewPluginName.Clear();
        TxtNewPluginUrl.Clear();
        TxtNewPluginVerify.Clear();
    }

    private void BtnDeletePlugin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ComponentItem comp } && comp.IsCustom)
        {
            if (System.Windows.MessageBox.Show($"Remove plugin '{comp.Name}' from the list? (Downloaded files are kept.)",
                "Remove Plugin", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _componentsList.Remove(comp);
                _updater.SaveComponents(_componentsList);
            }
        }
        else if (sender is Button)
        {
            System.Windows.MessageBox.Show("Built-in components cannot be removed. Only custom plugins can.",
                "Remove Plugin", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ------------------------------------------------------------------
    //  Settings tab
    // ------------------------------------------------------------------

    private void InitSettingsTab()
    {
        RbDoubleClickExtract.IsChecked = _settings.EnableDoubleClickQuickExtract;
        RbDoubleClickOpen.IsChecked = !_settings.EnableDoubleClickQuickExtract;
        ChkAutoTryPasswords.IsChecked = _settings.AutoTrySavedPasswords;

        RbMenuCascaded.IsChecked = _settings.ContextMenuCascaded;
        RbMenuNonCascaded.IsChecked = !_settings.ContextMenuCascaded;
        ChkContextMenuIcons.IsChecked = _settings.ContextMenuIcons;
        ChkItemOpen.IsChecked = _settings.ContextMenuOpen;
        ChkItemExtractFiles.IsChecked = _settings.ContextMenuExtractFiles;
        ChkItemExtractHere.IsChecked = _settings.ContextMenuExtractHere;
        ChkItemExtractTo.IsChecked = _settings.ContextMenuExtractTo;
        ChkItemTest.IsChecked = _settings.ContextMenuTest;
        ChkItemAdd.IsChecked = _settings.ContextMenuAdd;
        ChkItemAdd7z.IsChecked = _settings.ContextMenuAdd7z;
        ChkItemAddZip.IsChecked = _settings.ContextMenuAddZip;
        ChkItemCrcSha.IsChecked = _settings.ContextMenuCrcSha;
        ChkItemScanFileType.IsChecked = _settings.ContextMenuScanFileType;

        bool isReg = ShellAssociationService.IsContextMenuRegistered();
        TxtContextMenuStatus.Text = isReg
            ? "✓ Context menu is currently registered in Windows Explorer for all files and folders."
            : "Context menu is not yet registered. Click 'Register Context Menu for All Files' to enable.";

        bool isClassicReg = ShellAssociationService.IsClassic7ZipRegistered();
        TxtClassic7ZipStatus.Text = isClassicReg
            ? "✓ Classic 7-Zip shell extension is currently registered."
            : "Classic 7-Zip shell extension is not registered.";

        RefreshPasswordList();
    }

    private void RefreshPasswordList()
    {
        LbPasswords.Items.Clear();
        foreach (string pwd in _vault.Passwords)
        {
            LbPasswords.Items.Add("•••••••• (" + pwd.Length + " chars)");
        }
    }

    private void BtnAddPassword_Click(object sender, RoutedEventArgs e)
    {
        string pwd = TxtNewPassword.Text.Trim();
        if (!string.IsNullOrEmpty(pwd))
        {
            _vault.AddPassword(pwd);
            TxtNewPassword.Clear();
            RefreshPasswordList();
        }
    }

    private void BtnRemovePassword_Click(object sender, RoutedEventArgs e)
    {
        int idx = LbPasswords.SelectedIndex;
        if (idx >= 0 && idx < _vault.Passwords.Count)
        {
            string pwd = _vault.Passwords[idx];
            _vault.RemovePassword(pwd);
            RefreshPasswordList();
        }
    }

    private void BtnClearPasswords_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show("Are you sure you want to clear all remembered passwords?",
            "Clear Vault", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _vault.Clear();
            RefreshPasswordList();
        }
    }

    private void BtnRegisterShell_Click(object sender, RoutedEventArgs e)
    {
        bool extract = RbDoubleClickExtract.IsChecked == true;
        _settings.EnableDoubleClickQuickExtract = extract;
        _settings.Save();
        bool ok = extract
            ? ShellAssociationService.RegisterDoubleclickQuickExtract()
            : ShellAssociationService.RegisterOpenInBrowser();
        TxtShellStatus.Text = ok
            ? "✓ Explorer Double-Click associations successfully registered!"
            : "Could not update registry associations.";
    }

    private void BtnRegisterContextMenu_Click(object sender, RoutedEventArgs e)
    {
        _settings.ContextMenuCascaded = RbMenuCascaded.IsChecked == true;
        _settings.ContextMenuIcons = ChkContextMenuIcons.IsChecked == true;
        _settings.ContextMenuOpen = ChkItemOpen.IsChecked == true;
        _settings.ContextMenuExtractFiles = ChkItemExtractFiles.IsChecked == true;
        _settings.ContextMenuExtractHere = ChkItemExtractHere.IsChecked == true;
        _settings.ContextMenuExtractTo = ChkItemExtractTo.IsChecked == true;
        _settings.ContextMenuTest = ChkItemTest.IsChecked == true;
        _settings.ContextMenuAdd = ChkItemAdd.IsChecked == true;
        _settings.ContextMenuAdd7z = ChkItemAdd7z.IsChecked == true;
        _settings.ContextMenuAddZip = ChkItemAddZip.IsChecked == true;
        _settings.ContextMenuCrcSha = ChkItemCrcSha.IsChecked == true;
        _settings.ContextMenuScanFileType = ChkItemScanFileType.IsChecked == true;
        _settings.Save();

        bool ok = ShellAssociationService.RegisterAllFilesContextMenu(_settings);
        string styleDesc = _settings.ContextMenuCascaded ? "cascaded sub-menu" : "main context menu";
        TxtContextMenuStatus.Text = ok
            ? $"✓ Context menu options successfully registered in {styleDesc} for all files and folders!"
            : "Could not write context menu keys to registry.";
    }

    private void BtnUnregisterContextMenu_Click(object sender, RoutedEventArgs e)
    {
        bool ok = ShellAssociationService.UnregisterAllFilesContextMenu();
        TxtContextMenuStatus.Text = ok
            ? "Context menu options successfully removed from Explorer."
            : "Could not remove context menu keys.";
    }

    // ------------------------------------------------------------------
    //  Context menu item Select All / Deselect All
    // ------------------------------------------------------------------

    private void BtnContextSelectAll_Click(object sender, RoutedEventArgs e)
    {
        ChkItemOpen.IsChecked = true;
        ChkItemExtractFiles.IsChecked = true;
        ChkItemExtractHere.IsChecked = true;
        ChkItemExtractTo.IsChecked = true;
        ChkItemTest.IsChecked = true;
        ChkItemAdd.IsChecked = true;
        ChkItemAdd7z.IsChecked = true;
        ChkItemAddZip.IsChecked = true;
        ChkItemCrcSha.IsChecked = true;
        ChkItemScanFileType.IsChecked = true;
    }

    private void BtnContextDeselectAll_Click(object sender, RoutedEventArgs e)
    {
        ChkItemOpen.IsChecked = false;
        ChkItemExtractFiles.IsChecked = false;
        ChkItemExtractHere.IsChecked = false;
        ChkItemExtractTo.IsChecked = false;
        ChkItemTest.IsChecked = false;
        ChkItemAdd.IsChecked = false;
        ChkItemAdd7z.IsChecked = false;
        ChkItemAddZip.IsChecked = false;
        ChkItemCrcSha.IsChecked = false;
        ChkItemScanFileType.IsChecked = false;
    }

    // ------------------------------------------------------------------
    //  Classic 7-Zip native shell extension (7-zip.dll COM handler)
    // ------------------------------------------------------------------

    private void BtnRegisterClassic7Zip_Click(object sender, RoutedEventArgs e)
    {
        bool ok = ShellAssociationService.RegisterClassic7ZipShellExtension();
        if (ok)
        {
            TxtClassic7ZipStatus.Text = "✓ Classic 7-Zip shell extension successfully registered! Cascaded '7-Zip' submenu is now active.";
        }
        else
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string dll = System.IO.Path.Combine(dir, "7-zip.dll");
            TxtClassic7ZipStatus.Text = System.IO.File.Exists(dll)
                ? "Could not register the classic shell extension. Try running as Administrator."
                : "7-zip.dll not found in the application folder. Cannot register the classic shell extension.";
        }
    }

    private void BtnUnregisterClassic7Zip_Click(object sender, RoutedEventArgs e)
    {
        bool ok = ShellAssociationService.UnregisterClassic7ZipShellExtension();
        TxtClassic7ZipStatus.Text = ok
            ? "Classic 7-Zip shell extension successfully removed."
            : "Could not remove the classic shell extension.";
    }
}

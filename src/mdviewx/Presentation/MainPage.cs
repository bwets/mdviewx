using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;
using Path = System.IO.Path;

namespace mdviewx.Presentation;

public sealed partial class MainPage : Page
{
    private readonly WebView2 _webView = new();
    private readonly TextBlock _title = new() { Text = "mdviewx" };

    // Session (back/forward) history of opened file paths.
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private string? _currentPath;
    private int _navCounter;
    private readonly Dictionary<string, string> _titleCache = new(StringComparer.OrdinalIgnoreCase);

    private Button _backButton = null!;
    private Button _forwardButton = null!;
    private ToggleButton _pinToggle = null!;

    private FileSystemWatcher? _watcher;
    private readonly DispatcherQueueTimer _reloadTimer;

    // Sidebar
    private readonly ColumnDefinition _sidebarColumn = new() { Width = new GridLength(280) };
    private double _sidebarWidth = 280;
    private Border _sidebar = null!;
    private Grid _splitter = null!;
    private ToggleButton _tabHeaders = null!;
    private ToggleButton _tabSession = null!;
    private ToggleButton _tabGlobal = null!;
    private ListView _tocList = null!;
    private ListView _sessionList = null!;
    private ListView _globalList = null!;
    private bool _resizing;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private bool _initialized;

    public MainPage()
    {
        _reloadTimer = DispatcherQueue.CreateTimer();
        _reloadTimer.Interval = TimeSpan.FromMilliseconds(200);
        _reloadTimer.IsRepeating = false;
        _reloadTimer.Tick += (_, _) => Reload();

        _webView.NavigationStarting += (sender, args) =>
        {
            if (TryGetLocalMarkdownPath(args.Uri, out var path))
            {
                args.Cancel = true;
                sender.DispatcherQueue.TryEnqueue(() => Navigate(path));
            }
        };

        Loaded += (_, _) => TryShowStartupFile();
        Unloaded += (_, _) => _watcher?.Dispose();

        BuildToolbarButtons();
        BuildSidebar();
        SetupKeyboard();

        var contentGrid = new Grid()
            .Children(
                _sidebar.Grid(column: 0),
                _splitter.Grid(column: 1),
                _webView.Grid(column: 2));
        contentGrid.ColumnDefinitions.Add(_sidebarColumn);
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        SelectTab(0);
        ApplySidebarState();

        var leftToolbar = new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(2)
            .Margin(new Thickness(6, 0, 0, 0))
            .VerticalAlignment(VerticalAlignment.Center)
            .Children(
                _pinToggle,
                _backButton,
                _forwardButton,
                IconButton("Refresh (F5)", Symbol.Refresh, Reload),
                IconButton("Print (Ctrl+P)", Symbol.Print, Print),
                IconButton("Open containing folder", Symbol.Folder, OpenContainingFolder));

        this.DataContext<MainViewModel>((page, vm) =>
        {
            var moreFlyout = new MenuFlyout();
            moreFlyout.Items.Add(new MenuFlyoutItem()
                .Text("Settings")
                .Icon(new SymbolIcon(Symbol.Setting))
                .Command(() => vm.GoToSettings));

            var moreButton = new Button
            {
                Content = new SymbolIcon(Symbol.More),
                Padding = new Thickness(8, 6, 8, 6),
                Flyout = moreFlyout,
            };
            ApplyChromelessStyle(moreButton);
            ToolTipService.SetToolTip(moreButton, "More");

            var topBar = new Border()
                .BorderThickness(new Thickness(0, 0, 0, 1))
                .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
                .Child(new Grid()
                    .Height(48)
                    .ColumnDefinitions("Auto,*,Auto")
                    .Children(
                        leftToolbar.Grid(column: 0),
                        _title
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Margin(new Thickness(12, 0, 0, 0))
                            .Grid(column: 1),
                        moreButton
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Grid(column: 2)));

            page
                .NavigationCacheMode(NavigationCacheMode.Required)
                .Background(ThemeResource.Get<Brush>("ApplicationPageBackgroundThemeBrush"))
                .Content(new Grid()
                    .SafeArea(SafeArea.InsetMask.VisibleBounds)
                    .RowDefinitions("Auto,*")
                    .Children(
                        topBar.Grid(row: 0),
                        contentGrid.Grid(row: 1)));
        });
    }

    private static IMarkdownRenderer? Renderer => App.Services?.GetService<IMarkdownRenderer>();

    private static IHistoryService? History => App.Services?.GetService<IHistoryService>();

    private static IPreferencesService? Prefs => App.Services?.GetService<IPreferencesService>();

    private void BuildToolbarButtons()
    {
        _pinToggle = new ToggleButton { Content = new SymbolIcon(Symbol.GlobalNavigationButton), IsChecked = true };
        ApplyChromelessStyle(_pinToggle);
        ToolTipService.SetToolTip(_pinToggle, "Toggle sidebar (Ctrl+B)");
        _pinToggle.Click += (_, _) =>
        {
            if (Prefs is { } p)
            {
                p.Current.SidebarPinned = _pinToggle.IsChecked == true;
                p.Save();
            }

            ApplySidebarState();
        };

        _backButton = IconButton("Back (Alt+Left)", Symbol.Back, GoBack);
        _forwardButton = IconButton("Forward (Alt+Right)", Symbol.Forward, GoForward);
    }

    private static Button IconButton(string tooltip, Symbol symbol, Action onClick)
    {
        var button = new Button
        {
            Content = new SymbolIcon(symbol),
            Padding = new Thickness(8, 6, 8, 6),
        };
        ApplyChromelessStyle(button);
        button.Click += (_, _) => onClick();
        ToolTipService.SetToolTip(button, tooltip);
        return button;
    }

    /// <summary>
    /// Makes a Button/ToggleButton transparent in its normal and disabled states (subtle on hover,
    /// accent when checked) with a readable foreground in every visual state, instead of the default
    /// filled chrome.
    /// </summary>
    private static void ApplyChromelessStyle(Control control)
    {
        var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        var resources = control.Resources;

        void Set(string key, string? themeKey)
        {
            var brush = themeKey is null ? transparent : ResolveBrush(themeKey);
            if (brush is not null)
            {
                resources[key] = brush;
            }
        }

        foreach (var p in new[] { "Button", "ToggleButton" })
        {
            Set(p + "Background", null);
            Set(p + "BackgroundDisabled", null);
            Set(p + "BackgroundPointerOver", "SubtleFillColorSecondaryBrush");
            Set(p + "BackgroundPressed", "SubtleFillColorTertiaryBrush");
            Set(p + "BorderBrush", null);
            Set(p + "BorderBrushPointerOver", null);
            Set(p + "BorderBrushPressed", null);
            Set(p + "BorderBrushDisabled", null);
            Set(p + "Foreground", "TextFillColorPrimaryBrush");
            Set(p + "ForegroundPointerOver", "TextFillColorPrimaryBrush");
            Set(p + "ForegroundPressed", "TextFillColorPrimaryBrush");
            Set(p + "ForegroundDisabled", "TextFillColorDisabledBrush");
        }

        // Checked (toggle) states use the accent color with on-accent text.
        Set("ToggleButtonBackgroundChecked", "AccentFillColorDefaultBrush");
        Set("ToggleButtonBackgroundCheckedPointerOver", "AccentFillColorSecondaryBrush");
        Set("ToggleButtonBackgroundCheckedPressed", "AccentFillColorTertiaryBrush");
        Set("ToggleButtonBorderBrushChecked", null);
        Set("ToggleButtonBorderBrushCheckedPointerOver", null);
        Set("ToggleButtonBorderBrushCheckedPressed", null);
        Set("ToggleButtonForegroundChecked", "TextOnAccentFillColorPrimaryBrush");
        Set("ToggleButtonForegroundCheckedPointerOver", "TextOnAccentFillColorPrimaryBrush");
        Set("ToggleButtonForegroundCheckedPressed", "TextOnAccentFillColorPrimaryBrush");
    }

    private void SetupKeyboard()
    {
        AddAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu, GoBack);
        AddAccelerator(VirtualKey.Right, VirtualKeyModifiers.Menu, GoForward);
        AddAccelerator(VirtualKey.F5, VirtualKeyModifiers.None, Reload);
        AddAccelerator(VirtualKey.R, VirtualKeyModifiers.Control, Reload);
        AddAccelerator(VirtualKey.P, VirtualKeyModifiers.Control, Print);
        AddAccelerator(VirtualKey.B, VirtualKeyModifiers.Control, ToggleSidebar);
    }

    private void AddAccelerator(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, e) =>
        {
            e.Handled = true;
            action();
        };
        KeyboardAccelerators.Add(accelerator);
    }

    private static Brush? ResolveBrush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush ? brush : null;

    private static void StyleList(ListView list)
    {
        var primary = ResolveBrush("TextFillColorPrimaryBrush");
        if (primary is null)
        {
            return;
        }

        list.Foreground = primary;
        foreach (var key in new[]
        {
            "ListViewItemForeground",
            "ListViewItemForegroundPointerOver",
            "ListViewItemForegroundPressed",
            "ListViewItemForegroundSelected",
            "ListViewItemForegroundSelectedPointerOver",
            "ListViewItemForegroundSelectedPressed",
        })
        {
            list.Resources[key] = primary;
        }
    }

    private void ToggleSidebar()
    {
        if (Prefs is { } p)
        {
            p.Current.SidebarPinned = !p.Current.SidebarPinned;
            p.Save();
        }

        ApplySidebarState();
    }

    private void BuildSidebar()
    {
        _tabHeaders = new ToggleButton().Content("Headers").HorizontalAlignment(HorizontalAlignment.Stretch);
        _tabHeaders.Click += (_, _) => SelectTab(0);
        _tabSession = new ToggleButton().Content("Session").HorizontalAlignment(HorizontalAlignment.Stretch);
        _tabSession.Click += (_, _) => SelectTab(1);
        _tabGlobal = new ToggleButton().Content("Global").HorizontalAlignment(HorizontalAlignment.Stretch);
        _tabGlobal.Click += (_, _) => SelectTab(2);
        foreach (var tab in new[] { _tabHeaders, _tabSession, _tabGlobal })
        {
            ApplyChromelessStyle(tab);
            // Rounded top, square bottom so the active (accent) tab merges into the line below it.
            tab.CornerRadius = new CornerRadius(6, 6, 0, 0);
            tab.BorderThickness = new Thickness(0);
        }

        var tabButtons = new Grid()
            .ColumnDefinitions("*,*,*")
            .Children(
                _tabHeaders.Grid(column: 0),
                _tabSession.Grid(column: 1),
                _tabGlobal.Grid(column: 2));

        // Accent highlight line that the active tab connects to.
        var accentLine = new Border
        {
            Height = 2,
            Background = ResolveBrush("AccentFillColorDefaultBrush") ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        };

        var tabs = new StackPanel()
            .Margin(new Thickness(8, 8, 8, 0))
            .Children(tabButtons, accentLine);

        _tocList = new ListView().IsItemClickEnabled(true);
        _tocList.ItemClick += (_, e) =>
        {
            if (e.ClickedItem is TocItem item)
            {
                ScrollTo(item.Id);
            }
        };
        _sessionList = new ListView().IsItemClickEnabled(true);
        _sessionList.ItemClick += (_, e) =>
        {
            if (e.ClickedItem is SessionItem item)
            {
                GoToHistoryIndex(item.Index);
            }
        };
        _globalList = new ListView().IsItemClickEnabled(true);
        _globalList.ItemClick += (_, e) =>
        {
            if (e.ClickedItem is GlobalItem item && File.Exists(item.PathValue))
            {
                Navigate(item.PathValue);
            }
        };

        StyleList(_tocList);
        StyleList(_sessionList);
        StyleList(_globalList);

        // History rows show the page title, with the filename in a tooltip and a right-click menu.
        foreach (var list in new[] { _sessionList, _globalList })
        {
            list.RightTapped += OnHistoryRightTapped;
            list.ContainerContentChanging += OnHistoryContainerContentChanging;
        }

        var content = new Grid().Children(_tocList, _sessionList, _globalList);

        var inner = new Grid()
            .RowDefinitions("Auto,*")
            .Children(tabs.Grid(row: 0), content.Grid(row: 1));

        _sidebar = new Border()
            .BorderThickness(new Thickness(0, 0, 1, 0))
            .BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))
            .Background(ThemeResource.Get<Brush>("LayerFillColorDefaultBrush"))
            .Child(inner);

        var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        var hoverBrush = ResolveBrush("AccentFillColorDefaultBrush") ?? ResolveBrush("CardStrokeColorDefaultBrush");
        _splitter = new ResizeGrip
        {
            Width = 10,
            Background = transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _splitter.PointerEntered += (_, _) =>
        {
            if (hoverBrush is not null)
            {
                _splitter.Background = hoverBrush;
            }
        };
        _splitter.PointerExited += (_, _) =>
        {
            if (!_resizing)
            {
                _splitter.Background = transparent;
            }
        };
        _splitter.PointerPressed += (_, e) =>
        {
            _resizing = true;
            if (hoverBrush is not null)
            {
                _splitter.Background = hoverBrush;
            }

            _splitter.CapturePointer(e.Pointer);
            _resizeStartX = e.GetCurrentPoint(this).Position.X;
            _resizeStartWidth = _sidebarWidth;
        };
        _splitter.PointerMoved += (_, e) =>
        {
            if (!_resizing)
            {
                return;
            }

            var x = e.GetCurrentPoint(this).Position.X;
            _sidebarWidth = Math.Clamp(_resizeStartWidth + (x - _resizeStartX), 150, 700);
            _sidebarColumn.Width = new GridLength(_sidebarWidth);
        };
        _splitter.PointerReleased += (_, e) =>
        {
            if (!_resizing)
            {
                return;
            }

            _resizing = false;
            _splitter.ReleasePointerCapture(e.Pointer);
            if (Prefs is { } p)
            {
                p.Current.SidebarWidth = _sidebarWidth;
                p.Save();
            }
        };
    }

    private void SelectTab(int index)
    {
        index = Math.Clamp(index, 0, 2);
        _tabHeaders.IsChecked = index == 0;
        _tabSession.IsChecked = index == 1;
        _tabGlobal.IsChecked = index == 2;
        _tocList.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        _sessionList.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        _globalList.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        if (Prefs is { } p)
        {
            p.Current.SidebarTab = index;
            p.Save();
        }
    }

    private void ApplySidebarState()
    {
        var pinned = Prefs?.Current.SidebarPinned ?? true;
        _pinToggle.IsChecked = pinned;
        _sidebarColumn.Width = pinned ? new GridLength(_sidebarWidth) : new GridLength(0);
        _sidebar.Visibility = pinned ? Visibility.Visible : Visibility.Collapsed;
        _splitter.Visibility = pinned ? Visibility.Visible : Visibility.Collapsed;
    }

    private void InitFromPrefs()
    {
        if (Prefs is not { } p)
        {
            return;
        }

        if (p.Current.SidebarWidth >= 150)
        {
            _sidebarWidth = p.Current.SidebarWidth;
        }

        SelectTab(Math.Clamp(p.Current.SidebarTab, 0, 2));
        ApplySidebarState();
        UpdateGlobalList();
    }

    // The DI container is only available once the host finishes building, which can happen slightly
    // after the page is Loaded; retry on the dispatcher until it is ready.
    private void TryShowStartupFile(int attempt = 0)
    {
        if (Renderer is null)
        {
            if (attempt < 50)
            {
                DispatcherQueue.TryEnqueue(() => TryShowStartupFile(attempt + 1));
            }

            return;
        }

        if (!_initialized)
        {
            _initialized = true;
            InitFromPrefs();
        }

        var path = Renderer.GetStartupFilePath();
        if (path is not null)
        {
            Navigate(path);
        }
    }

    /// <summary>Opens a file as a new entry in the back/forward history.</summary>
    private void Navigate(string path)
    {
        if (_historyIndex < _history.Count - 1)
        {
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        }

        if (_history.Count == 0 || !PathEquals(_history[^1], path))
        {
            _history.Add(path);
        }

        _historyIndex = _history.Count - 1;
        SetCurrent(path);
    }

    private void GoBack()
    {
        if (_historyIndex > 0)
        {
            _historyIndex--;
            SetCurrent(_history[_historyIndex]);
        }
    }

    private void GoForward()
    {
        if (_historyIndex < _history.Count - 1)
        {
            _historyIndex++;
            SetCurrent(_history[_historyIndex]);
        }
    }

    private void GoToHistoryIndex(int index)
    {
        if (index >= 0 && index < _history.Count)
        {
            _historyIndex = index;
            SetCurrent(_history[index]);
        }
    }

    private void SetCurrent(string path)
    {
        _currentPath = path;
        SetupWatcher(path);
        var title = DisplayCurrent();
        History?.Record(path, title);
        UpdateCommandStates();
        UpdateSessionList();
        UpdateGlobalList();
    }

    /// <summary>Re-renders the current document and refreshes the session list (used for reloads).</summary>
    private void Reload()
    {
        DisplayCurrent();
        UpdateSessionList();
    }

    /// <summary>(Re)renders the current document and shows it. Returns the document title.</summary>
    private string DisplayCurrent()
    {
        if (_currentPath is null)
        {
            return "mdviewx";
        }

        var fallback = Path.GetFileNameWithoutExtension(_currentPath);
        var result = Renderer?.Render(_currentPath);
        if (result is null)
        {
            return fallback;
        }

        // A changing query forces the WebView to reload even when the file path is unchanged.
        _webView.Source = new Uri(result.Uri.AbsoluteUri + "?v=" + (++_navCounter));
        var title = DeriveTitle(result.Headings, _currentPath);
        _title.Text = title;
        ToolTipService.SetToolTip(_title, Path.GetFileName(_currentPath));
        _titleCache[Path.GetFullPath(_currentPath)] = title;
        UpdateToc(result.Headings);
        return title;
    }

    private static string DeriveTitle(IReadOnlyList<HeadingInfo> headings, string path)
    {
        var heading = headings.FirstOrDefault(h => h.Level == 1) ?? headings.FirstOrDefault();
        return heading is not null && !string.IsNullOrWhiteSpace(heading.Text)
            ? heading.Text
            : Path.GetFileNameWithoutExtension(path);
    }

    private string TitleFor(string path) =>
        _titleCache.TryGetValue(Path.GetFullPath(path), out var title)
            ? title
            : Path.GetFileNameWithoutExtension(path);

    private void UpdateToc(IReadOnlyList<HeadingInfo> headings) =>
        _tocList.ItemsSource = headings
            .Select(h => new TocItem { Level = h.Level, Text = h.Text, Id = h.Id })
            .ToList();

    private void UpdateSessionList()
    {
        var items = new List<SessionItem>();
        for (var i = 0; i < _history.Count; i++)
        {
            items.Add(new SessionItem
            {
                Index = i,
                PathValue = _history[i],
                Title = TitleFor(_history[i]),
                Current = i == _historyIndex,
            });
        }

        _sessionList.ItemsSource = items;
    }

    private void UpdateGlobalList()
    {
        var entries = History?.Entries ?? (IReadOnlyList<HistoryEntry>)Array.Empty<HistoryEntry>();
        _globalList.ItemsSource = entries
            .Select(e => new GlobalItem { PathValue = e.Path, Title = e.Title })
            .ToList();
    }

    private async void ScrollTo(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        var escaped = id.Replace("\\", "\\\\").Replace("'", "\\'");
        var js = $"(function(){{var e=document.getElementById('{escaped}');if(e)e.scrollIntoView({{behavior:'smooth'}});}})();";
        try
        {
            await _webView.ExecuteScriptAsync(js);
        }
        catch
        {
            // Best-effort.
        }
    }

    private void SetupWatcher(string path)
    {
        _watcher?.Dispose();
        _watcher = null;

        var dir = Path.GetDirectoryName(path);
        if (dir is null || !Directory.Exists(dir))
        {
            return;
        }

        var watcher = new FileSystemWatcher(dir, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
        };
        void OnChanged(object? s, FileSystemEventArgs e) => ScheduleReload();
        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Renamed += (_, _) => ScheduleReload();
        watcher.EnableRaisingEvents = true;
        _watcher = watcher;
    }

    private void ScheduleReload() =>
        DispatcherQueue.TryEnqueue(() =>
        {
            _reloadTimer.Stop();
            _reloadTimer.Start();
        });

    private void UpdateCommandStates()
    {
        _backButton.IsEnabled = _historyIndex > 0;
        _forwardButton.IsEnabled = _historyIndex >= 0 && _historyIndex < _history.Count - 1;
    }

    private async void Print()
    {
        try
        {
            await _webView.ExecuteScriptAsync("window.print();");
        }
        catch
        {
            // Printing is best-effort.
        }
    }

    private void OpenContainingFolder()
    {
        if (_currentPath is not null)
        {
            OpenFolderFor(_currentPath);
        }
    }

    private static void OpenFolderFor(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            ProcessStartInfo info;
            if (OperatingSystem.IsWindows())
            {
                info = new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"");
            }
            else if (OperatingSystem.IsMacOS())
            {
                info = new ProcessStartInfo("open", $"-R \"{path}\"");
            }
            else
            {
                info = new ProcessStartInfo("xdg-open", $"\"{Path.GetDirectoryName(path)}\"");
            }

            info.UseShellExecute = true;
            Process.Start(info);
        }
        catch
        {
            // Opening the folder is best-effort.
        }
    }

    private static void OpenInNewWindow(string path)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (exe is not null)
            {
                Process.Start(new ProcessStartInfo(exe, $"\"{path}\"") { UseShellExecute = false });
            }
        }
        catch
        {
            // Best-effort.
        }
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            var data = new DataPackage();
            data.SetText(text);
            Clipboard.SetContent(data);
        }
        catch
        {
            // Best-effort.
        }
    }

    private MenuFlyout BuildHistoryContextMenu(string path)
    {
        var flyout = new MenuFlyout();
        flyout.Items.Add(HistoryMenuItem("Open", () => Navigate(path)));
        flyout.Items.Add(HistoryMenuItem("Open in new window", () => OpenInNewWindow(path)));
        flyout.Items.Add(HistoryMenuItem("Copy filename", () => CopyToClipboard(Path.GetFileName(path))));
        flyout.Items.Add(HistoryMenuItem("Open containing folder", () => OpenFolderFor(path)));
        return flyout;
    }

    private static MenuFlyoutItem HistoryMenuItem(string text, Action action)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Click += (_, _) => action();
        return item;
    }

    private void OnHistoryRightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        if ((args.OriginalSource as FrameworkElement)?.DataContext is IHistoryRow row && sender is FrameworkElement owner)
        {
            var flyout = BuildHistoryContextMenu(row.PathValue);
            flyout.ShowAt(owner, new FlyoutShowOptions { Position = args.GetPosition(owner) });
            args.Handled = true;
        }
    }

    private static void OnHistoryContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.ItemContainer is ListViewItem container && args.Item is IHistoryRow row)
        {
            ToolTipService.SetToolTip(container, row.PathValue);
        }
    }

    private static bool PathEquals(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static bool TryGetLocalMarkdownPath(string? uriString, out string path)
    {
        path = string.Empty;
        if (Uri.TryCreate(uriString, UriKind.Absolute, out var uri)
            && uri.IsFile
            && uri.LocalPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            && File.Exists(uri.LocalPath))
        {
            path = uri.LocalPath;
            return true;
        }

        return false;
    }

    private sealed class TocItem
    {
        public int Level { get; init; }
        public string Text { get; init; } = string.Empty;
        public string Id { get; init; } = string.Empty;
        public override string ToString() => new string(' ', Math.Max(0, Level - 1) * 3) + Text;
    }

    private interface IHistoryRow
    {
        string PathValue { get; }
    }

    private sealed class SessionItem : IHistoryRow
    {
        public int Index { get; init; }
        public string PathValue { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public bool Current { get; init; }
        public override string ToString() => (Current ? "● " : "   ") + Title;
    }

    private sealed class GlobalItem : IHistoryRow
    {
        public string PathValue { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public override string ToString() => Title;
    }
}

using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using SkillPalette.Core.Skills;
using SkillPalette_Windows.Native;
using SkillPalette_Windows.Services;
using SkillPalette_Windows.ViewModels;

namespace SkillPalette_Windows;

public sealed partial class MainPage : Page
{
    private readonly SkillCatalogScanner _scanner = new();
    private readonly SkillSearchEngine _search = new();
    private readonly SkillMetadataStore _metadataStore = new();
    private SkillCatalogSnapshot? _snapshot;
    private Dictionary<string, SkillMetadata> _metadata = new(StringComparer.OrdinalIgnoreCase);
    private readonly GlobalKeyboardListener _listener = new();
    private readonly DispatcherQueue _dispatcher;
    private readonly TargetRuleStore _targetRuleStore = new();
    private readonly GeneralSettingsStore _generalSettingsStore = new();
    private readonly StartupRegistrationService _startupRegistration = new();
    private PaletteWindow? _palette;
    private TargetRuleSettings _targetSettings = TargetRuleSettings.CreateDefault();
    private GeneralSettings _generalSettings = new();
    private bool _targetSettingsLoaded;
    private bool _generalSettingsLoaded;
    private bool _initializationComplete;
    private bool _isListenerStatusDialogOpen;
    private string _lastSession = "尚无会话。";
    private DateTimeOffset? _lastSessionAt;

    public ObservableCollection<SkillListItem> Skills { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["全部"];

    public MainPage()
    {
        InitializeComponent();
        _dispatcher = DispatcherQueue;
        _listener.Triggered += OnTriggerReceived;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _metadata = await _metadataStore.LoadAsync();
        _generalSettings = await _generalSettingsStore.LoadAsync();
        _generalSettingsLoaded = true;
        TriggerCharacterBox.Text = _generalSettings.TriggerCharacter;
        InvocationPrefixBox.Text = _generalSettings.InvocationPrefix;
        ListeningEnabledSwitch.IsOn = _generalSettings.IsListeningEnabled;
        LaunchAtStartupSwitch.IsOn = _generalSettings.IsLaunchAtStartupEnabled;
        ApplyGeneralSettings();
        ApplyStartupRegistration();
        _targetSettings = await _targetRuleStore.LoadAsync();
        _targetSettingsLoaded = true;
        TargetOnlyModeSwitch.IsOn = _targetSettings.TargetOnlyMode;
        ApplyTargetRules();
        RenderTargetRules();
        await ScanAsync();
        StartListener();
        _initializationComplete = true;
        await CheckListeningStatusAsync();
    }

    private void StartListener()
    {
        if (!_generalSettings.IsListeningEnabled)
        {
            GeneralStatus.Severity = InfoBarSeverity.Informational;
            GeneralStatus.Title = "全局监听已暂停";
            GeneralStatus.Message = "启用后才会响应触发字符。";
            return;
        }
        try { _listener.Start(); }
        catch (Exception ex)
        {
            ScanStatus.Severity = InfoBarSeverity.Warning;
            ScanStatus.Title = "Skill 库可用，但监听器未启动";
            ScanStatus.Message = ex.Message;
        }
    }

    private void OnTriggerReceived(ForegroundTarget target, char triggerCharacter) => _dispatcher.TryEnqueue(() =>
    {
        _lastSessionAt = DateTimeOffset.Now;
        _lastSession = $"已在 {target.ProcessName} 中打开面板；等待选择或取消。";
        _palette ??= CreatePalette();
        _palette.Open(target, triggerCharacter, _generalSettings.InvocationPrefix);
    });

    private PaletteWindow CreatePalette()
    {
        var palette = new PaletteWindow();
        palette.ActionRequested += Palette_ActionRequested;
        palette.Closed += (_, _) =>
        {
            if (ReferenceEquals(_palette, palette)) _palette = null;
            Palette_ActionRequested(palette, new PaletteActionEventArgs(palette.Target, PaletteAction.Cancel, triggerCharacter: palette.TriggerCharacter));
        };
        return palette;
    }

    private async void Palette_ActionRequested(object? sender, PaletteActionEventArgs e)
    {
        _palette?.ClosePalette();
        if (!InputReplay.RestoreFocus(e.Target))
        {
            ScanSummary.Text = "焦点恢复失败：未向未知窗口发送输入。";
            _lastSession = "焦点恢复失败：未向未知窗口发送输入。";
            return;
        }
        await Task.Delay(120);

        if (e.Action == PaletteAction.Cancel)
        {
            _ = InputReplay.SendUnicode(e.TriggerCharacter);
            _lastSession = "用户取消：已将触发字符还原到原应用。";
            return;
        }

        var original = Clipboard.GetContent();
        var canRestoreText = original.Contains(StandardDataFormats.Text);
        var originalText = canRestoreText ? await original.GetTextAsync() : null;
        var package = new DataPackage();
        package.SetText(e.Invocation ?? "@write-a-prd ");
        Clipboard.SetContent(package);
        Clipboard.Flush();
        var insertionSequence = NativeMethods.GetClipboardSequenceNumber();
        _ = InputReplay.SendPasteShortcut();
        _lastSession = "已将所选 Skill 调用发送到原应用。";

        await Task.Delay(500);
        if (canRestoreText && NativeMethods.GetClipboardSequenceNumber() == insertionSequence)
        {
            var restorePackage = new DataPackage();
            restorePackage.SetText(originalText ?? string.Empty);
            Clipboard.SetContent(restorePackage);
            Clipboard.Flush();
        }
        RenderDiagnostics();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _listener.Dispose();

    private async void RescanButton_Click(object sender, RoutedEventArgs e) => await ScanAsync();

    internal void NavigateTo(string page)
    {
        var item = SettingsNavigation.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(menuItem => string.Equals(menuItem.Tag as string, page, StringComparison.Ordinal));

        if (item is not null)
        {
            SettingsNavigation.SelectedItem = item;
        }
    }

    internal async Task CheckListeningStatusAsync()
    {
        if (!_initializationComplete || _isListenerStatusDialogOpen || (_generalSettings.IsListeningEnabled && _listener.IsRunning))
        {
            return;
        }

        _isListenerStatusDialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "监听未开启",
                Content = "全局监听当前未开启，无法通过触发字符在目标应用中打开 Skill Palette。请先在“常规”中开启监听，以确保软件可以正常工作。",
                PrimaryButtonText = "开启监听",
                CloseButtonText = "稍后",
                DefaultButton = ContentDialogButton.Primary,
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                NavigateTo("General");
            }
        }
        finally
        {
            _isListenerStatusDialogOpen = false;
        }
    }

    internal async Task RescanFromTrayAsync()
    {
        await ScanAsync();

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "扫描结果",
            Content = $"{ScanStatus.Title}\n{ScanStatus.Message}",
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close,
        };
        await dialog.ShowAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RenderResults();

    private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => RenderResults();

    private void FavoriteFilter_Changed(object sender, RoutedEventArgs e) => RenderResults();

    private void SettingsNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var selected = args.SelectedItemContainer as NavigationViewItem;
        var page = selected?.Tag as string;
        var isSkillsPage = page == "Skills";
        var isGeneralPage = page == "General";
        var isTargetsPage = page == "Targets";
        var isDiagnosticsPage = page == "Diagnostics";
        var isAboutPage = page == "About";
        SkillLibraryContent.Visibility = isSkillsPage ? Visibility.Visible : Visibility.Collapsed;
        GeneralContent.Visibility = isGeneralPage ? Visibility.Visible : Visibility.Collapsed;
        TargetApplicationsContent.Visibility = isTargetsPage ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsContent.Visibility = isDiagnosticsPage ? Visibility.Visible : Visibility.Collapsed;
        AboutContent.Visibility = isAboutPage ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderContent.Visibility = isSkillsPage || isGeneralPage || isTargetsPage || isDiagnosticsPage || isAboutPage ? Visibility.Collapsed : Visibility.Visible;

        if (isDiagnosticsPage) RenderDiagnostics();

        if (!isSkillsPage && !isGeneralPage && !isTargetsPage && !isDiagnosticsPage && !isAboutPage)
        {
            PlaceholderTitle.Text = selected?.Content?.ToString() ?? "设置";
            PlaceholderDescription.Text = page switch
            {
                "General" => "控制 Skill Palette 的显示、触发与启动方式。",
                "Targets" => "管理哪些桌面应用可以响应 Skill Palette 触发字符。",
                "Diagnostics" => "查看监听、前台窗口、扫描和粘贴回填的脱敏状态。",
                "About" => "查看版本、隐私说明和发布信息。",
                _ => string.Empty
            };
        }
    }

    private void RefreshDiagnosticsButton_Click(object sender, RoutedEventArgs e) => RenderDiagnostics();

    private void CopyDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        var foreground = ForegroundWindowInspector.GetCurrent();
        var package = new DataPackage();
        package.SetText($"Skill Palette 诊断摘要\n监听：{(_listener.IsRunning ? "运行中" : "已停止")}\n触发字符：{_generalSettings.TriggerCharacter}\n前台应用：{foreground.ProcessName}（PID {foreground.ProcessId}）\n目标规则：{_targetSettings.Rules.Count(rule => rule.IsEnabled)} 条启用，目标模式 {(_targetSettings.TargetOnlyMode ? "开启" : "关闭")}\n最近会话：{_lastSession}\nSkill 索引：{_snapshot?.Skills.Count ?? 0} 个，警告 {_snapshot?.Warnings.Count ?? 0} 条");
        Clipboard.SetContent(package);
        Clipboard.Flush();
    }

    private void RenderDiagnostics()
    {
        if (DiagnosticsListenerText is null) return;
        var foreground = ForegroundWindowInspector.GetCurrent();
        DiagnosticsListenerText.Text = _listener.IsRunning
            ? $"正在监听“{_generalSettings.TriggerCharacter}”。"
            : "监听已停止；触发字符不会打开 Skill Palette。";
        DiagnosticsForegroundText.Text = $"{foreground.ProcessName}（PID {foreground.ProcessId}）";
        DiagnosticsTargetsText.Text = _targetSettings.TargetOnlyMode
            ? $"仅目标应用模式开启；{_targetSettings.Rules.Count(rule => rule.IsEnabled)} 条规则已启用。"
            : "仅目标应用模式关闭；除 Skill Palette 自身外，所有前台应用均可响应。";
        DiagnosticsSessionText.Text = _lastSessionAt is null ? _lastSession : $"{_lastSessionAt:HH:mm:ss} · {_lastSession}";
        DiagnosticsCatalogText.Text = _snapshot is null
            ? "尚未完成扫描。"
            : $"已索引 {_snapshot.Skills.Count} 个 Skill；扫描警告 {_snapshot.Warnings.Count} 条。";
    }

    private async void TriggerCharacterBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_generalSettingsLoaded) return;
        if (!GeneralSettings.IsValidTrigger(TriggerCharacterBox.Text))
        {
            TriggerCharacterBox.Text = _generalSettings.TriggerCharacter;
            GeneralStatus.Severity = InfoBarSeverity.Warning;
            GeneralStatus.Title = "触发字符未修改";
            GeneralStatus.Message = "请输入一个非空白的可打印字符。";
            return;
        }
        _generalSettings.TriggerCharacter = TriggerCharacterBox.Text;
        await SaveGeneralSettingsAsync();
    }

    private async void InvocationPrefixBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_generalSettingsLoaded) return;
        _generalSettings.InvocationPrefix = InvocationPrefixBox.Text;
        await SaveGeneralSettingsAsync();
    }

    private async void ListeningEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_generalSettingsLoaded) return;
        _generalSettings.IsListeningEnabled = ListeningEnabledSwitch.IsOn;
        if (_generalSettings.IsListeningEnabled) StartListener(); else _listener.Stop();
        await SaveGeneralSettingsAsync();
    }

    private async void LaunchAtStartupSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_generalSettingsLoaded) return;
        _generalSettings.IsLaunchAtStartupEnabled = LaunchAtStartupSwitch.IsOn;
        await SaveGeneralSettingsAsync();
    }

    private void ApplyGeneralSettings()
    {
        _generalSettings.Normalize();
        _listener.UpdateTriggerCharacter(_generalSettings.TriggerCharacter[0]);
        GeneralStatus.Severity = _generalSettings.IsListeningEnabled ? InfoBarSeverity.Success : InfoBarSeverity.Informational;
        GeneralStatus.Title = _generalSettings.IsListeningEnabled ? "全局监听已启用" : "全局监听已暂停";
        GeneralStatus.Message = _generalSettings.IsListeningEnabled
            ? $"在目标应用中输入 {_generalSettings.TriggerCharacter} 可打开 Skill Palette。"
            : "启用后才会响应触发字符。";
    }

    private async Task SaveGeneralSettingsAsync()
    {
        ApplyGeneralSettings();
        ApplyStartupRegistration();
        TriggerCharacterBox.Text = _generalSettings.TriggerCharacter;
        InvocationPrefixBox.Text = _generalSettings.InvocationPrefix;
        await _generalSettingsStore.SaveAsync(_generalSettings);
    }

    private void ApplyStartupRegistration()
    {
        try
        {
            _startupRegistration.SetEnabled(_generalSettings.IsLaunchAtStartupEnabled);
        }
        catch (Exception ex)
        {
            GeneralStatus.Severity = InfoBarSeverity.Warning;
            GeneralStatus.Title = "无法更新开机启动";
            GeneralStatus.Message = ex.Message;
        }
    }

    private void TargetOnlyModeSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_targetSettingsLoaded) return;
        _targetSettings.TargetOnlyMode = TargetOnlyModeSwitch.IsOn;
        _ = SaveTargetRulesAsync();
    }

    private async void AddTargetRuleButton_Click(object sender, RoutedEventArgs e)
    {
        var processName = TargetRuleSettings.NormalizeProcessName(NewProcessNameBox.Text);
        if (string.IsNullOrWhiteSpace(processName))
        {
            TargetRulesSummary.Text = "请输入有效的进程名。";
            return;
        }
        if (_targetSettings.Rules.Any(rule => string.Equals(rule.ProcessName, processName, StringComparison.OrdinalIgnoreCase)))
        {
            TargetRulesSummary.Text = $"{processName} 已在规则列表中。";
            return;
        }
        _targetSettings.Rules.Add(new TargetAppRule { ProcessName = processName });
        NewProcessNameBox.Text = string.Empty;
        await SaveTargetRulesAsync();
    }

    private void RenderTargetRules()
    {
        if (TargetRulesPanel is null) return;
        TargetRulesPanel.Children.Clear();
        foreach (var rule in _targetSettings.Rules)
        {
            var row = new Grid { ColumnSpacing = 12, MinHeight = 44 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var enabled = new ToggleSwitch { IsOn = rule.IsEnabled, VerticalAlignment = VerticalAlignment.Center, OffContent = "停用", OnContent = "启用" };
            enabled.Toggled += async (_, _) =>
            {
                rule.IsEnabled = enabled.IsOn;
                await SaveTargetRulesAsync();
            };
            row.Children.Add(enabled);

            var name = new TextBox { Text = rule.ProcessName, IsReadOnly = rule.IsBuiltIn, VerticalAlignment = VerticalAlignment.Center, PlaceholderText = "进程名" };
            Grid.SetColumn(name, 1);
            name.LostFocus += async (_, _) =>
            {
                var normalized = TargetRuleSettings.NormalizeProcessName(name.Text);
                if (string.IsNullOrWhiteSpace(normalized) || rule.IsBuiltIn) return;
                rule.ProcessName = normalized;
                await SaveTargetRulesAsync();
            };
            row.Children.Add(name);

            var remove = new Button { Content = rule.IsBuiltIn ? "默认" : "删除", IsEnabled = !rule.IsBuiltIn, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(remove, 2);
            remove.Click += async (_, _) =>
            {
                _targetSettings.Rules.Remove(rule);
                await SaveTargetRulesAsync();
            };
            row.Children.Add(remove);
            TargetRulesPanel.Children.Add(row);
        }
        var enabledCount = _targetSettings.Rules.Count(rule => rule.IsEnabled);
        TargetRulesSummary.Text = _targetSettings.TargetOnlyMode
            ? $"仅目标应用模式已开启：{enabledCount} 条启用规则。"
            : "仅目标应用模式已关闭：除 Skill Palette 自身外，所有前台应用都会响应。";
    }

    private void ApplyTargetRules() => _listener.UpdateProcessMatcher(_targetSettings.Matches);

    private async Task SaveTargetRulesAsync()
    {
        _targetSettings.Normalize();
        ApplyTargetRules();
        await _targetRuleStore.SaveAsync(_targetSettings);
        RenderTargetRules();
    }

    private async void FavoriteItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SkillListItem selected })
        {
            return;
        }

        var metadata = GetOrCreateMetadata(selected.Id);
        metadata.IsFavorite = !metadata.IsFavorite;
        await _metadataStore.SaveAsync(_metadata);

        if (FavoriteFilterButton.IsChecked == true && !metadata.IsFavorite)
        {
            Skills.Remove(selected);
            UpdateSummary();
            return;
        }

        selected.FavoriteIcon = metadata.IsFavorite ? Symbol.SolidStar : Symbol.OutlineStar;
        selected.FavoriteAutomationName = metadata.IsFavorite ? $"取消收藏 {selected.Name}" : $"收藏 {selected.Name}";
    }

    private async Task ScanAsync()
    {
        ScanStatus.Severity = InfoBarSeverity.Informational;
        ScanStatus.Title = "正在扫描";
        ScanStatus.Message = "只读取每个 SKILL.md 的有限前缀；不会上传 Skill 内容。";

        try
        {
            var snapshot = await _scanner.ScanAsync();
            _snapshot = snapshot;
            RebuildCategories(snapshot);
            RenderResults();
            ScanStatus.Severity = snapshot.Warnings.Count == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
            ScanStatus.Title = snapshot.Warnings.Count == 0 ? "扫描完成" : "扫描完成，但有可诊断警告";
            ScanStatus.Message = $"已索引 {Skills.Count} 个 Skill；扫描警告 {snapshot.Warnings.Count} 条。";
        }
        catch (Exception ex)
        {
            ScanStatus.Severity = InfoBarSeverity.Error;
            ScanStatus.Title = "扫描失败";
            ScanStatus.Message = ex.Message;
        }
    }

    private SkillMetadata GetOrCreateMetadata(string id)
    {
        if (!_metadata.TryGetValue(id, out var metadata))
        {
            metadata = new SkillMetadata();
            _metadata[id] = metadata;
        }

        return metadata;
    }

    private void RebuildCategories(SkillCatalogSnapshot snapshot)
    {
        var selected = CategoryFilter.SelectedItem as string;
        Categories.Clear();
        Categories.Add("全部");
        foreach (var category in _search.Categories(snapshot))
        {
            Categories.Add(category);
        }

        CategoryFilter.SelectedItem = Categories.Contains(selected ?? string.Empty) ? selected : "全部";
    }

    private void RenderResults()
    {
        if (_snapshot is null || SearchBox is null)
        {
            return;
        }

        var category = CategoryFilter.SelectedItem as string;
        var results = _search.Search(_snapshot, _metadata, new SkillSearchOptions(
            Query: SearchBox.Text,
            Category: category == "全部" ? null : category,
            FavoritesOnly: FavoriteFilterButton.IsChecked == true,
            PrioritizeQuickAccess: false));
        Skills.Clear();
        foreach (var result in results)
        {
            Skills.Add(new SkillListItem
            {
                Id = result.Skill.Id,
                Name = result.Skill.Name,
                Description = result.Skill.Description,
                RelativePath = result.Skill.RelativePath,
                Family = result.Skill.Family,
                FavoriteIcon = result.Metadata.IsFavorite ? Symbol.SolidStar : Symbol.OutlineStar,
                FavoriteAutomationName = result.Metadata.IsFavorite ? $"取消收藏 {result.Skill.Name}" : $"收藏 {result.Skill.Name}"
            });
        }

        UpdateSummary();
        EmptyState.Text = Skills.Count == 0 ? "没有匹配结果。可尝试名称、描述或个人标签。" : string.Empty;
    }

    private void UpdateSummary()
    {
        if (_snapshot is not null)
        {
            ScanSummary.Text = $"显示 {Skills.Count} / {_snapshot.Skills.Count} 个 Skill";
        }
    }
}

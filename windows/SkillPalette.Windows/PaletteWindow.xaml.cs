using System.Collections.ObjectModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SkillPalette.Core.Skills;
using SkillPalette_Windows.Native;
using Windows.Graphics;

namespace SkillPalette_Windows;

public sealed partial class PaletteWindow : Window
{
    private readonly SkillCatalogScanner _scanner = new();
    private readonly SkillSearchEngine _search = new();
    private readonly SkillMetadataStore _metadataStore = new();
    private SkillCatalogSnapshot? _snapshot;
    private Dictionary<string, SkillMetadata> _metadata = new(StringComparer.OrdinalIgnoreCase);
    private ForegroundTarget _target;
    private int _openSession;
    private char _triggerCharacter = '#';
    private string _invocationPrefix = "@";

    internal ObservableCollection<PaletteItem> Items { get; } = [];
    internal event EventHandler<PaletteActionEventArgs>? ActionRequested;
    internal ForegroundTarget Target => _target;
    internal char TriggerCharacter => _triggerCharacter;

    public PaletteWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new SizeInt32(680, 520));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }
        PaletteWindowInterop.HideFromTaskbar(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }

    internal void Open(ForegroundTarget target, char triggerCharacter, string invocationPrefix)
    {
        var session = ++_openSession;
        _target = target;
        _triggerCharacter = triggerCharacter;
        _invocationPrefix = invocationPrefix;
        AppWindow.Move(PaletteWindowInterop.PlaceAboveTarget(target.WindowHandle, 680, 520));
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        PaletteWindowInterop.SetTopmost(handle, true);
        Activate();
        _ = InitializeAndActivateAsync(handle, target, session);
    }

    public void ClosePalette()
    {
        _openSession++;
        PaletteWindowInterop.SetTopmost(WinRT.Interop.WindowNative.GetWindowHandle(this), false);
        AppWindow.Hide();
    }

    private async Task InitializeAndActivateAsync(IntPtr handle, ForegroundTarget target, int session)
    {
        await LoadAsync();
        foreach (var delay in new[] { 0, 40, 140 })
        {
            if (delay > 0) await Task.Delay(delay);
            if (session != _openSession) return;
            PaletteWindowInterop.ActivateWindow(handle, target.WindowHandle);
            SearchBox.Focus(FocusState.Programmatic);
            if (PaletteWindowInterop.IsForeground(handle)) return;
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            _metadata = await _metadataStore.LoadAsync();
            _snapshot = await _scanner.ScanAsync();
            CategoryBox.Items.Clear();
            CategoryBox.Items.Add("全部");
            foreach (var category in _search.Categories(_snapshot)) CategoryBox.Items.Add(category);
            CategoryBox.SelectedIndex = 0;
            Render();
        }
        catch
        {
            CategoryBox.Items.Clear();
            CategoryBox.Items.Add("全部");
            CategoryBox.SelectedIndex = 0;
            CountText.Text = "无法加载技能";
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Render();
    private void Filter_Changed(object sender, RoutedEventArgs e) => Render();

    private void Render()
    {
        if (_snapshot is null) return;
        var category = CategoryBox.SelectedItem as string;
        var results = _search.Search(_snapshot, _metadata, new SkillSearchOptions(SearchBox.Text, category == "全部" ? null : category, FavoritesButton.IsChecked == true));
        Items.Clear();
        foreach (var result in results) Items.Add(new PaletteItem(result.Skill, result.Metadata.IsFavorite));
        CountText.Text = $"{Items.Count} 个技能";
        SelectionText.Text = Items.Count == 0 ? string.Empty : $"{Math.Max(ResultList.SelectedIndex + 1, 1)} / {Items.Count}";
        if (Items.Count > 0) ResultList.SelectedIndex = 0;
    }

    private async void Star_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PaletteItem item }) return;
        if (!_metadata.TryGetValue(item.Id, out var metadata)) _metadata[item.Id] = metadata = new SkillMetadata();
        metadata.IsFavorite = !metadata.IsFavorite;
        await _metadataStore.SaveAsync(_metadata);
        Render();
    }

    private void ResultList_ItemClick(object sender, ItemClickEventArgs e) => Submit((PaletteItem)e.ClickedItem);
    private void Root_KeyDown(object sender, KeyRoutedEventArgs e) { if (HandlePaletteKey(e.Key)) e.Handled = true; }
    private void Root_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is Windows.System.VirtualKey.Down or Windows.System.VirtualKey.Up)
        {
            HandlePaletteKey(e.Key);
            e.Handled = true;
        }
    }
    private void PaletteControl_PreviewKeyDown(object sender, KeyRoutedEventArgs e) { if (HandlePaletteKey(e.Key)) e.Handled = true; }

    private bool HandlePaletteKey(Windows.System.VirtualKey key)
    {
        switch (key)
        {
            case Windows.System.VirtualKey.Escape:
                ActionRequested?.Invoke(this, new PaletteActionEventArgs(_target, PaletteAction.Cancel, triggerCharacter: _triggerCharacter)); return true;
            case Windows.System.VirtualKey.Enter when ResultList.SelectedItem is PaletteItem item:
                Submit(item); return true;
            case Windows.System.VirtualKey.Down:
                MoveSelection(1); return true;
            case Windows.System.VirtualKey.Up:
                MoveSelection(-1); return true;
            default: return false;
        }
    }

    private void MoveSelection(int offset)
    {
        if (Items.Count == 0) return;
        ResultList.SelectedIndex = Math.Clamp(ResultList.SelectedIndex < 0 ? 0 : ResultList.SelectedIndex + offset, 0, Items.Count - 1);
        ResultList.ScrollIntoView(ResultList.SelectedItem);
        SelectionText.Text = $"{ResultList.SelectedIndex + 1} / {Items.Count}";
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void Submit(PaletteItem item) => ActionRequested?.Invoke(this, new PaletteActionEventArgs(_target, PaletteAction.Paste, $"{_invocationPrefix}{item.Name} ", _triggerCharacter));
}

internal sealed class PaletteItem(SkillDefinition skill, bool favorite)
{
    public string Id { get; } = skill.Id;
    public string Name { get; } = skill.Name;
    public string Detail { get; } = string.IsNullOrEmpty(skill.Description) ? skill.Family : $"{skill.Description} · {skill.Family}";
    public Symbol Star { get; } = favorite ? Symbol.SolidStar : Symbol.OutlineStar;
}

internal enum PaletteAction { Cancel, Paste }
internal sealed class PaletteActionEventArgs(ForegroundTarget target, PaletteAction action, string? invocation = null, char triggerCharacter = '#') : EventArgs
{ public ForegroundTarget Target { get; } = target; public PaletteAction Action { get; } = action; public string? Invocation { get; } = invocation; public char TriggerCharacter { get; } = triggerCharacter; }

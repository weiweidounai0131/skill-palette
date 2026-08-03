using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SkillPalette_Windows.ViewModels;

/// <summary>Mutable UI projection; the core catalog remains immutable.</summary>
public sealed class SkillListItem : INotifyPropertyChanged
{
    private Symbol _favoriteIcon = Symbol.OutlineStar;
    private string _favoriteAutomationName = string.Empty;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public Symbol FavoriteIcon
    {
        get => _favoriteIcon;
        set
        {
            if (_favoriteIcon == value) return;
            _favoriteIcon = value;
            OnPropertyChanged();
        }
    }

    public string FavoriteAutomationName
    {
        get => _favoriteAutomationName;
        set
        {
            if (_favoriteAutomationName == value) return;
            _favoriteAutomationName = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

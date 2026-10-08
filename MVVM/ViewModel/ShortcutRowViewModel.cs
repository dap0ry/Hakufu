using System.Collections.ObjectModel;
using Avalonia.Input;
using Hakufu.I18n;
using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

/// <summary>Una fila de Ajustes → Atajos de teclado: la acción y sus (hasta 2) teclas.</summary>
public class ShortcutRowViewModel : BaseViewModel
{
    public ShortcutAction Action { get; }
    public string Label => Action.Label;
    public ObservableCollection<ShortcutSlotViewModel> Slots { get; } = [];

    public ShortcutRowViewModel(ShortcutAction action, IReadOnlyList<KeyGesture> gestures, Action<ShortcutSlotViewModel> onClick)
    {
        Action = action;
        for (var i = 0; i < ShortcutService.MaxKeysPerAction; i++)
            Slots.Add(new ShortcutSlotViewModel(this, i < gestures.Count ? gestures[i] : null, onClick));
    }

    public IEnumerable<KeyGesture> Gestures => Slots.Select(s => s.Gesture).OfType<KeyGesture>();
}

/// <summary>Un hueco de tecla: muestra la tecla, "Añadir" o "Pulsa una tecla…" mientras escucha.</summary>
public class ShortcutSlotViewModel : BaseViewModel
{
    private KeyGesture? _gesture;
    private bool _isCapturing;

    public ShortcutSlotViewModel(ShortcutRowViewModel row, KeyGesture? gesture, Action<ShortcutSlotViewModel> onClick)
    {
        Row      = row;
        _gesture = gesture;
        ClickCommand = new RelayCommand(() => onClick(this));
    }

    public ShortcutRowViewModel Row { get; }

    public KeyGesture? Gesture
    {
        get => _gesture;
        set
        {
            if (!SetProperty(ref _gesture, value)) return;
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(IsAssigned));
        }
    }

    public bool IsCapturing
    {
        get => _isCapturing;
        set { if (SetProperty(ref _isCapturing, value)) OnPropertyChanged(nameof(Text)); }
    }

    public bool   IsAssigned => Gesture is not null;
    public string Text => IsCapturing ? L.Get("shortcuts.press_key")
                        : Gesture is { } g ? ShortcutService.Display(g)
                        : L.Get("shortcuts.add");

    public RelayCommand ClickCommand { get; }
}

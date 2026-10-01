using System.Reflection;
using Avalonia.Threading;

namespace Hakufu.MVVM.ViewModel;

/// <summary>
/// Sustituto de CommandManager.RequerySuggested de WPF: avisa a todos los
/// comandos de que reevalúen CanExecute. Lo disparan BaseViewModel (al cambiar
/// cualquier propiedad) y MainWindow (tras cada clic o tecla).
///
/// Los suscriptores se guardan con referencia débil — igual que en WPF — porque
/// muchos comandos se crean en cada get y los botones se suscriben a ellos: con
/// referencias fuertes, cada vista visitada quedaría viva para siempre.
/// </summary>
public static class CommandRequery
{
    private sealed record Subscriber(WeakReference? Target, MethodInfo Method);

    private static readonly List<Subscriber> Subscribers = [];
    private static readonly object Gate = new();

    public static event EventHandler? RequerySuggested
    {
        add
        {
            if (value is null) return;
            lock (Gate)
                Subscribers.Add(new(value.Target is null ? null : new WeakReference(value.Target), value.Method));
        }
        remove
        {
            if (value is null) return;
            lock (Gate)
                Subscribers.RemoveAll(s => s.Method == value.Method &&
                                           (s.Target?.Target ?? null) == value.Target);
        }
    }

    private static int _pending;

    /// <summary>
    /// Agrupa muchas peticiones seguidas (p. ej. varias propiedades cambiando
    /// a la vez) en una sola reevaluación en el hilo de UI.
    /// </summary>
    public static void RequestInvalidate()
    {
        if (Interlocked.Exchange(ref _pending, 1) == 1) return;
        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _pending, 0);
            Invalidate();
        }, DispatcherPriority.Background);
    }

    public static void Invalidate()
    {
        Subscriber[] snapshot;
        lock (Gate)
        {
            Subscribers.RemoveAll(s => s.Target is not null && !s.Target.IsAlive);
            snapshot = [.. Subscribers];
        }

        foreach (var s in snapshot)
        {
            var target = s.Target?.Target;
            if (s.Target is not null && target is null) continue;
            s.Method.Invoke(target, [null, EventArgs.Empty]);
        }
    }
}

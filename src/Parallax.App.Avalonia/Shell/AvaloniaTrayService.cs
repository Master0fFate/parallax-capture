using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Parallax.Core.Platform;

namespace Parallax.App.Avalonia.Shell;

public sealed class AvaloniaTrayService : ITrayService
{
    private readonly TrayIcon? _trayIcon;
    private bool _disposed;

    public AvaloniaTrayService(bool isAvailable = true)
    {
        IsAvailable = isAvailable;
        if (!isAvailable)
        {
            return;
        }

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Parallax Capture",
            Icon = TryLoadIcon()
        };
    }

    public bool IsAvailable { get; }

    public void SetMenu(IReadOnlyList<TrayMenuItem> items)
    {
        if (_trayIcon == null)
        {
            return;
        }

        var menu = new NativeMenu();
        foreach (var item in items.Where(item => item.IsVisible))
        {
            menu.Items.Add(new NativeMenuItem
            {
                Header = item.Label,
                IsEnabled = item.IsEnabled
            });
        }

        _trayIcon.Menu = menu;
    }

    public void SetToolTip(string text)
    {
        if (_trayIcon != null)
        {
            _trayIcon.ToolTipText = text;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_trayIcon is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _disposed = true;
    }

    private static WindowIcon? TryLoadIcon()
    {
        try
        {
            return new WindowIcon("avares://Parallax.App.Avalonia/Assets/icon.ico");
        }
        catch (Exception)
        {
            return null;
        }
    }
}

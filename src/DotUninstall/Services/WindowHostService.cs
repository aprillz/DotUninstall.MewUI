using Aprillz.MewUI;

namespace DotUninstall.Services;

public class WindowHostService
{
    private Window? _owner;
    private IDispatcher? _dispatcher;

    public event Action? RequerySuggested;

    public void SetOwner(Window window)
    {
        _owner = window;
        _dispatcher = Application.Current.Dispatcher;
    }

    public void BeginInvoke(Action action)
    {
        if (_dispatcher is not null)
        {
            _dispatcher.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    public void InvalidateCommands() => RequerySuggested?.Invoke();
}

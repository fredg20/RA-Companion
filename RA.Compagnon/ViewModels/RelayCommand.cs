using System.Windows.Input;

namespace RA.Compagnon.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action _executer;
    private readonly Func<bool>? _peutExecuter;

    public RelayCommand(Action executer, Func<bool>? peutExecuter = null)
    {
        _executer = executer;
        _peutExecuter = peutExecuter;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _peutExecuter?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _executer();
        }
    }

    public void NotifierPeutExecuterChange()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

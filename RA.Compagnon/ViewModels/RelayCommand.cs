using System.Windows.Input;

/*
 * Implémente une commande WPF simple basée sur des délégués pour relier
 * facilement l'interface aux actions des ViewModels.
 */
namespace RA.Compagnon.ViewModels;

/*
 * Expose une commande exécutable avec une condition optionnelle
 * d'activation pour le binding des boutons et actions UI.
 */
public sealed class RelayCommand : ICommand
{
    private readonly Action _executer;
    private readonly Func<bool>? _peutExecuter;

    /*
     * Initialise la commande avec son action principale et sa condition
     * d'exécution éventuelle.
     */
    public RelayCommand(Action executer, Func<bool>? peutExecuter = null)
    {
        _executer = executer;
        _peutExecuter = peutExecuter;
    }

    public event EventHandler? CanExecuteChanged;

    /*
     * Indique si la commande peut être exécutée dans son état actuel.
     */
    public bool CanExecute(object? parameter)
    {
        return _peutExecuter?.Invoke() ?? true;
    }

    /*
     * Exécute l'action associée lorsque la commande est active.
     */
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _executer();
        }
    }

    /*
     * Notifie l'interface qu'elle doit réévaluer l'état d'activation
     * de la commande.
     */
    public void NotifierPeutExecuterChange()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
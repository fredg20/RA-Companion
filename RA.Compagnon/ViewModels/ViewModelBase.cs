using System.ComponentModel;
using System.Runtime.CompilerServices;

/*
 * Définit la base commune des ViewModels avec la notification de changement
 * de propriété nécessaire au binding WPF.
 */
namespace RA.Compagnon.ViewModels;

/*
 * Fournit le socle minimal permettant aux ViewModels de notifier l'interface
 * lorsqu'une propriété change de valeur.
 */
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /*
     * Met à jour un champ si sa valeur change puis notifie la propriété liée.
     */
    protected bool SetProperty<T>(
        ref T champ,
        T valeur,
        [CallerMemberName] string? nomPropriete = null
    )
    {
        if (EqualityComparer<T>.Default.Equals(champ, valeur))
        {
            return false;
        }

        champ = valeur;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nomPropriete));
        return true;
    }
}

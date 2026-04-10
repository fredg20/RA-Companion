using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RA.Compagnon.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

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

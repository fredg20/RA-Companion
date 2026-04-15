/*
 * Décrit une entrée simple de la bibliothèque visible dans le module dédié
 * de la fenêtre principale.
 */
namespace RA.Compagnon.ViewModels;

/*
 * Transporte le titre et le sous-titre déjà prêts à afficher pour un jeu de
 * la bibliothèque récente.
 */
public sealed class BibliothequeJeuViewModel : ViewModelBase
{
    private string _titre = string.Empty;
    private string _sousTitre = string.Empty;

    public string Titre
    {
        get => _titre;
        set => SetProperty(ref _titre, value);
    }

    public string SousTitre
    {
        get => _sousTitre;
        set => SetProperty(ref _sousTitre, value);
    }
}
using System.Windows.Media;

namespace RA.Compagnon.ViewModels;

public sealed class AccountSummaryViewModel : ViewModelBase
{
    private string _libelleBouton = "Connexion";
    private string _etatNotice = string.Empty;
    private string _sousEtatNotice = string.Empty;
    private string _toolTipNotice = string.Empty;
    private bool _noticeVisible;
    private bool _sousEtatVisible;
    private Brush _fondNotice = Brushes.Transparent;
    private Brush _bordureNotice = Brushes.Transparent;

    public string LibelleBouton
    {
        get => _libelleBouton;
        set => SetProperty(ref _libelleBouton, value);
    }

    public string EtatNotice
    {
        get => _etatNotice;
        set => SetProperty(ref _etatNotice, value);
    }

    public string SousEtatNotice
    {
        get => _sousEtatNotice;
        set => SetProperty(ref _sousEtatNotice, value);
    }

    public string ToolTipNotice
    {
        get => _toolTipNotice;
        set => SetProperty(ref _toolTipNotice, value);
    }

    public bool NoticeVisible
    {
        get => _noticeVisible;
        set => SetProperty(ref _noticeVisible, value);
    }

    public bool SousEtatVisible
    {
        get => _sousEtatVisible;
        set => SetProperty(ref _sousEtatVisible, value);
    }

    public Brush FondNotice
    {
        get => _fondNotice;
        set => SetProperty(ref _fondNotice, value);
    }

    public Brush BordureNotice
    {
        get => _bordureNotice;
        set => SetProperty(ref _bordureNotice, value);
    }
}

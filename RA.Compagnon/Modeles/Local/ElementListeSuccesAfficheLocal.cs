namespace RA.Compagnon.Modeles.Local;

public sealed class ElementListeSuccesAfficheLocal
{
    public int IdentifiantSucces { get; set; }

    public string Titre { get; set; } = string.Empty;

    public string CheminImageBadge { get; set; } = string.Empty;

    public int AchievementId
    {
        get => IdentifiantSucces;
        set => IdentifiantSucces = value;
    }

    public string Title
    {
        get => Titre;
        set => Titre = value;
    }
}

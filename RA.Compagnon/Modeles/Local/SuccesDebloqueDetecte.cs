namespace RA.Compagnon.Modeles.Local;

public sealed class SuccesDebloqueDetecte
{
    public int IdentifiantJeu { get; init; }

    public string TitreJeu { get; init; } = string.Empty;

    public int IdentifiantSucces { get; init; }

    public string TitreSucces { get; init; } = string.Empty;

    public int Points { get; init; }

    public bool Hardcore { get; init; }

    public string DateObtention { get; init; } = string.Empty;
}

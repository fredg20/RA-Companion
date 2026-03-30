namespace RA.Compagnon.Modeles.Debug;

public sealed class ScenarioTestSuccesDebug
{
    public string NomEmulateur { get; init; } = string.Empty;

    public string SourceSimulee { get; init; } = string.Empty;

    public string TypeSourceLocale { get; init; } = string.Empty;

    public string CheminSourceLocale { get; init; } = string.Empty;

    public int IdentifiantJeu { get; init; }

    public string TitreJeu { get; init; } = string.Empty;

    public int IdentifiantSucces { get; init; }

    public string TitreSucces { get; init; } = string.Empty;

    public int Points { get; init; }

    public bool Hardcore { get; init; }

    public string DateObtention { get; init; } = string.Empty;

    public ModeDeclenchementTestSuccesDebug ModeDeclenchement { get; init; }
}

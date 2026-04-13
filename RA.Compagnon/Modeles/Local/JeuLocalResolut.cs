namespace RA.Compagnon.Modeles.Local;

public sealed class JeuLocalResolut
{
    public int IdentifiantJeu { get; init; }

    public int IdentifiantConsole { get; init; }

    public string TitreLocal { get; init; } = string.Empty;

    public string TitreRetroAchievements { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public double ScoreConfiance { get; init; }
}

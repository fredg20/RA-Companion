namespace RA.Compagnon.Modeles.Local;

/// <summary>
/// Représente le meilleur rapprochement connu entre un titre local détecté et un jeu RA.
/// </summary>
public sealed class JeuLocalResolut
{
    public int IdentifiantJeu { get; init; }

    public int IdentifiantConsole { get; init; }

    public string TitreLocal { get; init; } = string.Empty;

    public string TitreRetroAchievements { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public double ScoreConfiance { get; init; }
}

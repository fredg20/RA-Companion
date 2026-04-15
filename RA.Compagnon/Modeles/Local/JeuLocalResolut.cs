/*
 * Représente le résultat d'une résolution locale entre un titre détecté et
 * un jeu RetroAchievements.
 */
namespace RA.Compagnon.Modeles.Local;

/*
 * Transporte l'identité du jeu retenu, sa source de résolution et le score
 * de confiance associé.
 */
public sealed class JeuLocalResolut
{
    public int IdentifiantJeu { get; init; }

    public int IdentifiantConsole { get; init; }

    public string TitreLocal { get; init; } = string.Empty;

    public string TitreRetroAchievements { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public double ScoreConfiance { get; init; }
}
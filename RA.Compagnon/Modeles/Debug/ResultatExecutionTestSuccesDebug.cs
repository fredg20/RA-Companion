/*
 * Représente le résultat d'exécution concret d'un test de succès simulé.
 */
namespace RA.Compagnon.Modeles.Debug;

/*
 * Indique si l'injection d'un scénario de test a réussi et sur quel chemin.
 */
public sealed class ResultatExecutionTestSuccesDebug
{
    public bool EstReussi { get; init; }

    public string Motif { get; init; } = string.Empty;

    public string Chemin { get; init; } = string.Empty;
}

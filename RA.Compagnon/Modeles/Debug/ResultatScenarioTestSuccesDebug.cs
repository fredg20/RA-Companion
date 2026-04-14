/*
 * Représente le résultat de construction d'un scénario de test de succès.
 */
namespace RA.Compagnon.Modeles.Debug;

/*
 * Indique si un scénario de test est valide et, le cas échéant, le scénario
 * construit.
 */
public sealed class ResultatScenarioTestSuccesDebug
{
    public bool EstValide { get; init; }

    public string Motif { get; init; } = string.Empty;

    public ScenarioTestSuccesDebug? Scenario { get; init; }
}

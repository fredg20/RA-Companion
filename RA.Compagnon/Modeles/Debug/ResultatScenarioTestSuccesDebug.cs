namespace RA.Compagnon.Modeles.Debug;

public sealed class ResultatScenarioTestSuccesDebug
{
    public bool EstValide { get; init; }

    public string Motif { get; init; } = string.Empty;

    public ScenarioTestSuccesDebug? Scenario { get; init; }
}

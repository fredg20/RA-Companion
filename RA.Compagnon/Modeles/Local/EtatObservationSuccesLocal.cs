namespace RA.Compagnon.Modeles.Local;

/// <summary>
/// Représente l'état observé d'un succès pour détecter un nouveau déblocage.
/// </summary>
public sealed class EtatObservationSuccesLocal
{
    public int IdentifiantSucces { get; init; }

    public string DateObtention { get; init; } = string.Empty;

    public string DateObtentionHardcore { get; init; } = string.Empty;
}

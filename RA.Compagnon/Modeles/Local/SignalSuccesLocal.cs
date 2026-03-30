namespace RA.Compagnon.Modeles.Local;

/// <summary>
/// Représente un signal local indiquant qu'un état de succès a potentiellement changé.
/// </summary>
public sealed class SignalSuccesLocal
{
    public string NomEmulateur { get; init; } = string.Empty;

    public string TypeSource { get; init; } = string.Empty;

    public string Chemin { get; init; } = string.Empty;

    public DateTimeOffset HorodatageUtc { get; init; } = DateTimeOffset.UtcNow;
}

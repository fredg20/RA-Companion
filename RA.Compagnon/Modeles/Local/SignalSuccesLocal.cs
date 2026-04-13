namespace RA.Compagnon.Modeles.Local;

public sealed class SignalSuccesLocal
{
    public string NomEmulateur { get; init; } = string.Empty;

    public string TypeSource { get; init; } = string.Empty;

    public string Chemin { get; init; } = string.Empty;

    public DateTimeOffset HorodatageUtc { get; init; } = DateTimeOffset.UtcNow;
}

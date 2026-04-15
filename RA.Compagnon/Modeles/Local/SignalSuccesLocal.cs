/*
 * Représente un signal local émis par la surveillance d'un émulateur lorsqu'un
 * changement pertinent est détecté dans une source de succès.
 */
namespace RA.Compagnon.Modeles.Local;

/*
 * Transporte l'origine locale d'un signal de succès et son horodatage.
 */
public sealed class SignalSuccesLocal
{
    public string NomEmulateur { get; init; } = string.Empty;

    public string TypeSource { get; init; } = string.Empty;

    public string Chemin { get; init; } = string.Empty;

    public DateTimeOffset HorodatageUtc { get; init; } = DateTimeOffset.UtcNow;
}
/*
 * Représente l'état observé d'un succès local afin de détecter les nouveaux
 * déblocages entre deux sondes.
 */
namespace RA.Compagnon.Modeles.Local;

/*
 * Transporte les dates d'obtention connues d'un succès, en normal et en
 * hardcore.
 */
public sealed class EtatObservationSuccesLocal
{
    public int IdentifiantSucces { get; init; }

    public string DateObtention { get; init; } = string.Empty;

    public string DateObtentionHardcore { get; init; } = string.Empty;
}

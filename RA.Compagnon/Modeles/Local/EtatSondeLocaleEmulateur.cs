namespace RA.Compagnon.Modeles.Local;

/// <summary>
/// Représente l'état courant détecté par la sonde locale d'émulateurs.
/// </summary>
public sealed class EtatSondeLocaleEmulateur
{
    public bool EmulateurDetecte { get; init; }

    public string NomEmulateur { get; init; } = string.Empty;

    public string NomProcessus { get; init; } = string.Empty;

    public string CheminExecutable { get; init; } = string.Empty;

    public string TitreFenetre { get; init; } = string.Empty;

    public string TitreJeuProbable { get; init; } = string.Empty;

    public int IdentifiantJeuProbable { get; init; }

    public string InformationsDiagnostic { get; init; } = string.Empty;

    public string Signature { get; init; } = string.Empty;

    public DateTimeOffset HorodatageUtc { get; init; } = DateTimeOffset.UtcNow;
}

namespace RA.Compagnon.Modeles.Presentation;

/// <summary>
/// Représente le résultat de la sonde Rich Presence pour un compte utilisateur.
/// </summary>
public sealed class EtatRichPresence
{
    public string SourceRichPresence { get; init; } = string.Empty;

    public string MessageRichPresence { get; init; } = string.Empty;

    public string StatutSite { get; init; } = string.Empty;

    public string StatutAffiche { get; init; } = string.Empty;

    public string SousStatutAffiche { get; init; } = string.Empty;

    public int IdentifiantDernierJeu { get; init; }

    public string DatePresenceBrute { get; init; } = string.Empty;

    public DateTimeOffset? DatePresenceUtc { get; init; }

    public bool PresenceDateValide { get; init; }

    public bool PresenceManifestementAncienne { get; init; }

    public bool EstEnJeu { get; init; }
}

namespace RA.Compagnon.Modeles.Local;

/// <summary>
/// Représente les informations de hachage calculées pour un fichier de jeu local.
/// </summary>
public sealed class EmpreinteJeuLocal
{
    /// <summary>
    /// Chemin du fichier analysé.
    /// </summary>
    public string CheminFichier { get; set; } = string.Empty;

    /// <summary>
    /// Empreinte MD5 du fichier en hexadécimal.
    /// </summary>
    public string EmpreinteMd5 { get; set; } = string.Empty;

    /// <summary>
    /// Empreinte SHA-1 du fichier en hexadécimal.
    /// </summary>
    public string EmpreinteSha1 { get; set; } = string.Empty;

    /// <summary>
    /// Taille du fichier en octets.
    /// </summary>
    public long TailleOctets { get; set; }
}

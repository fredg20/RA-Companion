/*
 * Représente les empreintes calculées pour un fichier de jeu local.
 */
namespace RA.Compagnon.Modeles.Local;

/*
 * Transporte les chemins, hachages et la taille utilisés pour identifier un
 * jeu local de manière fiable.
 */
public sealed class EmpreinteJeuLocal
{
    public string CheminFichier { get; set; } = string.Empty;

    public string EmpreinteMd5 { get; set; } = string.Empty;

    public string EmpreinteSha1 { get; set; } = string.Empty;

    public long TailleOctets { get; set; }
}
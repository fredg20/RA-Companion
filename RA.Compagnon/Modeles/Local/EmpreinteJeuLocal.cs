namespace RA.Compagnon.Modeles.Local;

public sealed class EmpreinteJeuLocal
{
    public string CheminFichier { get; set; } = string.Empty;

    public string EmpreinteMd5 { get; set; } = string.Empty;

    public string EmpreinteSha1 { get; set; } = string.Empty;

    public long TailleOctets { get; set; }
}

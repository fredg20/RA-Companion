using System.IO;
using System.Security.Cryptography;
using RA.Compagnon.Modeles.Local;

/*
 * Calcule et met en cache les empreintes locales des fichiers de jeu afin
 * de faciliter leur identification.
 */
namespace RA.Compagnon.Services;

/*
 * Produit les empreintes MD5 et SHA1 d'un jeu local avec un cache mémoire
 * indexé sur le chemin et les métadonnées du fichier.
 */
public sealed class ServiceHachageJeuLocal
{
    private readonly Dictionary<string, EmpreinteJeuLocalCachee> _cacheEmpreintes = new(
        StringComparer.OrdinalIgnoreCase
    );

    /*
     * Calcule l'empreinte d'un fichier de jeu local ou retourne une valeur
     * déjà mémorisée si le fichier n'a pas changé.
     */
    public async Task<EmpreinteJeuLocal?> CalculerEmpreinteAsync(
        string cheminFichier,
        CancellationToken jetonAnnulation = default
    )
    {
        if (string.IsNullOrWhiteSpace(cheminFichier))
        {
            return null;
        }

        if (!Path.IsPathRooted(cheminFichier) || !File.Exists(cheminFichier))
        {
            return null;
        }

        FileInfo informationsFichier = new(cheminFichier);
        string cleCache =
            $"{informationsFichier.FullName}|{informationsFichier.Length}|{informationsFichier.LastWriteTimeUtc.Ticks}";

        if (_cacheEmpreintes.TryGetValue(cleCache, out EmpreinteJeuLocalCachee? empreinteCachee))
        {
            return empreinteCachee.Empreinte;
        }

        await using FileStream fluxLectureMd5 = new(
            informationsFichier.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 1024 * 128,
            useAsync: true
        );
        await using FileStream fluxLectureSha1 = new(
            informationsFichier.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 1024 * 128,
            useAsync: true
        );

        using MD5 md5 = MD5.Create();
        using SHA1 sha1 = SHA1.Create();
        byte[] empreinteMd5 = await md5.ComputeHashAsync(fluxLectureMd5, jetonAnnulation);
        byte[] empreinteSha1 = await sha1.ComputeHashAsync(fluxLectureSha1, jetonAnnulation);

        EmpreinteJeuLocal resultat = new()
        {
            CheminFichier = informationsFichier.FullName,
            EmpreinteMd5 = Convert.ToHexString(empreinteMd5),
            EmpreinteSha1 = Convert.ToHexString(empreinteSha1),
            TailleOctets = informationsFichier.Length,
        };

        _cacheEmpreintes[cleCache] = new EmpreinteJeuLocalCachee(resultat);
        return resultat;
    }

    /*
     * Représente une empreinte mise en cache pour un fichier local donné.
     */
    private sealed class EmpreinteJeuLocalCachee(EmpreinteJeuLocal empreinte)
    {
        public EmpreinteJeuLocal Empreinte { get; } = empreinte;
    }
}

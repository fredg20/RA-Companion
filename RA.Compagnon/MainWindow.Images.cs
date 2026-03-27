using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RA.Compagnon;

public partial class MainWindow
{
    /// <summary>
    /// Télécharge une image distante puis la garde en cache mémoire pour les prochains affichages.
    /// </summary>
    private async Task<ImageSource?> ChargerImageDistanteAsync(string urlImage)
    {
        if (string.IsNullOrWhiteSpace(urlImage))
        {
            return null;
        }

        if (_cacheImagesDistantes.TryGetValue(urlImage, out ImageSource? imageCachee))
        {
            return imageCachee;
        }

        string cheminCache = ObtenirCheminCacheImageDistante(urlImage);

        if (File.Exists(cheminCache))
        {
            ImageSource? imageDepuisDisque = await ChargerImageDepuisFichierAsync(cheminCache);

            if (imageDepuisDisque is not null)
            {
                _cacheImagesDistantes[urlImage] = imageDepuisDisque;
                return imageDepuisDisque;
            }
        }

        if (_chargementsImagesDistantesEnCours.TryGetValue(urlImage, out Task<ImageSource?>? tache))
        {
            return await tache;
        }

        Task<ImageSource?> nouvelleTache = TelechargerEtMettreEnCacheImageDistanteAsync(
            urlImage,
            cheminCache
        );
        _chargementsImagesDistantesEnCours[urlImage] = nouvelleTache;

        try
        {
            ImageSource? image = await nouvelleTache;

            if (image is not null)
            {
                _cacheImagesDistantes[urlImage] = image;
            }

            return image;
        }
        finally
        {
            _chargementsImagesDistantesEnCours.Remove(urlImage);
        }
    }

    /// <summary>
    /// Télécharge réellement une image distante.
    /// </summary>
    private static async Task<ImageSource?> TelechargerEtMettreEnCacheImageDistanteAsync(
        string urlImage,
        string cheminCache
    )
    {
        using HttpResponseMessage reponse = await HttpClientImages.GetAsync(urlImage);
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxImage = await reponse.Content.ReadAsStreamAsync();
        MemoryStream memoire = new();
        await fluxImage.CopyToAsync(memoire);
        byte[] contenu = memoire.ToArray();

        Directory.CreateDirectory(Path.GetDirectoryName(cheminCache)!);
        await File.WriteAllBytesAsync(cheminCache, contenu);
        return ChargerImageDepuisOctets(contenu);
    }

    /// <summary>
    /// Charge une image depuis un fichier de cache local.
    /// </summary>
    private static async Task<ImageSource?> ChargerImageDepuisFichierAsync(string cheminCache)
    {
        try
        {
            byte[] contenu = await File.ReadAllBytesAsync(cheminCache);
            return ChargerImageDepuisOctets(contenu);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Construit une image WPF figée à partir d'octets.
    /// </summary>
    private static ImageSource? ChargerImageDepuisOctets(byte[] contenu)
    {
        if (contenu.Length == 0)
        {
            return null;
        }

        using MemoryStream memoire = new(contenu);
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = memoire;
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <summary>
    /// Détermine le chemin de cache disque d'une image distante.
    /// </summary>
    private static string ObtenirCheminCacheImageDistante(string urlImage)
    {
        byte[] empreinte = SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(urlImage));
        string nomFichier = Convert.ToHexString(empreinte) + ".bin";
        string dossierCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RA-Compagnon",
            "image_cache"
        );
        return Path.Combine(dossierCache, nomFichier);
    }
}


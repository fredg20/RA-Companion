using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;

/*
 * Fournit un petit serveur HTTP local pour exposer les fichiers OBS sans
 * dépendre de la lecture directe de fichiers locaux par OBS.
 */
namespace RA.Compagnon.Services;

/*
 * Sert index.html, state.json et les fichiers texte depuis le dossier OBS
 * sur une adresse locale stable de type http://127.0.0.1:28718/.
 */
public sealed class ServiceServeurObsLocal : IDisposable
{
    private const int PortPreferentiel = 28718;
    private const int NombrePortsRepli = 10;
    private readonly CancellationTokenSource _annulation = new();
    private TcpListener? _serveur;
    private Task? _tacheEcoute;

    public int Port { get; private set; } = PortPreferentiel;

    public string UrlRacine => $"http://127.0.0.1:{Port}/";

    public string UrlOverlay => $"{UrlRacine}index.html";

    /*
     * Démarre l'écoute locale si elle n'est pas déjà active.
     */
    public void Demarrer()
    {
        if (_serveur is not null)
        {
            return;
        }

        for (int port = PortPreferentiel; port < PortPreferentiel + NombrePortsRepli; port++)
        {
            try
            {
                TcpListener serveur = new(IPAddress.Loopback, port);
                serveur.Start();
                _serveur = serveur;
                Port = port;
                _tacheEcoute = Task.Run(() => EcouterAsync(_annulation.Token));
                return;
            }
            catch (SocketException) { }
        }
    }

    /*
     * Arrête le serveur local et libère le port utilisé.
     */
    public void Dispose()
    {
        _annulation.Cancel();
        _serveur?.Stop();
        _annulation.Dispose();
    }

    /*
     * Accepte les connexions entrantes une par une sans bloquer l'interface.
     */
    private async Task EcouterAsync(CancellationToken jetonAnnulation)
    {
        if (_serveur is null)
        {
            return;
        }

        while (!jetonAnnulation.IsCancellationRequested)
        {
            try
            {
                TcpClient client = await _serveur.AcceptTcpClientAsync(jetonAnnulation);
                _ = Task.Run(
                    async () =>
                    {
                        using TcpClient clientConnecte = client;

                        try
                        {
                            await RepondreAsync(clientConnecte, jetonAnnulation);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        catch (Exception exception)
                        {
                            Debug.WriteLine(
                                $"Erreur non bloquante serveur OBS: {exception.GetType().Name}: {exception.Message}"
                            );
                        }
                    },
                    jetonAnnulation
                );
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.WriteLine(
                    $"Erreur non bloquante écoute serveur OBS: {exception.GetType().Name}: {exception.Message}"
                );
            }
        }
    }

    /*
     * Répond à une requête HTTP minimale en servant uniquement les fichiers du
     * dossier OBS, avec des en-têtes adaptés aux sources navigateur.
     */
    private static async Task RepondreAsync(TcpClient client, CancellationToken jetonAnnulation)
    {
        await using NetworkStream flux = client.GetStream();
        using StreamReader lecteur = new(flux, Encoding.ASCII, leaveOpen: true);

        string? ligneRequete = await lecteur.ReadLineAsync(jetonAnnulation);
        if (string.IsNullOrWhiteSpace(ligneRequete))
        {
            return;
        }

        string methode = ExtraireMethode(ligneRequete);
        string cheminRelatif = ExtraireCheminRelatif(ligneRequete);
        Dictionary<string, string> enTetes = await LireEntetesAsync(lecteur, jetonAnnulation);

        if (string.Equals(methode, "POST", StringComparison.OrdinalIgnoreCase))
        {
            await TraiterEcritureAsync(flux, lecteur, cheminRelatif, enTetes, jetonAnnulation);
            return;
        }

        string cheminFichier = ConstruireCheminFichier(cheminRelatif);

        if (string.IsNullOrWhiteSpace(cheminFichier) || !File.Exists(cheminFichier))
        {
            await EcrireReponseTexteAsync(
                flux,
                HttpStatusCode.NotFound,
                "text/plain; charset=utf-8",
                "Fichier OBS introuvable.",
                jetonAnnulation
            );
            return;
        }

        byte[] contenu = await File.ReadAllBytesAsync(cheminFichier, jetonAnnulation);
        await EcrireEnteteAsync(
            flux,
            HttpStatusCode.OK,
            DeterminerTypeContenu(cheminFichier),
            contenu.Length,
            jetonAnnulation
        );
        await flux.WriteAsync(contenu, jetonAnnulation);
    }

    /*
     * Lit les en-têtes HTTP jusqu'à la ligne vide finale.
     */
    private static async Task<Dictionary<string, string>> LireEntetesAsync(
        StreamReader lecteur,
        CancellationToken jetonAnnulation
    )
    {
        Dictionary<string, string> enTetes = new(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            string? ligne = await lecteur.ReadLineAsync(jetonAnnulation);
            if (string.IsNullOrEmpty(ligne))
            {
                return enTetes;
            }

            int separateur = ligne.IndexOf(':');
            if (separateur <= 0)
            {
                continue;
            }

            string nom = ligne[..separateur].Trim();
            string valeur = ligne[(separateur + 1)..].Trim();
            enTetes[nom] = valeur;
        }
    }

    /*
     * Retourne la méthode HTTP extraite de la ligne de requête.
     */
    private static string ExtraireMethode(string ligneRequete)
    {
        string[] morceaux = ligneRequete.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return morceaux.Length > 0 ? morceaux[0] : "GET";
    }

    /*
     * Extrait le chemin demandé par la requête HTTP.
     */
    private static string ExtraireCheminRelatif(string ligneRequete)
    {
        string[] morceaux = ligneRequete.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (morceaux.Length < 2)
        {
            return "index.html";
        }

        string chemin = morceaux[1].Split('?', 2)[0].Trim('/');
        return string.IsNullOrWhiteSpace(chemin) ? "index.html" : chemin;
    }

    /*
     * Construit un chemin sûr à partir du nom de fichier demandé.
     */
    private static string ConstruireCheminFichier(string cheminRelatif)
    {
        string nomFichier = Path.GetFileName(cheminRelatif.Replace('\\', '/'));
        return string.IsNullOrWhiteSpace(nomFichier)
            ? string.Empty
            : Path.Combine(ServiceExportObs.DossierExportObs, nomFichier);
    }

    /*
     * Accepte l'écriture du layout OBS persistant afin que l'overlay puisse
     * sauvegarder ses sections directement dans le dossier OBS.
     */
    private static async Task TraiterEcritureAsync(
        Stream flux,
        StreamReader lecteur,
        string cheminRelatif,
        IReadOnlyDictionary<string, string> enTetes,
        CancellationToken jetonAnnulation
    )
    {
        string cheminFichier = ConstruireCheminFichier(cheminRelatif);
        if (
            string.IsNullOrWhiteSpace(cheminFichier)
            || !string.Equals(
                Path.GetFileName(cheminFichier),
                Path.GetFileName(ServiceExportObs.CheminLayoutJson),
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            await EcrireReponseTexteAsync(
                flux,
                HttpStatusCode.Forbidden,
                "text/plain; charset=utf-8",
                "Écriture refusée.",
                jetonAnnulation
            );
            return;
        }

        if (
            !enTetes.TryGetValue("Content-Length", out string? valeurLongueur)
            || !int.TryParse(valeurLongueur, out int longueur)
            || longueur < 0
        )
        {
            await EcrireReponseTexteAsync(
                flux,
                HttpStatusCode.BadRequest,
                "text/plain; charset=utf-8",
                "Longueur de contenu invalide.",
                jetonAnnulation
            );
            return;
        }

        char[] tampon = new char[longueur];
        int totalLu = 0;

        while (totalLu < longueur)
        {
            int lu = await lecteur.ReadAsync(
                tampon.AsMemory(totalLu, longueur - totalLu),
                jetonAnnulation
            );
            if (lu <= 0)
            {
                break;
            }

            totalLu += lu;
        }

        if (totalLu != longueur)
        {
            await EcrireReponseTexteAsync(
                flux,
                HttpStatusCode.BadRequest,
                "text/plain; charset=utf-8",
                "Contenu incomplet.",
                jetonAnnulation
            );
            return;
        }

        Directory.CreateDirectory(ServiceExportObs.DossierExportObs);
        await ServiceExportObs.EcrireLayoutJsonDepuisOverlayAsync(
            new string(tampon, 0, totalLu),
            jetonAnnulation
        );

        await EcrireReponseTexteAsync(
            flux,
            HttpStatusCode.OK,
            "application/json; charset=utf-8",
            "{\"success\":true}",
            jetonAnnulation
        );
    }

    /*
     * Retourne le type MIME adapté aux fichiers exportés pour OBS.
     */
    private static string DeterminerTypeContenu(string cheminFichier)
    {
        return Path.GetExtension(cheminFichier).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".txt" => "text/plain; charset=utf-8",
            _ => "application/octet-stream",
        };
    }

    /*
     * Écrit une réponse texte complète.
     */
    private static async Task EcrireReponseTexteAsync(
        Stream flux,
        HttpStatusCode statut,
        string typeContenu,
        string texte,
        CancellationToken jetonAnnulation
    )
    {
        byte[] contenu = Encoding.UTF8.GetBytes(texte);
        await EcrireEnteteAsync(flux, statut, typeContenu, contenu.Length, jetonAnnulation);
        await flux.WriteAsync(contenu, jetonAnnulation);
    }

    /*
     * Écrit les en-têtes HTTP communs aux réponses OBS.
     */
    private static async Task EcrireEnteteAsync(
        Stream flux,
        HttpStatusCode statut,
        string typeContenu,
        int longueur,
        CancellationToken jetonAnnulation
    )
    {
        string entete =
            $"HTTP/1.1 {(int)statut} {statut}\r\n"
            + $"Content-Type: {typeContenu}\r\n"
            + $"Content-Length: {longueur}\r\n"
            + "Access-Control-Allow-Origin: *\r\n"
            + "Cache-Control: no-store, no-cache, must-revalidate, max-age=0\r\n"
            + "Connection: close\r\n\r\n";
        byte[] donnees = Encoding.ASCII.GetBytes(entete);
        await flux.WriteAsync(donnees, jetonAnnulation);
    }
}

using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace RA.Compagnon.Services;

/// <summary>
/// Lit la mémoire d'un cœur RetroArch via l'interface réseau officielle.
/// </summary>
public sealed class ServiceMemoireRetroArch
{
    public const string HoteParDefaut = "127.0.0.1";
    public const int PortParDefaut = 55355;

    private static readonly TimeSpan DelaiReponseParDefaut = TimeSpan.FromMilliseconds(250);
    private readonly string _hote;
    private readonly int _port;
    private readonly TimeSpan _delaiReponse;

    public ServiceMemoireRetroArch(
        string hote = HoteParDefaut,
        int port = PortParDefaut,
        TimeSpan? delaiReponse = null
    )
    {
        _hote = string.IsNullOrWhiteSpace(hote) ? HoteParDefaut : hote;
        _port = port <= 0 ? PortParDefaut : port;
        _delaiReponse = delaiReponse ?? DelaiReponseParDefaut;
    }

    /// <summary>
    /// Indique si l'émulateur détecté expose le protocole RetroArch ciblé.
    /// </summary>
    public bool EmulateurPrisEnCharge(string nomEmulateur)
    {
        return string.Equals(nomEmulateur, "RetroArch", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Vérifie rapidement que l'interface réseau RetroArch répond.
    /// </summary>
    public async Task<DiagnosticMemoireRetroArch?> SonderAsync(
        string nomEmulateur,
        CancellationToken cancellationToken = default
    )
    {
        if (!EmulateurPrisEnCharge(nomEmulateur))
        {
            return null;
        }

        string version = await EnvoyerCommandeAsync("VERSION", cancellationToken);

        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        string statut = await EnvoyerCommandeAsync("GET_STATUS", cancellationToken);
        return new DiagnosticMemoireRetroArch(_hote, _port, version, statut);
    }

    /// <summary>
    /// Lit un bloc de mémoire brute depuis le cœur actif.
    /// </summary>
    public async Task<byte[]?> LireMemoireAsync(
        string nomEmulateur,
        uint adresse,
        int nombreOctets,
        CancellationToken cancellationToken = default
    )
    {
        if (!EmulateurPrisEnCharge(nomEmulateur) || nombreOctets <= 0)
        {
            return null;
        }

        string commande = string.Create(
            CultureInfo.InvariantCulture,
            $"READ_CORE_MEMORY {adresse} {nombreOctets}"
        );
        string reponse = await EnvoyerCommandeAsync(commande, cancellationToken);
        return DecoderReponseMemoire(reponse);
    }

    private async Task<string> EnvoyerCommandeAsync(
        string commande,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using UdpClient client = new();
            client.Connect(_hote, _port);

            // L'interface RetroArch attend une commande texte simple via UDP.
            byte[] donnees = Encoding.ASCII.GetBytes(commande);
            await client.SendAsync(donnees, cancellationToken);

            Task<UdpReceiveResult> reception = client.ReceiveAsync(cancellationToken).AsTask();
            Task delai = Task.Delay(_delaiReponse, cancellationToken);
            Task termine = await Task.WhenAny(reception, delai);

            if (!ReferenceEquals(termine, reception))
            {
                return string.Empty;
            }

            UdpReceiveResult resultat = await reception;
            return Encoding.UTF8.GetString(resultat.Buffer).Trim();
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
        catch (SocketException)
        {
            return string.Empty;
        }
    }

    private static byte[]? DecoderReponseMemoire(string reponse)
    {
        if (string.IsNullOrWhiteSpace(reponse))
        {
            return null;
        }

        string texte = reponse.Trim();

        if (
            string.Equals(texte, "ERR", StringComparison.OrdinalIgnoreCase)
            || string.Equals(texte, "ERROR", StringComparison.OrdinalIgnoreCase)
            || string.Equals(texte, "-1", StringComparison.OrdinalIgnoreCase)
        )
        {
            return null;
        }

        string[] morceaux = texte.Split(
            [' ', '\t', '\r', '\n', ',', ';'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        if (morceaux.Length == 1 && RessembleAFluxHexaContinu(morceaux[0]))
        {
            // Certains cœurs renvoient un flux hexa compact plutôt qu'une liste d'octets.
            return DecoderFluxHexaContinu(morceaux[0]);
        }

        List<byte> octets = new(morceaux.Length);

        foreach (string morceau in morceaux)
        {
            if (!EssayerDecoderOctet(morceau, out byte valeur))
            {
                return null;
            }

            octets.Add(valeur);
        }

        return octets.ToArray();
    }

    private static bool RessembleAFluxHexaContinu(string texte)
    {
        string valeur = NettoyerPrefixeHexa(texte);

        if (valeur.Length < 2 || valeur.Length % 2 != 0)
        {
            return false;
        }

        return valeur.All(Uri.IsHexDigit);
    }

    private static byte[] DecoderFluxHexaContinu(string texte)
    {
        string valeur = NettoyerPrefixeHexa(texte);
        byte[] resultat = new byte[valeur.Length / 2];

        for (int index = 0; index < valeur.Length; index += 2)
        {
            string morceau = valeur.Substring(index, 2);
            resultat[index / 2] = byte.Parse(
                morceau,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture
            );
        }

        return resultat;
    }

    private static bool EssayerDecoderOctet(string texte, out byte valeur)
    {
        string valeurNettoyee = NettoyerPrefixeHexa(texte);

        if (
            byte.TryParse(
                valeurNettoyee,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out valeur
            )
        )
        {
            return true;
        }

        return byte.TryParse(
            valeurNettoyee,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out valeur
        );
    }

    private static string NettoyerPrefixeHexa(string texte)
    {
        return texte.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? texte[2..] : texte;
    }
}

public sealed record DiagnosticMemoireRetroArch(
    string Hote,
    int Port,
    string Version,
    string Statut
);

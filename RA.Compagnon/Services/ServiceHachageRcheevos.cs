using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RA.Compagnon.Services;

/// <summary>
/// Tente de générer un hash RetroAchievements natif à partir du bridge rcheevos.
/// </summary>
public sealed class ServiceHachageRcheevos
{
    private readonly Dictionary<string, ResultatHashRcheevos> _cache = [];
    private bool _bridgeIndisponible;

    /// <summary>
    /// Indique si le bridge natif de hash semble encore exploitable dans le processus courant.
    /// </summary>
    public bool EstBridgeDisponible => !_bridgeIndisponible;

    /// <summary>
    /// Génère un hash RetroAchievements pour le fichier et la console fournis.
    /// </summary>
    public ResultatHashRcheevos CalculerHash(string cheminFichier, int identifiantConsole)
    {
        if (string.IsNullOrWhiteSpace(cheminFichier) || identifiantConsole <= 0)
        {
            return new ResultatHashRcheevos(string.Empty, "Paramètres invalides.");
        }

        if (!Path.IsPathRooted(cheminFichier) || !File.Exists(cheminFichier))
        {
            return new ResultatHashRcheevos(string.Empty, "Fichier local introuvable.");
        }

        FileInfo informationsFichier = new(cheminFichier);
        string cleCache =
            $"{identifiantConsole}|{informationsFichier.FullName}|{informationsFichier.Length}|{informationsFichier.LastWriteTimeUtc.Ticks}";

        if (_cache.TryGetValue(cleCache, out ResultatHashRcheevos? resultatCache))
        {
            return resultatCache;
        }

        if (_bridgeIndisponible)
        {
            return new ResultatHashRcheevos(
                string.Empty,
                "Bridge rcheevos indisponible pour le hash."
            );
        }

        try
        {
            StringBuilder hash = new(33);
            StringBuilder message = new(256);
            int resultat = MethodesNatives.GenererHashJeu(
                identifiantConsole,
                informationsFichier.FullName,
                hash,
                hash.Capacity,
                message,
                message.Capacity
            );

            ResultatHashRcheevos calcul =
                resultat == 0
                    ? new ResultatHashRcheevos(hash.ToString().Trim(), string.Empty)
                    : new ResultatHashRcheevos(
                        string.Empty,
                        string.IsNullOrWhiteSpace(message.ToString())
                            ? $"Génération du hash impossible (code {resultat})."
                            : message.ToString().Trim()
                    );

            _cache[cleCache] = calcul;
            return calcul;
        }
        catch (DllNotFoundException)
        {
            _bridgeIndisponible = true;
        }
        catch (EntryPointNotFoundException)
        {
            _bridgeIndisponible = true;
        }
        catch (BadImageFormatException)
        {
            _bridgeIndisponible = true;
        }

        return new ResultatHashRcheevos(string.Empty, "Bridge rcheevos indisponible pour le hash.");
    }

    private static class MethodesNatives
    {
        private const string NomBibliotheque = "ra_compagnon_rcheevos_bridge";

        [DllImport(
            NomBibliotheque,
            EntryPoint = "ra_compagnon_rcheevos_generate_game_hash",
            CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi
        )]
        public static extern int GenererHashJeu(
            int identifiantConsole,
            string cheminFichier,
            StringBuilder hash,
            int tailleHash,
            StringBuilder message,
            int tailleMessage
        );
    }
}

/// <summary>
/// Résultat d'un calcul de hash RetroAchievements.
/// </summary>
public sealed record ResultatHashRcheevos(string Hash, string Message);

using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

/// <summary>
/// Source passive pour RALibRetro en lisant le bloc ACHV embarqué dans les fichiers `.state`.
/// </summary>
public sealed class ServiceRcheevosRalibretro
{
    private const string MarqueurEtat = "RASTATE";
    private const string BlocAchievements = "ACHV";
    private readonly List<string> _dossiersEtats = [];
    private readonly List<string> _clesJeu = [];
    private string _dossierRacine = string.Empty;
    private string _cheminEtatActif = string.Empty;
    private DateTime _derniereEcritureEtatUtc = DateTime.MinValue;

    public Task<bool> DefinirProcessusAsync(
        JeuDetecteLocalement? jeuLocal,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        if (
            jeuLocal is null
            || !string.Equals(
                jeuLocal.NomEmulateur,
                "RALibRetro",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            Reinitialiser();
            return Task.FromResult(false);
        }

        Reinitialiser();
        _dossierRacine = DeterminerDossierRacine(jeuLocal);

        if (string.IsNullOrWhiteSpace(_dossierRacine) || !Directory.Exists(_dossierRacine))
        {
            return Task.FromResult(false);
        }

        AlimenterDossiersEtats();
        AlimenterClesJeu(jeuLocal);
        return Task.FromResult(_dossiersEtats.Count > 0);
    }

    public string ObtenirResumeSource()
    {
        if (string.IsNullOrWhiteSpace(_dossierRacine))
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(_cheminEtatActif))
        {
            return $"Source mémoire : RALibRetro savestate ({Path.GetFileName(_cheminEtatActif)})";
        }

        return "Source mémoire : RALibRetro savestate";
    }

    public bool ActualiserProgressionPont()
    {
        string? cheminEtat = TrouverMeilleurEtat();

        if (string.IsNullOrWhiteSpace(cheminEtat) || !File.Exists(cheminEtat))
        {
            EssayerEffacerProgressionSerialisee();
            _cheminEtatActif = string.Empty;
            _derniereEcritureEtatUtc = DateTime.MinValue;
            return false;
        }

        DateTime derniereEcritureUtc = File.GetLastWriteTimeUtc(cheminEtat);
        if (
            string.Equals(cheminEtat, _cheminEtatActif, StringComparison.OrdinalIgnoreCase)
            && derniereEcritureUtc == _derniereEcritureEtatUtc
        )
        {
            // Rien n'a changé depuis le dernier import de progression.
            return true;
        }

        byte[] contenuEtat;

        try
        {
            contenuEtat = File.ReadAllBytes(cheminEtat);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        if (!ExtraireBlocAchievements(contenuEtat, out byte[] blocAchievements))
        {
            EssayerEffacerProgressionSerialisee();
            return false;
        }

        // Seul le bloc ACHV nous intéresse ici ; le reste du savestate est ignoré.
        if (!EssayerDefinirProgressionSerialisee(blocAchievements))
        {
            return false;
        }

        _cheminEtatActif = cheminEtat;
        _derniereEcritureEtatUtc = derniereEcritureUtc;
        return true;
    }

    public void Reinitialiser()
    {
        _dossierRacine = string.Empty;
        _cheminEtatActif = string.Empty;
        _derniereEcritureEtatUtc = DateTime.MinValue;
        _dossiersEtats.Clear();
        _clesJeu.Clear();
        EssayerEffacerProgressionSerialisee();
    }

    private void AlimenterDossiersEtats()
    {
        foreach (string dossier in DeterminerDossiersDepuisConfiguration())
        {
            if (Directory.Exists(dossier))
            {
                _dossiersEtats.Add(dossier);
            }
        }

        AjouterDossierSiAbsent(Path.Combine(_dossierRacine, "Saves"));
        AjouterDossierSiAbsent(Path.Combine(_dossierRacine, "States"));
    }

    private IEnumerable<string> DeterminerDossiersDepuisConfiguration()
    {
        string cheminConfiguration = Path.Combine(_dossierRacine, "RALibretro.json");

        if (!File.Exists(cheminConfiguration))
        {
            yield break;
        }

        JsonDocument? document = null;

        try
        {
            document = JsonDocument.Parse(File.ReadAllText(cheminConfiguration));
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }
        catch (JsonException)
        {
            yield break;
        }

        using (document)
        {
            if (
                !document.RootElement.TryGetProperty("saves", out JsonElement savesElement)
                || !savesElement.TryGetProperty("statePath", out JsonElement statePathElement)
            )
            {
                yield break;
            }

            string? codeChemin = statePathElement.GetString();
            if (string.IsNullOrWhiteSpace(codeChemin))
            {
                yield break;
            }

            string sousDossier = codeChemin.Contains('T', StringComparison.OrdinalIgnoreCase)
                ? "States"
                : "Saves";

            yield return Path.Combine(_dossierRacine, sousDossier);
        }
    }

    private void AlimenterClesJeu(JeuDetecteLocalement jeuLocal)
    {
        AjouterCleDepuisChemin(jeuLocal.CheminJeuRetenu);
        AjouterCleDepuisChemin(jeuLocal.CheminJeuLigneCommande);
        AjouterCleDepuisChemin(jeuLocal.CheminJeuEstime);
        AjouterCleTexte(jeuLocal.TitreJeuEstime);

        string cheminConfiguration = Path.Combine(_dossierRacine, "RALibretro.json");
        if (!File.Exists(cheminConfiguration))
        {
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(cheminConfiguration));
            if (
                document.RootElement.TryGetProperty("recent", out JsonElement recentElement)
                && recentElement.ValueKind == JsonValueKind.Array
                && recentElement.GetArrayLength() > 0
            )
            {
                JsonElement premier = recentElement[0];
                if (premier.TryGetProperty("path", out JsonElement pathElement))
                {
                    AjouterCleDepuisChemin(pathElement.GetString());
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
    }

    private string? TrouverMeilleurEtat()
    {
        List<FileInfo> candidats = [];

        foreach (string dossier in _dossiersEtats)
        {
            try
            {
                DirectoryInfo repertoire = new(dossier);
                candidats.AddRange(
                    repertoire
                        .EnumerateFiles("*.state*", SearchOption.AllDirectories)
                        .Where(fichier =>
                            !fichier.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                        )
                );
            }
            catch (DirectoryNotFoundException) { }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        if (candidats.Count == 0)
        {
            return null;
        }

        FileInfo? meilleur = candidats
            .OrderByDescending(ScoreEtat)
            .ThenByDescending(fichier => fichier.LastWriteTimeUtc)
            .FirstOrDefault();

        return meilleur?.FullName;
    }

    private int ScoreEtat(FileInfo fichier)
    {
        string nom = NormaliserCle(fichier.Name);
        int score = 0;

        // Score simple : meilleur match textuel de nom de jeu, puis fraîcheur du fichier.
        foreach (string cle in _clesJeu)
        {
            if (string.IsNullOrWhiteSpace(cle))
            {
                continue;
            }

            if (nom.Contains(cle, StringComparison.Ordinal))
            {
                score = Math.Max(score, cle.Length * 10);
            }
        }

        return score;
    }

    private static bool ExtraireBlocAchievements(byte[] contenuEtat, out byte[] blocAchievements)
    {
        blocAchievements = [];

        if (contenuEtat.Length < 8)
        {
            return false;
        }

        string marqueur = Encoding.ASCII.GetString(contenuEtat, 0, 7);
        if (!string.Equals(marqueur, MarqueurEtat, StringComparison.Ordinal))
        {
            return false;
        }

        int position = 8;

        while (position + 8 <= contenuEtat.Length)
        {
            string identifiantBloc = Encoding.ASCII.GetString(contenuEtat, position, 4);
            int tailleBloc = BitConverter.ToInt32(contenuEtat, position + 4);
            position += 8;

            if (tailleBloc < 0 || position + tailleBloc > contenuEtat.Length)
            {
                return false;
            }

            if (string.Equals(identifiantBloc, BlocAchievements, StringComparison.Ordinal))
            {
                blocAchievements = new byte[tailleBloc];
                Buffer.BlockCopy(contenuEtat, position, blocAchievements, 0, tailleBloc);
                return true;
            }

            // Les blocs sont alignés sur 8 octets dans ce format de savestate.
            position += AlignerSur8Octets(tailleBloc);
        }

        return false;
    }

    private void AjouterDossierSiAbsent(string dossier)
    {
        if (
            !string.IsNullOrWhiteSpace(dossier)
            && !_dossiersEtats.Contains(dossier, StringComparer.OrdinalIgnoreCase)
            && Directory.Exists(dossier)
        )
        {
            _dossiersEtats.Add(dossier);
        }
    }

    private void AjouterCleDepuisChemin(string? chemin)
    {
        if (string.IsNullOrWhiteSpace(chemin))
        {
            return;
        }

        AjouterCleTexte(Path.GetFileName(chemin));
        AjouterCleTexte(Path.GetFileNameWithoutExtension(chemin));
    }

    private void AjouterCleTexte(string? texte)
    {
        string cle = NormaliserCle(texte);
        if (!string.IsNullOrWhiteSpace(cle) && !_clesJeu.Contains(cle, StringComparer.Ordinal))
        {
            _clesJeu.Add(cle);
        }
    }

    private static string DeterminerDossierRacine(JeuDetecteLocalement jeuLocal)
    {
        if (!string.IsNullOrWhiteSpace(jeuLocal.CheminExecutable))
        {
            string? dossier = Path.GetDirectoryName(jeuLocal.CheminExecutable);
            if (!string.IsNullOrWhiteSpace(dossier))
            {
                return dossier;
            }
        }

        return string.Empty;
    }

    private static int AlignerSur8Octets(int taille)
    {
        return (taille + 7) & ~7;
    }

    private static string NormaliserCle(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte))
        {
            return string.Empty;
        }

        StringBuilder resultat = new(texte.Length);

        foreach (char caractere in texte)
        {
            if (char.IsLetterOrDigit(caractere))
            {
                resultat.Append(char.ToLowerInvariant(caractere));
            }
        }

        return resultat.ToString();
    }

    private static class MethodesNatives
    {
        private const string NomBibliotheque = "ra_compagnon_rcheevos_bridge";

        [DllImport(
            NomBibliotheque,
            EntryPoint = "ra_compagnon_rcheevos_set_serialized_progress",
            CallingConvention = CallingConvention.Cdecl
        )]
        public static extern int DefinirProgressionSerialisee(
            byte[] progressionSerialisee,
            uint tailleProgression
        );

        [DllImport(
            NomBibliotheque,
            EntryPoint = "ra_compagnon_rcheevos_clear_serialized_progress",
            CallingConvention = CallingConvention.Cdecl
        )]
        public static extern int EffacerProgressionSerialisee();
    }

    private static bool EssayerDefinirProgressionSerialisee(byte[] blocAchievements)
    {
        try
        {
            return MethodesNatives.DefinirProgressionSerialisee(
                    blocAchievements,
                    (uint)blocAchievements.Length
                ) == 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    private static void EssayerEffacerProgressionSerialisee()
    {
        try
        {
            _ = MethodesNatives.EffacerProgressionSerialisee();
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        catch (BadImageFormatException) { }
    }
}

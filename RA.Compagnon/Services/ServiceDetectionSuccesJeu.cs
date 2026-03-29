using System.Globalization;
using System.IO;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

/// <summary>
/// Détecte les nouveaux succès obtenus en comparant deux états successifs d'un même jeu.
/// </summary>
public sealed class ServiceDetectionSuccesJeu
{
    private static readonly string CheminJournalDetectionSucces = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-detection-succes.log"
    );

    public static void ReinitialiserJournalSession()
    {
        try
        {
            string? repertoire = Path.GetDirectoryName(CheminJournalDetectionSucces);

            if (!string.IsNullOrWhiteSpace(repertoire))
            {
                Directory.CreateDirectory(repertoire);
            }

            File.WriteAllText(
                CheminJournalDetectionSucces,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] nouvelle_session{Environment.NewLine}"
            );
        }
        catch
        {
            // Ce journal est auxiliaire.
        }
    }

    public IReadOnlyList<SuccesDebloqueDetecte> DetecterNouveauxSucces(
        int identifiantJeu,
        string titreJeu,
        IReadOnlyDictionary<int, EtatObservationSuccesLocal> etatPrecedent,
        IReadOnlyCollection<GameAchievementV2> succesCourants
    )
    {
        if (identifiantJeu <= 0 || succesCourants.Count == 0)
        {
            return [];
        }

        List<SuccesDebloqueDetecte> resultats = [];

        foreach (GameAchievementV2 succes in succesCourants)
        {
            etatPrecedent.TryGetValue(succes.Id, out EtatObservationSuccesLocal? precedent);
            string dateSoft = (succes.DateEarned ?? string.Empty).Trim();
            string dateHard = (succes.DateEarnedHardcore ?? string.Empty).Trim();

            bool nouveauHardcore =
                !string.IsNullOrWhiteSpace(dateHard)
                && !string.Equals(
                    precedent?.DateObtentionHardcore,
                    dateHard,
                    StringComparison.Ordinal
                );

            bool nouveauSoftcore =
                !nouveauHardcore
                && !string.IsNullOrWhiteSpace(dateSoft)
                && !string.Equals(precedent?.DateObtention, dateSoft, StringComparison.Ordinal);

            if (!nouveauHardcore && !nouveauSoftcore)
            {
                continue;
            }

            resultats.Add(
                new SuccesDebloqueDetecte
                {
                    IdentifiantJeu = identifiantJeu,
                    TitreJeu = titreJeu?.Trim() ?? string.Empty,
                    IdentifiantSucces = succes.Id,
                    TitreSucces = succes.Title?.Trim() ?? string.Empty,
                    Points = succes.Points,
                    Hardcore = nouveauHardcore,
                    DateObtention = nouveauHardcore ? dateHard : dateSoft,
                }
            );
        }

        return resultats;
    }

    public Dictionary<int, EtatObservationSuccesLocal> CapturerEtat(
        IReadOnlyCollection<GameAchievementV2> succesCourants
    )
    {
        Dictionary<int, EtatObservationSuccesLocal> resultat = [];

        foreach (GameAchievementV2 succes in succesCourants)
        {
            resultat[succes.Id] = new EtatObservationSuccesLocal
            {
                IdentifiantSucces = succes.Id,
                DateObtention = (succes.DateEarned ?? string.Empty).Trim(),
                DateObtentionHardcore = (succes.DateEarnedHardcore ?? string.Empty).Trim(),
            };
        }

        return resultat;
    }

    public static void JournaliserInitialisation(int identifiantJeu, string titreJeu, int nombreSucces)
    {
        JournaliserLigne(
            $"etat=initialisation;source=session;jeu={identifiantJeu};titreJeu={Nettoyer(titreJeu)};succes={nombreSucces.ToString(CultureInfo.InvariantCulture)}"
        );
    }

    public static void JournaliserDetection(
        SuccesDebloqueDetecte succes,
        string source = "session"
    )
    {
        JournaliserLigne(
            $"etat=deblocage;source={Nettoyer(source)};jeu={succes.IdentifiantJeu};titreJeu={Nettoyer(succes.TitreJeu)};succes={succes.IdentifiantSucces};titreSucces={Nettoyer(succes.TitreSucces)};points={succes.Points.ToString(CultureInfo.InvariantCulture)};mode={(succes.Hardcore ? "hardcore" : "softcore")};date={Nettoyer(succes.DateObtention)}"
        );
    }

    private static void JournaliserLigne(string details)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CheminJournalDetectionSucces)!);
            File.AppendAllText(
                CheminJournalDetectionSucces,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {details}{Environment.NewLine}"
            );
        }
        catch
        {
            // Ce journal est auxiliaire.
        }
    }

    private static string Nettoyer(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}

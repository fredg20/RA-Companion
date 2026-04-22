using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using RA.Compagnon.Modeles.Obs;

/*
 * Gère l'export local des données publiques de Compagnon vers des fichiers
 * consommables par OBS Studio.
 */
namespace RA.Compagnon.Services;

/*
 * Écrit un état JSON, des sources texte et un overlay HTML minimal dans un
 * dossier stable afin qu'OBS puisse les relire pendant que Compagnon tourne.
 */
public sealed class ServiceExportObs
{
    private const string ExtensionTemporaire = ".tmp";
    private readonly SemaphoreSlim _verrouExport = new(1, 1);

    private static readonly JsonSerializerOptions OptionsJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string DossierExportObs =>
        Path.Combine(ServiceModeDiagnostic.DossierApplication, "OBS");

    public static string CheminEtatJson => Path.Combine(DossierExportObs, "state.json");

    public static string CheminOverlayHtml => Path.Combine(DossierExportObs, "overlay.html");

    /*
     * Publie l'état OBS complet en gardant chaque écriture atomique pour
     * éviter qu'OBS lise un fichier partiellement écrit.
     */
    public async Task ExporterAsync(EtatExportObs etat, CancellationToken jetonAnnulation = default)
    {
        await _verrouExport.WaitAsync(jetonAnnulation);

        try
        {
            Directory.CreateDirectory(DossierExportObs);
            await EcrireJsonAsync(CheminEtatJson, etat, jetonAnnulation);
            await EcrireTexteAsync(CheminOverlayHtml, ConstruireOverlayHtml(), jetonAnnulation);
            await EcrireSourcesTexteAsync(etat, jetonAnnulation);
        }
        finally
        {
            _verrouExport.Release();
        }
    }

    /*
     * Écrit les fichiers texte séparés que l'utilisateur pourra brancher dans
     * OBS avec des sources texte indépendantes.
     */
    private static async Task EcrireSourcesTexteAsync(
        EtatExportObs etat,
        CancellationToken jetonAnnulation
    )
    {
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "jeu-titre.txt"),
            NettoyerTexteSource(etat.Jeu.Titre),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "jeu-statut.txt"),
            NettoyerTexteSource(etat.Jeu.Statut),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "progression.txt"),
            ConstruireProgressionTexte(etat),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "succes-titre.txt"),
            NettoyerTexteSource(etat.SuccesCourant.Titre),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "succes-description.txt"),
            NettoyerTexteSource(etat.SuccesCourant.Description),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "succes-badge.txt"),
            NettoyerTexteSource(etat.SuccesCourant.Badge),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "dernier-succes.txt"),
            ConstruireDernierSuccesTexte(etat),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "synchronisation.txt"),
            NettoyerTexteSource(etat.EtatSynchronisation),
            jetonAnnulation
        );
    }

    /*
     * Sérialise l'état principal au format JSON lisible par une source
     * navigateur ou par un outil externe.
     */
    private static async Task EcrireJsonAsync<T>(
        string chemin,
        T donnees,
        CancellationToken jetonAnnulation
    )
    {
        string json = JsonSerializer.Serialize(donnees, OptionsJson);
        await EcrireTexteAsync(chemin, json, jetonAnnulation);
    }

    /*
     * Écrit un fichier texte par remplacement atomique via un fichier
     * temporaire dans le même dossier.
     */
    private static async Task EcrireTexteAsync(
        string chemin,
        string contenu,
        CancellationToken jetonAnnulation
    )
    {
        string cheminTemporaire = chemin + ExtensionTemporaire;
        await File.WriteAllTextAsync(cheminTemporaire, contenu, jetonAnnulation);
        File.Move(cheminTemporaire, chemin, overwrite: true);
    }

    /*
     * Construit une version compacte de la progression utilisable directement
     * comme source texte OBS.
     */
    private static string ConstruireProgressionTexte(EtatExportObs etat)
    {
        string resume = NettoyerTexteSource(etat.Progression.Resume);
        string pourcentage = NettoyerTexteSource(etat.Progression.Pourcentage);

        if (string.IsNullOrWhiteSpace(resume))
        {
            return pourcentage;
        }

        if (string.IsNullOrWhiteSpace(pourcentage))
        {
            return resume;
        }

        return $"{resume} - {pourcentage}";
    }

    /*
     * Formate le dernier succès obtenu de manière courte pour les overlays ou
     * les sources texte dédiées aux alertes.
     */
    private static string ConstruireDernierSuccesTexte(EtatExportObs etat)
    {
        SuccesDebloqueExportObs succes = etat.DernierSuccesObtenu;

        if (succes.IdentifiantSucces <= 0 || string.IsNullOrWhiteSpace(succes.Titre))
        {
            return string.Empty;
        }

        string mode = string.IsNullOrWhiteSpace(succes.Mode) ? "succès" : succes.Mode;
        return $"{succes.Titre} - {succes.Points} pts - {mode}";
    }

    /*
     * Réduit les retours de ligne et espaces multiples pour rendre les sources
     * texte plus prévisibles dans OBS.
     */
    private static string NettoyerTexteSource(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : string.Join(' ', valeur.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /*
     * Fournit un overlay qui dépend explicitement de state.json pour séparer
     * l'affichage HTML des données exportées par Compagnon.
     */
    private static string ConstruireOverlayHtml()
    {
        return """
            <!doctype html>
            <html lang="fr">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>RA-Compagnon OBS</title>
              <style>
                :root {
                  color-scheme: dark;
                  --fond: rgba(18, 18, 20, 0.82);
                  --texte: #f3f3f3;
                  --secondaire: #c7c7c7;
                  --accent: #43a82a;
                }
                * { box-sizing: border-box; }
                html,
                body {
                  margin: 0;
                  width: fit-content;
                  height: fit-content;
                  overflow: hidden;
                  background: transparent;
                  color: var(--texte);
                  font-family: "Segoe UI", sans-serif;
                }
                main {
                  display: inline-block;
                  width: fit-content;
                  min-width: 360px;
                  max-width: 720px;
                  margin: 0;
                  padding: 22px 26px;
                  border-radius: 18px;
                  background: var(--fond);
                  box-shadow: 0 18px 42px rgba(0, 0, 0, 0.35);
                }
                .statut {
                  color: var(--accent);
                  font-size: 15px;
                  font-weight: 700;
                  letter-spacing: 0.08em;
                  text-transform: uppercase;
                }
                h1 {
                  margin: 6px 0 10px;
                  font-size: 34px;
                  line-height: 1.08;
                }
                .progression,
                .succes {
                  color: var(--secondaire);
                  font-size: 18px;
                  line-height: 1.35;
                }
                .erreur {
                  margin-top: 10px;
                  color: #ffb4ab;
                  font-size: 14px;
                  line-height: 1.35;
                }
                .barre {
                  width: 100%;
                  height: 10px;
                  margin: 16px 0;
                  overflow: hidden;
                  border-radius: 999px;
                  background: rgba(255, 255, 255, 0.14);
                }
                .barre span {
                  display: block;
                  height: 100%;
                  width: 0%;
                  border-radius: inherit;
                  background: var(--accent);
                  transition: width 350ms ease;
                }
              </style>
            </head>
            <body>
              <main>
                <div id="statut" class="statut">RA-Compagnon</div>
                <h1 id="jeu">Aucun jeu</h1>
                <div id="progression" class="progression"></div>
                <div class="barre"><span id="barre"></span></div>
                <div id="succes" class="succes"></div>
                <div id="erreur" class="erreur"></div>
              </main>
              <script>
                function appliquerEtat(etat) {
                  document.getElementById('statut').textContent = etat?.jeu?.statut || 'RA-Compagnon';
                  document.getElementById('jeu').textContent = etat?.jeu?.titre || 'Aucun jeu';
                  document.getElementById('progression').textContent = [etat?.progression?.resume, etat?.progression?.pourcentage].filter(Boolean).join(' - ');
                  document.getElementById('barre').style.width = Math.max(0, Math.min(100, etat?.progression?.valeur || 0)) + '%';
                  document.getElementById('succes').textContent = etat?.succesCourant?.titre || '';
                  document.getElementById('erreur').textContent = '';
                }

                function afficherErreur(message) {
                  document.getElementById('statut').textContent = 'RA-Compagnon';
                  document.getElementById('jeu').textContent = 'Données OBS indisponibles';
                  document.getElementById('progression').textContent = '';
                  document.getElementById('barre').style.width = '0%';
                  document.getElementById('succes').textContent = '';
                  document.getElementById('erreur').textContent = message;
                }

                async function lireEtatJsonAvecFetch() {
                  const reponse = await fetch('state.json?cache=' + Date.now(), { cache: 'no-store' });
                  if (!reponse.ok) {
                    throw new Error('state.json introuvable');
                  }

                  return await reponse.json();
                }

                function lireEtatJsonAvecXhr() {
                  return new Promise((resolve, reject) => {
                    const requete = new XMLHttpRequest();
                    requete.open('GET', 'state.json?cache=' + Date.now(), true);
                    requete.onreadystatechange = () => {
                      if (requete.readyState !== 4) {
                        return;
                      }

                      if (requete.status === 0 || (requete.status >= 200 && requete.status < 300)) {
                        try {
                          resolve(JSON.parse(requete.responseText));
                        } catch (erreur) {
                          reject(erreur);
                        }
                        return;
                      }

                      reject(new Error('state.json introuvable'));
                    };
                    requete.onerror = () => reject(new Error('Lecture locale de state.json bloquée'));
                    requete.send();
                  });
                }

                async function rafraichir() {
                  try {
                    appliquerEtat(await lireEtatJsonAvecFetch());
                  } catch {
                    try {
                      appliquerEtat(await lireEtatJsonAvecXhr());
                    } catch {
                      afficherErreur('Impossible de lire state.json. Vérifie que overlay.html et state.json sont dans le même dossier OBS.');
                    }
                  }
                }

                rafraichir();
                setInterval(rafraichir, 1000);
              </script>
            </body>
            </html>
            """;
    }
}

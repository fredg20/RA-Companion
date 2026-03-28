using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon;

public partial class MainWindow
{
    /// <summary>
    /// Active les rafraîchissements API généraux ainsi que la surveillance légère du Rich Presence.
    /// </summary>
    private void DemarrerActualisationAutomatique()
    {
        if (!ConfigurationConnexionEstComplete())
        {
            return;
        }

        if (_profilUtilisateurAccessible && !_minuteurActualisationApi.IsEnabled)
        {
            _minuteurActualisationApi.Start();
        }

        if (_profilUtilisateurAccessible && !_minuteurActualisationRichPresence.IsEnabled)
        {
            _minuteurActualisationRichPresence.Start();
        }

        if (!_minuteurSondeLocaleEmulateurs.IsEnabled)
        {
            _minuteurSondeLocaleEmulateurs.Start();
        }
    }

    /// <summary>
    /// Redémarre le minuteur API pour repousser le prochain tick après un rafraîchissement ciblé.
    /// </summary>
    private void RedemarrerMinuteurActualisationApi()
    {
        if (!_profilUtilisateurAccessible)
        {
            return;
        }

        _minuteurActualisationApi.Stop();
        _minuteurActualisationApi.Start();
    }

    /// <summary>
    /// Arrête les rafraîchissements périodiques.
    /// </summary>
    private void ArreterActualisationAutomatique()
    {
        _minuteurActualisationApi.Stop();
        _minuteurActualisationRichPresence.Stop();
        _minuteurSondeLocaleEmulateurs.Stop();
        _minuteurRotationVisuelsJeuEnCours.Stop();
    }

    /// <summary>
    /// Aucun amorçage local : l'application reste entièrement autonome.
    /// </summary>
    private Task AmorcerEtatJeuLocalAuDemarrageAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Réinitialise les derniers marqueurs utilisés pour éviter les rechargements API inutiles.
    /// </summary>
    private void ReinitialiserContexteSurveillance()
    {
        _actualisationApiCibleeEnAttente = false;
        _dernierIdentifiantJeuApi = 0;
        _dernierIdentifiantJeuAvecInfos = 0;
        _dernierIdentifiantJeuAvecProgression = 0;
        _dernierTitreJeuApi = string.Empty;
        _dernierePresenceRiche = string.Empty;
        _signatureDernierEtatRichPresence = string.Empty;
        _signatureDerniereSondeLocaleEmulateurs = string.Empty;
        _signatureDernierJeuLocalResolut = string.Empty;
        _signatureDerniereNoticeCompteJournalisee = string.Empty;
        _dernierPseudoCharge = string.Empty;
        _dernierProfilUtilisateurCharge = null;
        _dernierResumeUtilisateurCharge = null;
        _dernierEtatSondeLocaleEmulateurs = null;
        _identifiantJeuLocalResolutEnAttente = 0;
        _titreJeuLocalResolutEnAttente = string.Empty;
        _identifiantJeuLocalActif = 0;
        _titreJeuLocalActif = string.Empty;
        _consolesResolutionLocale = [];
    }

    /// <summary>
    /// Surveille en continu l'état Rich Presence sans recharger tout le contenu principal.
    /// </summary>
    private async void ActualisationRichPresence_Tick(object? sender, EventArgs e)
    {
        if (
            !ConfigurationConnexionEstComplete()
            || !_profilUtilisateurAccessible
            || _surveillanceRichPresenceEnCours
        )
        {
            return;
        }

        _surveillanceRichPresenceEnCours = true;

        try
        {
            UserSummaryV2? resume = await _serviceUtilisateurRetroAchievements.ObtenirResumeAsync(
                _configurationConnexion.Pseudo,
                _configurationConnexion.CleApiWeb
            );

            if (resume is null)
            {
                return;
            }

            _dernierResumeUtilisateurCharge = resume;

            EtatRichPresence etat = _serviceSondeRichPresence.Sonder(
                new DonneesCompteUtilisateur
                {
                    Profil = _dernierProfilUtilisateurCharge,
                    Resume = resume,
                },
                journaliser: false
            );

            string signatureEtat =
                $"{etat.StatutAffiche}|{etat.SousStatutAffiche}|{etat.IdentifiantDernierJeu}|{etat.DatePresenceBrute}|{etat.SourceRichPresence}";

            if (
                string.Equals(
                    _signatureDernierEtatRichPresence,
                    signatureEtat,
                    StringComparison.Ordinal
                )
            )
            {
                return;
            }

            _signatureDernierEtatRichPresence = signatureEtat;
            _dernierePresenceRiche = etat.MessageRichPresence;
            MettreAJourNoticeCompteEntete();

            if (
                _dernierEtatSondeLocaleEmulateurs?.EmulateurDetecte == true
                && _identifiantJeuLocalActif > 0
                && etat.IdentifiantDernierJeu != _identifiantJeuLocalActif
            )
            {
                return;
            }

            if (
                etat.IdentifiantDernierJeu > 0
                && etat.IdentifiantDernierJeu != _dernierIdentifiantJeuApi
                && !_chargementJeuEnCoursActif
            )
            {
                await ChargerJeuEnCoursAsync(false, true);
            }
        }
        catch
        {
            // Une erreur ponctuelle de sonde ne doit pas interrompre la surveillance continue.
        }
        finally
        {
            _surveillanceRichPresenceEnCours = false;
        }
    }

    /// <summary>
    /// Surveille localement les émulateurs connus pour déclencher un rafraîchissement ciblé plus tôt.
    /// </summary>
    private async void ActualisationSondeLocaleEmulateurs_Tick(object? sender, EventArgs e)
    {
        if (_surveillanceLocaleEmulateursEnCours)
        {
            return;
        }

        _surveillanceLocaleEmulateursEnCours = true;

        try
        {
            EtatSondeLocaleEmulateur etat = _serviceSondeLocaleEmulateurs.Sonder();
            _dernierEtatSondeLocaleEmulateurs = etat;
            MettreAJourNoticeCompteEntete();

            if (
                string.Equals(
                    _signatureDerniereSondeLocaleEmulateurs,
                    etat.Signature,
                    StringComparison.Ordinal
                )
            )
            {
                return;
            }

            _signatureDerniereSondeLocaleEmulateurs = etat.Signature;

            if (
                !ConfigurationConnexionEstComplete()
                || !_profilUtilisateurAccessible
                || !etat.EmulateurDetecte
            )
            {
                _signatureDernierJeuLocalResolut = string.Empty;
                _identifiantJeuLocalActif = 0;
                _titreJeuLocalActif = string.Empty;
                _identifiantJeuLocalResolutEnAttente = 0;
                _titreJeuLocalResolutEnAttente = string.Empty;
                MettreAJourNoticeCompteEntete();
                return;
            }

            AppliquerTitreJeuLocalProvisoire(etat);

            JeuLocalResolut? jeuResolutImmediate = _serviceResolutionJeuLocal.ResoudreDepuisJeuxRecents(
                etat.TitreJeuProbable,
                _dernierResumeUtilisateurCharge?.RecentlyPlayed ?? []
            );

            if (jeuResolutImmediate is not null)
            {
                string signatureResolutionImmediate =
                    $"{etat.Signature}|{jeuResolutImmediate.IdentifiantJeu}|{jeuResolutImmediate.Source}";

                if (
                    !string.Equals(
                        _signatureDernierJeuLocalResolut,
                        signatureResolutionImmediate,
                        StringComparison.Ordinal
                    )
                        || _dernierIdentifiantJeuApi != jeuResolutImmediate.IdentifiantJeu
                )
                {
                    _signatureDernierJeuLocalResolut = signatureResolutionImmediate;
                    _identifiantJeuLocalActif = jeuResolutImmediate.IdentifiantJeu;
                    _titreJeuLocalActif = jeuResolutImmediate.TitreRetroAchievements;

                    if (_chargementJeuEnCoursActif)
                    {
                        _identifiantJeuLocalResolutEnAttente = jeuResolutImmediate.IdentifiantJeu;
                        _titreJeuLocalResolutEnAttente =
                            jeuResolutImmediate.TitreRetroAchievements;
                        return;
                    }

                    ChargerJeuResolutLocal(
                        jeuResolutImmediate.IdentifiantJeu,
                        jeuResolutImmediate.TitreRetroAchievements
                    );
                }

                return;
            }

            JeuLocalResolut? jeuResolut = await ResoudreJeuLocalDepuisSondeAsync(etat);

            if (jeuResolut is not null)
            {
                string signatureResolution =
                    $"{etat.Signature}|{jeuResolut.IdentifiantJeu}|{jeuResolut.Source}";

                if (
                    string.Equals(
                        _signatureDernierJeuLocalResolut,
                        signatureResolution,
                        StringComparison.Ordinal
                    )
                        && _dernierIdentifiantJeuApi == jeuResolut.IdentifiantJeu
                )
                {
                    return;
                }

                _signatureDernierJeuLocalResolut = signatureResolution;
                _identifiantJeuLocalActif = jeuResolut.IdentifiantJeu;
                _titreJeuLocalActif = jeuResolut.TitreRetroAchievements;

                if (_chargementJeuEnCoursActif)
                {
                    _identifiantJeuLocalResolutEnAttente = jeuResolut.IdentifiantJeu;
                    _titreJeuLocalResolutEnAttente = jeuResolut.TitreRetroAchievements;
                    return;
                }

                ChargerJeuResolutLocal(
                    jeuResolut.IdentifiantJeu,
                    jeuResolut.TitreRetroAchievements
                );
                return;
            }

            _signatureDernierJeuLocalResolut = string.Empty;
        }
        catch
        {
            // Une erreur locale ponctuelle ne doit pas casser la surveillance continue.
        }
        finally
        {
            _surveillanceLocaleEmulateursEnCours = false;
        }
    }

    private async Task<JeuLocalResolut?> ResoudreJeuLocalDepuisSondeAsync(
        EtatSondeLocaleEmulateur etat
    )
    {
        string titreJeuLocal = etat.TitreJeuProbable?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(titreJeuLocal))
        {
            return null;
        }

        IReadOnlyList<RecentlyPlayedGameV2> jeuxRecents =
            _dernierResumeUtilisateurCharge?.RecentlyPlayed ?? [];

        if (jeuxRecents.Count == 0)
        {
            try
            {
                jeuxRecents =
                    await _serviceUtilisateurRetroAchievements.ObtenirJeuxRecemmentJouesAsync(
                        _configurationConnexion.Pseudo,
                        _configurationConnexion.CleApiWeb
                    );
            }
            catch
            {
                jeuxRecents = [];
            }
        }

        HashSet<int> identifiantsConsoleCandidats = [.. jeuxRecents.Select(jeu => jeu.IdentifiantConsole)];

        if (_dernieresDonneesJeuAffichees?.Jeu.IdentifiantConsole > 0)
        {
            identifiantsConsoleCandidats.Add(_dernieresDonneesJeuAffichees.Jeu.IdentifiantConsole);
        }

        if (identifiantsConsoleCandidats.Count == 0)
        {
            foreach (
                int identifiantConsole in await ObtenirIdentifiantsConsoleCandidatsParEmulateurAsync(
                    etat.NomEmulateur
                )
            )
            {
                identifiantsConsoleCandidats.Add(identifiantConsole);
            }
        }

        return await _serviceResolutionJeuLocal.ResoudreAsync(
            titreJeuLocal,
            jeuxRecents,
            identifiantsConsoleCandidats,
            (identifiantConsole, jetonAnnulation) =>
                _serviceCatalogueRetroAchievements.ObtenirJeuxSystemeAvecHashesAsync(
                    _configurationConnexion.CleApiWeb,
                    identifiantConsole,
                    jetonAnnulation
                )
        );
    }

    private async Task<IReadOnlyList<int>> ObtenirIdentifiantsConsoleCandidatsParEmulateurAsync(
        string nomEmulateur
    )
    {
        string[] aliasConsoles = ObtenirAliasConsolesDepuisEmulateur(nomEmulateur);

        if (aliasConsoles.Length == 0)
        {
            return [];
        }

        if (_consolesResolutionLocale.Count == 0)
        {
            try
            {
                _consolesResolutionLocale = await _serviceCatalogueRetroAchievements.ObtenirConsolesAsync(
                    _configurationConnexion.CleApiWeb
                );
            }
            catch
            {
                return [];
            }
        }

        List<int> identifiants = [];

        foreach (ConsoleV2 console in _consolesResolutionLocale)
        {
            string nomNormalise = NormaliserNomConsole(console.Name);

            if (string.IsNullOrWhiteSpace(nomNormalise))
            {
                continue;
            }

            if (
                aliasConsoles.Any(alias =>
                {
                    string aliasNormalise = NormaliserNomConsole(alias);
                    return !string.IsNullOrWhiteSpace(aliasNormalise)
                        && (
                            nomNormalise.Contains(aliasNormalise, StringComparison.Ordinal)
                            || aliasNormalise.Contains(nomNormalise, StringComparison.Ordinal)
                        );
                })
            )
            {
                identifiants.Add(console.Id);
            }
        }

        return [.. identifiants.Distinct()];
    }

    private static string[] ObtenirAliasConsolesDepuisEmulateur(string nomEmulateur)
    {
        if (string.IsNullOrWhiteSpace(nomEmulateur))
        {
            return [];
        }

        return nomEmulateur.Trim().ToLowerInvariant() switch
        {
            "flycast" => ["dreamcast", "naomi", "atomiswave"],
            "duckstation" => ["playstation", "ps1"],
            "pcsx2" => ["playstation 2", "ps2"],
            "ppsspp" => ["playstation portable", "psp"],
            "dolphin" => ["gamecube", "wii"],
            "project64" => ["nintendo 64", "n64"],
            _ => [],
        };
    }

    private static string NormaliserNomConsole(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return string.Empty;
        }

        return string.Concat(
            valeur
                .Trim()
                .ToLowerInvariant()
                .Where(caractere => char.IsLetterOrDigit(caractere))
        );
    }
}

let signatureEtatApplique = "";
let dernierMessageErreur = "";
let modeEdition = false;
let interactionEdition = null;
let observateurRedimensionnement = null;
let minuterieSauvegardeLayout = null;
let ecritureLayoutEnCours = Promise.resolve();
let miseAJourAnimationGrillePlanifiee = false;
let ajustementTitrePlanifie = false;

const etatAnimationGrille = {
  animationId: 0,
  pauseJusqua: 0,
  offset: 0,
  amplitude: 0,
  sens: 1,
  survol: false,
  cycleDebut: 0,
  dureeSegment: 0,
  offsetDepart: 0,
  offsetArrivee: 0,
};

const CLE_LAYOUT = "ra-compagnon-overlay-layout-v1";
const URL_LAYOUT = "layout.json";
const PARAMETRES_URL = new URLSearchParams(window.location.search);
const EST_MODE_PREVIEW = PARAMETRES_URL.has("preview");
const EST_MODE_OBS = PARAMETRES_URL.get("obs") === "1";
const SECTION_DEMANDEE = (PARAMETRES_URL.get("section") || "").trim().toLowerCase();
const LARGEUR_SECTION_DEMANDEE = lireParametreDimensionUrl("width");
const HAUTEUR_SECTION_DEMANDEE = lireParametreDimensionUrl("height");
const IDS_BLOCS_EDITABLES = [
  "entete-bloc",
  "succes-bloc",
  "grille-zone",
];
const DEFINITIONS_SECTIONS_OVERLAY = [
  { id: "entete-bloc", cle: "entete", libelle: "En-tête" },
  { id: "succes-bloc", cle: "succes-courant", libelle: "Rétrosuccès en cours" },
  { id: "grille-zone", cle: "grille-succes", libelle: "Rétrosuccès du jeu" },
];
const referencesLiensPreview = new Map();

function lireParametreDimensionUrl(nomParametre) {
  const valeur = Number.parseFloat(PARAMETRES_URL.get(nomParametre) || "");
  return Number.isFinite(valeur) && valeur > 0 ? valeur : null;
}

function obtenirCurseurRedimensionnement(zoneRedimensionnement) {
  const { haut, bas, gauche, droite } = zoneRedimensionnement;

  if ((haut && gauche) || (bas && droite)) {
    return "nwse-resize";
  }

  if ((haut && droite) || (bas && gauche)) {
    return "nesw-resize";
  }

  if (gauche || droite) {
    return "ew-resize";
  }

  if (haut || bas) {
    return "ns-resize";
  }

  return "move";
}

function determinerZoneRedimensionnement(rect, positionX, positionY) {
  const marge = lireNombreCss("--poignee-edition-taille", 18);
  const haut = positionY - rect.top <= marge;
  const bas = rect.bottom - positionY <= marge;
  const gauche = positionX - rect.left <= marge;
  const droite = rect.right - positionX <= marge;

  if (!haut && !bas && !gauche && !droite) {
    return null;
  }

  return { haut, bas, gauche, droite };
}

function obtenirElementsAnimationGrille() {
  return {
    zone: document.getElementById("grille-zone"),
    viewport: document.getElementById("grille-viewport"),
    grille: document.getElementById("grille"),
  };
}

function ajusterEcartGrille(viewport, grille) {
  if (!viewport || !grille) {
    return;
  }

  const largeurVisible = viewport.clientWidth;
  const tailleBadge = lireNombreCss("--badge-taille", 34);
  const ecartBase = lireNombreCss("--grille-ecart", 8);

  if (!(largeurVisible > 0) || !(tailleBadge > 0)) {
    grille.style.removeProperty("--grille-ecart-actuel");
    return;
  }

  const colonnes = Math.max(1, Math.floor((largeurVisible + ecartBase) / (tailleBadge + ecartBase)));

  if (colonnes <= 1) {
    grille.style.setProperty("--grille-ecart-actuel", "0px");
    return;
  }

  const espaceOccupeParBadges = colonnes * tailleBadge;
  const espaceRestant = Math.max(0, largeurVisible - espaceOccupeParBadges);
  const ecartCalcule = espaceRestant / (colonnes - 1);
  grille.style.setProperty("--grille-ecart-actuel", `${ecartCalcule.toFixed(2)}px`);
}

function calculerHauteurVisibleCibleGrille(zone) {
  const main = obtenirMain();
  const styleZone = getComputedStyle(zone);
  const paddingVertical =
    (Number.parseFloat(styleZone.paddingTop) || 0)
    + (Number.parseFloat(styleZone.paddingBottom) || 0);
  const hauteurInterneZone = Math.max(0, zone.clientHeight - paddingVertical);
  const hauteurParDefaut = lireNombreCss("--badge-taille", 34) * 2
    + lireNombreCss("--grille-ecart", 8)
    + 2;

  if (main?.classList.contains("layout-personnalise")) {
    return hauteurInterneZone > 0 ? hauteurInterneZone : hauteurParDefaut;
  }

  if (hauteurInterneZone <= 0) {
    return hauteurParDefaut;
  }

  return Math.min(hauteurInterneZone, hauteurParDefaut);
}

function lireDureeCssMilliseconds(nomVariable, valeurRepli) {
  const brut = getComputedStyle(document.documentElement).getPropertyValue(nomVariable).trim();

  if (!brut) {
    return valeurRepli;
  }

  const valeur = Number.parseFloat(brut);
  if (!Number.isFinite(valeur)) {
    return valeurRepli;
  }

  if (brut.endsWith("ms")) {
    return valeur;
  }

  if (brut.endsWith("s")) {
    return valeur * 1000;
  }

  return valeurRepli;
}

function appliquerOffsetAnimationGrille(offset) {
  const { grille } = obtenirElementsAnimationGrille();

  if (!grille) {
    return;
  }

  grille.style.transform = `translate3d(0, ${-offset}px, 0)`;
}

function preparerSegmentAnimationGrille(temps, offsetDepart, offsetArrivee) {
  const vitesse = lireNombreCss("--grille-vitesse-animation", 16);
  const distance = Math.abs(offsetArrivee - offsetDepart);
  const dureeSecondes = Math.min(20, Math.max(5.5, distance / vitesse));

  etatAnimationGrille.cycleDebut = temps;
  etatAnimationGrille.dureeSegment = dureeSecondes * 1000;
  etatAnimationGrille.offsetDepart = offsetDepart;
  etatAnimationGrille.offsetArrivee = offsetArrivee;
}

function arreterAnimationGrille(reinitialiserPosition = false) {
  if (etatAnimationGrille.animationId) {
    cancelAnimationFrame(etatAnimationGrille.animationId);
    etatAnimationGrille.animationId = 0;
  }

  etatAnimationGrille.pauseJusqua = 0;
  etatAnimationGrille.amplitude = 0;
  etatAnimationGrille.sens = 1;
  etatAnimationGrille.cycleDebut = 0;
  etatAnimationGrille.dureeSegment = 0;
  etatAnimationGrille.offsetDepart = 0;
  etatAnimationGrille.offsetArrivee = 0;

  if (reinitialiserPosition) {
    etatAnimationGrille.offset = 0;
    appliquerOffsetAnimationGrille(0);
  }
}

function animerGrille(temps) {
  const { zone, viewport, grille } = obtenirElementsAnimationGrille();

  if (
    !zone
    || !viewport
    || !grille
    || !zone.classList.contains("visible")
    || etatAnimationGrille.amplitude <= 0
  ) {
    arreterAnimationGrille(true);
    return;
  }

  etatAnimationGrille.animationId = requestAnimationFrame(animerGrille);

  if (etatAnimationGrille.survol || interactionEdition) {
    return;
  }

  if (etatAnimationGrille.pauseJusqua > temps) {
    return;
  }

  if (!etatAnimationGrille.cycleDebut || etatAnimationGrille.dureeSegment <= 0) {
    preparerSegmentAnimationGrille(
      temps,
      etatAnimationGrille.offset,
      etatAnimationGrille.sens > 0 ? etatAnimationGrille.amplitude : 0,
    );
  }

  const pause = lireDureeCssMilliseconds("--grille-pause-animation", 1200);
  const progressionBrute =
    etatAnimationGrille.dureeSegment <= 0
      ? 1
      : (temps - etatAnimationGrille.cycleDebut) / etatAnimationGrille.dureeSegment;
  const progression = Math.max(0, Math.min(1, progressionBrute));
  const prochainOffset = etatAnimationGrille.offsetDepart
    + ((etatAnimationGrille.offsetArrivee - etatAnimationGrille.offsetDepart) * progression);

  if (progression >= 1) {
    etatAnimationGrille.offset = etatAnimationGrille.offsetArrivee;
    appliquerOffsetAnimationGrille(etatAnimationGrille.offset);
    etatAnimationGrille.sens = etatAnimationGrille.offsetArrivee >= etatAnimationGrille.amplitude ? -1 : 1;
    etatAnimationGrille.pauseJusqua = temps + pause;
    etatAnimationGrille.cycleDebut = 0;
    etatAnimationGrille.dureeSegment = 0;
    etatAnimationGrille.offsetDepart = etatAnimationGrille.offset;
    etatAnimationGrille.offsetArrivee = etatAnimationGrille.sens > 0 ? etatAnimationGrille.amplitude : 0;
    return;
  }

  etatAnimationGrille.offset = prochainOffset;
  appliquerOffsetAnimationGrille(prochainOffset);
}

function demarrerAnimationGrille() {
  if (etatAnimationGrille.animationId || etatAnimationGrille.amplitude <= 0) {
    return;
  }

  etatAnimationGrille.pauseJusqua =
    performance.now() + lireDureeCssMilliseconds("--grille-pause-animation", 1200);
  etatAnimationGrille.cycleDebut = 0;
  etatAnimationGrille.dureeSegment = 0;
  etatAnimationGrille.offsetDepart = etatAnimationGrille.offset;
  etatAnimationGrille.offsetArrivee =
    etatAnimationGrille.sens > 0 ? etatAnimationGrille.amplitude : 0;
  etatAnimationGrille.animationId = requestAnimationFrame(animerGrille);
}

function mettreAJourAnimationGrille() {
  miseAJourAnimationGrillePlanifiee = false;

  const { zone, viewport, grille } = obtenirElementsAnimationGrille();
  if (!zone || !viewport || !grille || !zone.classList.contains("visible")) {
    arreterAnimationGrille(true);
    return;
  }

  viewport.style.height = `${calculerHauteurVisibleCibleGrille(zone)}px`;
  ajusterEcartGrille(viewport, grille);

  const seuil = lireNombreCss("--grille-seuil-animation", 4);
  const hauteurVisible = viewport.clientHeight;
  const hauteurContenu = grille.scrollHeight;
  const amplitude = Math.max(0, hauteurContenu - hauteurVisible);

  if (!(hauteurVisible > 0) || amplitude <= seuil) {
    arreterAnimationGrille(true);
    return;
  }

  etatAnimationGrille.amplitude = amplitude;
  etatAnimationGrille.offset = Math.max(0, Math.min(amplitude, etatAnimationGrille.offset));
  etatAnimationGrille.cycleDebut = 0;
  etatAnimationGrille.dureeSegment = 0;
  etatAnimationGrille.offsetDepart = etatAnimationGrille.offset;
  etatAnimationGrille.offsetArrivee =
    etatAnimationGrille.sens > 0 ? amplitude : 0;
  appliquerOffsetAnimationGrille(etatAnimationGrille.offset);
  demarrerAnimationGrille();
}

function programmerMiseAJourAnimationGrille() {
  if (miseAJourAnimationGrillePlanifiee) {
    return;
  }

  miseAJourAnimationGrillePlanifiee = true;
  requestAnimationFrame(() => {
    requestAnimationFrame(mettreAJourAnimationGrille);
  });
}

function construireSignatureEtat(etat) {
  if (!etat) {
    return "";
  }

  return JSON.stringify({
    misAJourUtc: etat?.misAJourUtc || "",
    jeu: etat?.jeu?.identifiantJeu || 0,
    succes: etat?.succesCourant?.identifiantSucces || 0,
    progression: etat?.progression?.valeur || 0,
    grille: Array.isArray(etat?.grilleSuccesJeu)
      ? etat.grilleSuccesJeu.map((badge) => [
          badge?.identifiantSucces || 0,
          !!badge?.estDebloque,
          !!badge?.estHardcore,
          !!badge?.estSelectionne,
          badge?.badge || "",
        ])
      : [],
    sync: etat?.etatSynchronisation || "",
  });
}

function appliquerGrilleSucces(badges) {
  const zone = document.getElementById("grille-zone");
  const grille = document.getElementById("grille");

  if (!zone || !grille) {
    return;
  }

  grille.innerHTML = "";

  if (!Array.isArray(badges) || badges.length === 0) {
    zone.classList.remove("visible");
    arreterAnimationGrille(true);
    return;
  }

  zone.classList.add("visible");

  for (const badge of badges) {
    const element = document.createElement("div");
    element.className = "badge";

    if (!badge?.estDebloque) {
      element.classList.add("verrouille");
    }

    if (badge?.estHardcore) {
      element.classList.add("hardcore");
    }

    if (badge?.estSelectionne) {
      element.classList.add("selectionne");
    }

    const info = [badge?.titre, badge?.description].filter(Boolean).join("\n");
    if (info) {
      element.title = info;
    }

    const image = document.createElement("img");
    image.alt = badge?.titre || "Badge";
    image.src = badge?.badge || "";
    image.loading = "eager";
    element.appendChild(image);
    grille.appendChild(element);
  }

  programmerMiseAJourAnimationGrille();
}

function normaliserUrlImageJeu(chemin) {
  const valeur = (chemin || "").trim();

  if (!valeur) {
    return "";
  }

  if (/^https?:\/\//i.test(valeur)) {
    return valeur;
  }

  if (valeur.startsWith("/")) {
    return `https://retroachievements.org${valeur}`;
  }

  return valeur;
}

function mesurerOccupationTitre(titre) {
  const style = getComputedStyle(titre);
  const taillePolice = Number.parseFloat(style.fontSize) || 0;
  const lineHeight =
    Number.parseFloat(style.lineHeight)
    || (taillePolice > 0 ? taillePolice * 1.02 : 0);
  const hauteur = titre.getBoundingClientRect().height;
  const lignes = lineHeight > 0 ? Math.max(1, Math.round(hauteur / lineHeight)) : 1;
  const estTronque = (titre.scrollHeight - titre.clientHeight) > 1;

  return { lignes, estTronque };
}

function trouverTailleTitre(titre, tailleMax, tailleMin, lignesMax) {
  for (let taille = tailleMax; taille >= tailleMin; taille -= 0.5) {
    titre.style.fontSize = `${taille}px`;
    const { lignes, estTronque } = mesurerOccupationTitre(titre);

    if (!estTronque && lignes <= lignesMax) {
      return taille;
    }
  }

  return null;
}

function ajusterTitreJeu() {
  ajustementTitrePlanifie = false;

  const titre = document.getElementById("jeu");
  if (!titre) {
    return;
  }

  titre.style.removeProperty("font-size");

  if (!titre.textContent?.trim()) {
    return;
  }

  const tailleMax = Number.parseFloat(getComputedStyle(titre).fontSize) || 0;
  const tailleMinUneLigne = lireNombreCss("--titre-jeu-taille-min-une-ligne", 20);
  const tailleMinDeuxLignes = lireNombreCss("--titre-jeu-taille-min-deux-lignes", 18);

  if (!(tailleMax > 0)) {
    return;
  }

  const tailleUneLigne = trouverTailleTitre(
    titre,
    tailleMax,
    Math.min(tailleMax, tailleMinUneLigne),
    1,
  );

  if (tailleUneLigne !== null) {
    titre.style.fontSize = `${tailleUneLigne}px`;
    return;
  }

  const tailleDeuxLignes = trouverTailleTitre(
    titre,
    tailleMax,
    Math.min(tailleMax, tailleMinDeuxLignes),
    2,
  );

  if (tailleDeuxLignes !== null) {
    titre.style.fontSize = `${tailleDeuxLignes}px`;
    return;
  }

  titre.style.fontSize = `${Math.min(tailleMax, tailleMinDeuxLignes)}px`;
}

function programmerAjustementTitreJeu() {
  if (ajustementTitrePlanifie) {
    return;
  }

  ajustementTitrePlanifie = true;
  requestAnimationFrame(() => {
    requestAnimationFrame(ajusterTitreJeu);
  });
}

function ajusterContenuSuccesCourant() {
  const blocSucces = document.getElementById("succes-bloc");
  if (!blocSucces) {
    return;
  }

  const largeur = blocSucces.clientWidth;
  const hauteur = blocSucces.clientHeight;
  if (!(largeur > 0) || !(hauteur > 0)) {
    return;
  }

  const echelleMinimale = EST_MODE_OBS ? 0.94 : 0.84;
  const largeurReference = EST_MODE_OBS ? 420 : 440;
  const hauteurReference = EST_MODE_OBS ? 155 : 165;
  const echelleLargeur = Math.min(1, Math.max(echelleMinimale, largeur / largeurReference));
  const echelleHauteur = Math.min(1, Math.max(echelleMinimale, hauteur / hauteurReference));
  const echelle = Math.max(echelleMinimale, Math.min(1, echelleLargeur, echelleHauteur));
  const modeCompact = hauteur <= 110;

  const tailleBadgeBase = lireNombreCss("--badge-succes-courant-taille", 72);
  const tailleSuccesBase = lireNombreCss("--succes-taille", 22);
  const tailleDescriptionBase = lireNombreCss("--description-taille", 15);
  const largeurDescriptionBase = lireNombreCss("--description-largeur-max", 46);
  const tailleBadgeMinimum = EST_MODE_OBS ? (modeCompact ? 42 : 60) : 48;
  const tailleSuccesMinimum = EST_MODE_OBS ? (modeCompact ? 18 : 22) : 18;
  const tailleDescriptionMinimum = EST_MODE_OBS ? (modeCompact ? 14 : 18) : 13;
  const largeurDescriptionMinimum = EST_MODE_OBS ? (modeCompact ? 24 : 34) : 28;

  blocSucces.style.setProperty(
    "--badge-succes-courant-taille-actuelle",
    `${Math.max(tailleBadgeMinimum, tailleBadgeBase * echelle).toFixed(2)}px`,
  );
  blocSucces.style.setProperty(
    "--succes-taille-actuelle",
    `${Math.max(tailleSuccesMinimum, tailleSuccesBase * echelle).toFixed(2)}px`,
  );
  blocSucces.style.setProperty(
    "--description-taille-actuelle",
    `${Math.max(tailleDescriptionMinimum, tailleDescriptionBase * echelle).toFixed(2)}px`,
  );
  blocSucces.style.setProperty(
    "--description-largeur-max-actuelle",
    `${Math.max(largeurDescriptionMinimum, largeurDescriptionBase * echelle).toFixed(2)}ch`,
  );
}

function obtenirMain() {
  return document.querySelector("main");
}

function normaliserStructureOverlay() {
  const main = obtenirMain();
  const colonneGauche = main.querySelector(".colonne-gauche");
  const colonneDroite = main.querySelector(".colonne-droite");
  const entete = document.getElementById("entete-bloc");
  const succes = colonneDroite?.querySelector(".bloc");
  const grille = document.getElementById("grille-zone");

  if (succes) {
    succes.id = "succes-bloc";
    succes.dataset.section = "Rétrosuccès en cours";
  }

  if (entete) {
    entete.dataset.section = "En-tête";
  }

  if (grille) {
    grille.dataset.section = "Rétrosuccès du jeu";
  }

  for (const bloc of [entete, succes, grille]) {
    if (bloc) {
      bloc.classList.add("bloc-editable");
    }
  }

  if (entete && entete.parentElement !== main) {
    main.insertBefore(entete, colonneGauche);
  }

  if (succes && succes.parentElement !== main) {
    main.insertBefore(succes, grille);
  }

  colonneGauche?.remove();
  colonneDroite?.remove();
}

function obtenirBlocsEditables() {
  return IDS_BLOCS_EDITABLES.map((id) => document.getElementById(id)).filter(Boolean);
}

function obtenirDefinitionsSectionsDisponibles() {
  return DEFINITIONS_SECTIONS_OVERLAY.map((definition) => ({
    ...definition,
    element: document.getElementById(definition.id),
  })).filter((definition) => definition.element);
}

function construireUrlOverlaySection(cleSection = "", largeur = null, hauteur = null) {
  const url = new URL(window.location.href);
  url.search = "";
  url.searchParams.set("obs", "1");

  if (cleSection) {
    url.searchParams.set("section", cleSection);
  }

  if (Number.isFinite(largeur) && largeur > 0) {
    url.searchParams.set("width", String(Math.round(largeur)));
  }

  if (Number.isFinite(hauteur) && hauteur > 0) {
    url.searchParams.set("height", String(Math.round(hauteur)));
  }

  return url.toString();
}

function appliquerRedimensionnementSectionDepuisPreview(
  definitionId,
  largeurDemandee,
  hauteurDemandee,
) {
  const bloc = definitionId ? document.getElementById(definitionId) : null;
  if (!bloc) {
    return;
  }

  const main = obtenirMain();
  const mainRect = main.getBoundingClientRect();
  const blocRect = bloc.getBoundingClientRect();
  const largeurMin = Math.max(180, lireDimensionMinimumBloc(bloc, "min-width", 180));
  const hauteurMin = Math.max(72, lireDimensionMinimumBloc(bloc, "min-height", 72));
  const layout = lireLayoutSauvegarde() ?? capturerLayoutDepuisDom();
  const item = layout[definitionId] ?? {
    left: blocRect.left - mainRect.left,
    top: blocRect.top - mainRect.top,
    width: blocRect.width,
    height: blocRect.height,
    visible: getComputedStyle(bloc).display !== "none",
  };

  if (Number.isFinite(largeurDemandee) && largeurDemandee > 0) {
    item.width = Math.max(largeurMin, Math.round(largeurDemandee));
  }

  if (Number.isFinite(hauteurDemandee) && hauteurDemandee > 0) {
    item.height = Math.max(hauteurMin, Math.round(hauteurDemandee));
  }

  item.visible = true;
  layout[definitionId] = item;

  sauvegarderLayoutLocal(layout);
  programmerSauvegardeLayoutDistant(layout);
  appliquerLayoutPersonnalise(layout);
  mettreAJourDimensionsLiensPreview();
}

function appliquerFiltreSectionSiNecessaire() {
  if (!SECTION_DEMANDEE) {
    return;
  }

  const main = obtenirMain();
  const toolbar = document.getElementById("toolbar-edition");
  const panneauPreview = document.getElementById("panneau-liens-preview");
  const definitions = obtenirDefinitionsSectionsDisponibles();
  const definitionCible = definitions.find((definition) => definition.cle === SECTION_DEMANDEE);

  if (!definitionCible) {
    return;
  }

  modeEdition = false;
  interactionEdition = null;
  document.body.classList.remove("mode-edition");
  document.body.classList.add("section-isolee");
  main.classList.remove("layout-personnalise", "mode-edition");
  main.style.removeProperty("width");
  main.style.removeProperty("height");
  main.style.removeProperty("minWidth");
  main.style.removeProperty("minHeight");

  for (const bloc of obtenirBlocsEditables()) {
    const estVisible = bloc.id === definitionCible.id;
    bloc.hidden = !estVisible;
    bloc.style.removeProperty("left");
    bloc.style.removeProperty("top");
    bloc.style.removeProperty("right");
    bloc.style.removeProperty("bottom");
    bloc.style.removeProperty("width");
    bloc.style.removeProperty("height");
    bloc.style.removeProperty("transform");
    bloc.style.removeProperty("position");
    bloc.style.removeProperty("cursor");
    bloc.style.removeProperty("minWidth");
    bloc.style.removeProperty("minHeight");

    if (estVisible) {
      bloc.style.removeProperty("display");
    } else {
      bloc.style.display = "none";
    }
  }

  definitionCible.element.hidden = false;
  definitionCible.element.style.removeProperty("display");

  if (LARGEUR_SECTION_DEMANDEE) {
    const largeur = Math.round(LARGEUR_SECTION_DEMANDEE);
    main.style.width = `${largeur}px`;
    definitionCible.element.style.setProperty("width", `${largeur}px`, "important");
    definitionCible.element.style.setProperty("min-width", `${largeur}px`, "important");
  }

  if (HAUTEUR_SECTION_DEMANDEE) {
    const hauteur = Math.round(HAUTEUR_SECTION_DEMANDEE);
    main.style.height = `${hauteur}px`;
    definitionCible.element.style.setProperty("height", `${hauteur}px`, "important");
    definitionCible.element.style.setProperty("min-height", `${hauteur}px`, "important");
  }

  if (toolbar) {
    toolbar.hidden = true;
  }

  if (panneauPreview) {
    panneauPreview.hidden = true;
  }
}

function initialiserPanneauLiensPreview() {
  const panneau = document.getElementById("panneau-liens-preview");
  const liste = document.getElementById("liste-liens-preview");

  if (!panneau || !liste || !EST_MODE_PREVIEW || SECTION_DEMANDEE) {
    return;
  }

  const liens = obtenirDefinitionsSectionsDisponibles().map((definition) => ({
    libelle: definition.libelle,
    url: construireUrlOverlaySection(definition.cle),
  }));

  liste.innerHTML = "";
  referencesLiensPreview.clear();

  for (const lien of liens) {
    const ligne = document.createElement("div");
    ligne.className = "lien-preview-item";
    const definition = obtenirDefinitionsSectionsDisponibles().find(
      (item) => construireUrlOverlaySection(item.cle) === lien.url,
    );

    const etiquette = document.createElement("div");
    etiquette.className = "lien-preview-item-label";
    etiquette.textContent = lien.libelle;

    const champ = document.createElement("input");
    champ.className = "lien-preview-item-url";
    champ.type = "text";
    champ.readOnly = true;
    champ.value = lien.url;
    champ.addEventListener("focus", () => champ.select());
    champ.addEventListener("click", () => champ.select());

    const rangee = document.createElement("div");
    rangee.className = "lien-preview-item-rangee";

    const controlesTaille = document.createElement("div");
    controlesTaille.className = "lien-preview-item-taille";

    const champLargeur = document.createElement("input");
    champLargeur.className = "lien-preview-item-dimension";
    champLargeur.type = "number";
    champLargeur.min = "1";
    champLargeur.step = "1";
    champLargeur.inputMode = "numeric";
    champLargeur.setAttribute("aria-label", `Largeur ${lien.libelle}`);
    champLargeur.title = "Largeur";

    const champHauteur = document.createElement("input");
    champHauteur.className = "lien-preview-item-dimension";
    champHauteur.type = "number";
    champHauteur.min = "1";
    champHauteur.step = "1";
    champHauteur.inputMode = "numeric";
    champHauteur.setAttribute("aria-label", `Hauteur ${lien.libelle}`);
    champHauteur.title = "Hauteur";

    const appliquerDimensions = () => {
      appliquerRedimensionnementSectionDepuisPreview(
        definition?.id || "",
        Number.parseFloat(champLargeur.value),
        Number.parseFloat(champHauteur.value),
      );
    };

    champLargeur.addEventListener("change", appliquerDimensions);
    champHauteur.addEventListener("change", appliquerDimensions);
    champLargeur.addEventListener("keydown", (evenement) => {
      if (evenement.key === "Enter") {
        appliquerDimensions();
      }
    });
    champHauteur.addEventListener("keydown", (evenement) => {
      if (evenement.key === "Enter") {
        appliquerDimensions();
      }
    });

    const boutonCopier = document.createElement("button");
    boutonCopier.className = "lien-preview-item-copier";
    boutonCopier.type = "button";
    boutonCopier.textContent = "Copier";
    boutonCopier.addEventListener("click", async () => {
      try {
        if (navigator.clipboard?.writeText) {
          await navigator.clipboard.writeText(champ.value);
        } else {
          champ.select();
          document.execCommand("copy");
        }

        boutonCopier.textContent = "Copié";
        window.setTimeout(() => {
          boutonCopier.textContent = "Copier";
        }, 1200);
      } catch {
        champ.select();
      }
    });

    controlesTaille.appendChild(champLargeur);
    controlesTaille.appendChild(champHauteur);
    rangee.appendChild(controlesTaille);
    rangee.appendChild(champ);
    rangee.appendChild(boutonCopier);
    ligne.appendChild(etiquette);
    ligne.appendChild(rangee);
    liste.appendChild(ligne);

    referencesLiensPreview.set(lien.url, {
      cle: lien.url,
      etiquette,
      champ,
      champLargeur,
      champHauteur,
      libelle: lien.libelle,
      definitionId: definition?.id || "",
      sectionCle: definition?.cle || "",
    });
  }

  panneau.hidden = false;
  mettreAJourDimensionsLiensPreview();
}

function mettreAJourDimensionsLiensPreview() {
  if (!EST_MODE_PREVIEW || SECTION_DEMANDEE || referencesLiensPreview.size === 0) {
    return;
  }

  for (const reference of referencesLiensPreview.values()) {
    const element = reference.definitionId
      ? document.getElementById(reference.definitionId)
      : null;
    if (!element || !reference.etiquette || !reference.champ) {
      continue;
    }

    const rect = element.getBoundingClientRect();
    const largeur = Math.max(0, Math.round(rect.width));
    const hauteur = Math.max(0, Math.round(rect.height));
    reference.champ.value = construireUrlOverlaySection(reference.sectionCle, largeur, hauteur);
    if (reference.champLargeur && document.activeElement !== reference.champLargeur) {
      reference.champLargeur.value = String(largeur);
    }

    if (reference.champHauteur && document.activeElement !== reference.champHauteur) {
      reference.champHauteur.value = String(hauteur);
    }
    reference.etiquette.textContent = `${reference.libelle} · ${largeur} x ${hauteur}`;
  }
}

function calculerRayonDynamiqueBloc(bloc) {
  const rect = bloc.getBoundingClientRect();
  const coteCourt = Math.min(rect.width, rect.height);

  if (!(coteCourt > 0)) {
    return null;
  }

  const rayonMin = lireNombreCss("--bloc-rayon-min", 4);
  const rayonMax = lireNombreCss("--bloc-rayon-max", 10);
  const rayonProportionnel = coteCourt / 1.618;

  return Math.max(rayonMin, Math.min(rayonMax, rayonProportionnel));
}

function appliquerRayonDynamiqueBloc(bloc) {
  const rayon = calculerRayonDynamiqueBloc(bloc);

  if (rayon === null) {
    bloc.style.removeProperty("--bloc-rayon-dynamique");
    return;
  }

  bloc.style.setProperty("--bloc-rayon-dynamique", `${rayon.toFixed(2)}px`);
}

function appliquerRayonsDynamiques() {
  for (const bloc of obtenirBlocsEditables()) {
    appliquerRayonDynamiqueBloc(bloc);
  }

  mettreAJourDimensionsLiensPreview();
}

function lireNombreCss(nomVariable, valeurRepli) {
  const brut = getComputedStyle(document.documentElement).getPropertyValue(nomVariable).trim();
  const valeur = Number.parseFloat(brut);
  return Number.isFinite(valeur) ? valeur : valeurRepli;
}

function lireDimensionMinimumBloc(bloc, propriete, valeurRepli) {
  const valeurInline = Number.parseFloat(bloc.style.getPropertyValue(propriete));
  if (Number.isFinite(valeurInline)) {
    return valeurInline;
  }

  const styleCalcule = getComputedStyle(bloc);
  const valeurCalculee = Number.parseFloat(styleCalcule.getPropertyValue(propriete));
  return Number.isFinite(valeurCalculee) ? valeurCalculee : valeurRepli;
}

function lireLayoutSauvegarde() {
  try {
    const brut = localStorage.getItem(CLE_LAYOUT);
    if (!brut) {
      return null;
    }

    const layout = JSON.parse(brut);
    return layout && typeof layout === "object" ? layout : null;
  } catch {
    return null;
  }
}

function sauvegarderLayoutLocal(layout) {
  try {
    localStorage.setItem(CLE_LAYOUT, JSON.stringify(layout));
  } catch {
  }
}

async function lireLayoutDistant() {
  const reponse = await fetch(`${URL_LAYOUT}?cache=${Date.now()}`, { cache: "no-store" });
  if (!reponse.ok) {
    throw new Error("layout.json introuvable");
  }

  return await reponse.json();
}

function programmerSauvegardeLayoutDistant(layout) {
  if (minuterieSauvegardeLayout) {
    clearTimeout(minuterieSauvegardeLayout);
  }

  minuterieSauvegardeLayout = setTimeout(() => {
    const contenu = JSON.stringify(layout);

    ecritureLayoutEnCours = ecritureLayoutEnCours
      .catch(() => {})
      .then(() =>
        fetch(URL_LAYOUT, {
          method: "POST",
          cache: "no-store",
          headers: {
            "Content-Type": "application/json; charset=utf-8",
          },
          body: contenu,
        }),
      )
      .catch(() => {});
  }, 180);
}

function supprimerLayoutSauvegarde() {
  try {
    localStorage.removeItem(CLE_LAYOUT);
  } catch {
  }
}

function capturerLayoutDepuisDom() {
  const main = obtenirMain();
  const mainRect = main.getBoundingClientRect();
  const layout = {};

  for (const bloc of obtenirBlocsEditables()) {
    const rect = bloc.getBoundingClientRect();
    const style = getComputedStyle(bloc);

    layout[bloc.id] = {
      left: rect.left - mainRect.left,
      top: rect.top - mainRect.top,
      width: rect.width,
      height: rect.height,
      visible: style.display !== "none",
    };
  }

  return layout;
}

function mettreAJourTailleMainDepuisLayout(layout) {
  const main = obtenirMain();
  const padding = lireNombreCss("--overlay-padding", 22);
  let largeur = 0;
  let hauteur = 0;

  for (const bloc of obtenirBlocsEditables()) {
    const item = layout[bloc.id];
    if (!item || item.visible === false) {
      continue;
    }

    largeur = Math.max(largeur, item.left + item.width);
    hauteur = Math.max(hauteur, item.top + item.height);
  }

  main.style.width = `${Math.max(lireNombreCss("--overlay-min-width", 620), largeur + padding)}px`;
  main.style.height = `${Math.max(1, hauteur + padding)}px`;
}

function appliquerLayoutPersonnalise(layout) {
  const main = obtenirMain();

  if (!layout) {
    main.classList.remove("layout-personnalise");
    main.style.removeProperty("width");
    main.style.removeProperty("height");

    for (const bloc of obtenirBlocsEditables()) {
      bloc.style.removeProperty("left");
      bloc.style.removeProperty("top");
      bloc.style.removeProperty("width");
      bloc.style.removeProperty("height");
    }

    appliquerRayonsDynamiques();
    programmerAjustementTitreJeu();
    ajusterContenuSuccesCourant();
    programmerMiseAJourAnimationGrille();
    return;
  }

  main.classList.add("layout-personnalise");

  for (const bloc of obtenirBlocsEditables()) {
    const item = layout[bloc.id];
    if (!item) {
      continue;
    }

    bloc.style.left = `${item.left}px`;
    bloc.style.top = `${item.top}px`;
    bloc.style.width = `${item.width}px`;
    bloc.style.height = `${item.height}px`;
  }

  mettreAJourTailleMainDepuisLayout(layout);
  appliquerRayonsDynamiques();
  programmerAjustementTitreJeu();
  ajusterContenuSuccesCourant();
  programmerMiseAJourAnimationGrille();
}

function sauvegarderLayoutDepuisDom() {
  const layout = capturerLayoutDepuisDom();
  sauvegarderLayoutLocal(layout);
  programmerSauvegardeLayoutDistant(layout);
  appliquerLayoutPersonnalise(layout);
}

function garantirContraintesBloc(bloc) {
  const largeurMin = Math.max(180, lireDimensionMinimumBloc(bloc, "min-width", 180));
  const hauteurMin = Math.max(72, lireDimensionMinimumBloc(bloc, "min-height", 72));
  const largeur = bloc.getBoundingClientRect().width;
  const hauteur = bloc.getBoundingClientRect().height;

  if (largeur < largeurMin) {
    bloc.style.width = `${largeurMin}px`;
  }

  if (hauteur < hauteurMin) {
    bloc.style.height = `${hauteurMin}px`;
  }
}

function initialiserObservateurRedimensionnement() {
  if (typeof ResizeObserver === "undefined" || observateurRedimensionnement) {
    return;
  }

  observateurRedimensionnement = new ResizeObserver((entrees) => {
    appliquerRayonsDynamiques();
    programmerAjustementTitreJeu();
    ajusterContenuSuccesCourant();
    programmerMiseAJourAnimationGrille();

    if (!modeEdition) {
      return;
    }

    const redimensionnementBlocEditable = entrees.some((entree) =>
      entree.target instanceof HTMLElement
      && entree.target.classList.contains("bloc-editable")
    );

    if (!redimensionnementBlocEditable) {
      return;
    }

    for (const bloc of obtenirBlocsEditables()) {
      garantirContraintesBloc(bloc);
    }

    sauvegarderLayoutDepuisDom();
  });

  for (const bloc of obtenirBlocsEditables()) {
    observateurRedimensionnement.observe(bloc);
  }

  const { viewport, grille } = obtenirElementsAnimationGrille();
  if (viewport) {
    observateurRedimensionnement.observe(viewport);
  }

  if (grille) {
    observateurRedimensionnement.observe(grille);
  }
}

function activerModeEdition() {
  let layout = lireLayoutSauvegarde();

  if (!layout) {
    layout = capturerLayoutDepuisDom();
    sauvegarderLayoutLocal(layout);
    programmerSauvegardeLayoutDistant(layout);
  }

  appliquerLayoutPersonnalise(layout);
  modeEdition = true;
  document.body.classList.add("mode-edition");
  obtenirMain().classList.add("mode-edition");
  document.getElementById("quitter-edition").textContent = "Désactiver";
}

function quitterModeEdition() {
  if (modeEdition) {
    for (const bloc of obtenirBlocsEditables()) {
      garantirContraintesBloc(bloc);
    }

    const layout = capturerLayoutDepuisDom();
    sauvegarderLayoutLocal(layout);
    programmerSauvegardeLayoutDistant(layout);
  }

  modeEdition = false;
  document.body.classList.remove("mode-edition");
  obtenirMain().classList.remove("mode-edition");
  appliquerLayoutPersonnalise(lireLayoutSauvegarde());
  document.getElementById("quitter-edition").textContent = "Modifier les sections";
}

function basculerModeEdition() {
  if (modeEdition) {
    quitterModeEdition();
  } else {
    activerModeEdition();
  }
}

function reinitialiserLayoutPersonnalise() {
  supprimerLayoutSauvegarde();
  appliquerLayoutPersonnalise(null);

  if (modeEdition) {
    requestAnimationFrame(() => {
      const layout = capturerLayoutDepuisDom();
      sauvegarderLayoutLocal(layout);
      programmerSauvegardeLayoutDistant(layout);
      appliquerLayoutPersonnalise(layout);
    });
  }
}

function initialiserEdition() {
  const main = obtenirMain();
  normaliserStructureOverlay();
  appliquerRayonsDynamiques();
  initialiserObservateurRedimensionnement();
  const { grille, viewport } = obtenirElementsAnimationGrille();

  document.getElementById("quitter-edition").addEventListener("click", basculerModeEdition);
  document
    .getElementById("reinitialiser-layout")
    .addEventListener("click", reinitialiserLayoutPersonnalise);

  viewport?.addEventListener("pointerenter", () => {
    etatAnimationGrille.survol = true;
  });

  viewport?.addEventListener("pointerleave", () => {
    etatAnimationGrille.survol = false;
  });

  grille?.addEventListener("focusin", () => {
    etatAnimationGrille.survol = true;
  });

  grille?.addEventListener("focusout", () => {
    etatAnimationGrille.survol = false;
  });

  quitterModeEdition();
  programmerAjustementTitreJeu();
  ajusterContenuSuccesCourant();

  window.addEventListener("resize", programmerAjustementTitreJeu);
  window.addEventListener("resize", ajusterContenuSuccesCourant);
  window.addEventListener("resize", mettreAJourDimensionsLiensPreview);

  main.addEventListener("pointerdown", (evenement) => {
    if (!modeEdition) {
      return;
    }

    const bloc = evenement.target.closest(".bloc-editable");
    if (!bloc) {
      return;
    }

    const rect = bloc.getBoundingClientRect();
    const mainRect = main.getBoundingClientRect();
    const zoneRedimensionnement = determinerZoneRedimensionnement(
      rect,
      evenement.clientX,
      evenement.clientY,
    );

    if (zoneRedimensionnement) {
      interactionEdition = {
        type: "resize",
        bloc,
        zoneRedimensionnement,
        origineX: evenement.clientX,
        origineY: evenement.clientY,
        origineLeft: rect.left - mainRect.left,
        origineTop: rect.top - mainRect.top,
        origineWidth: rect.width,
        origineHeight: rect.height,
      };

      bloc.style.cursor = obtenirCurseurRedimensionnement(zoneRedimensionnement);
      bloc.setPointerCapture(evenement.pointerId);
      evenement.preventDefault();
      return;
    }

    interactionEdition = {
      type: "move",
      bloc,
      decalageX: evenement.clientX - rect.left,
      decalageY: evenement.clientY - rect.top,
    };

    bloc.setPointerCapture(evenement.pointerId);
    evenement.preventDefault();
  });

  main.addEventListener("pointermove", (evenement) => {
    if (!modeEdition) {
      return;
    }

    if (!interactionEdition) {
      const blocSurvole = evenement.target.closest(".bloc-editable");
      if (!blocSurvole) {
        return;
      }

      const rect = blocSurvole.getBoundingClientRect();
      const zoneRedimensionnement = determinerZoneRedimensionnement(
        rect,
        evenement.clientX,
        evenement.clientY,
      );
      blocSurvole.style.cursor = zoneRedimensionnement
        ? obtenirCurseurRedimensionnement(zoneRedimensionnement)
        : "move";
      return;
    }

    const mainRect = main.getBoundingClientRect();
    const bloc = interactionEdition.bloc;
    const largeurMin = Math.max(180, lireDimensionMinimumBloc(bloc, "min-width", 180));
    const hauteurMin = Math.max(72, lireDimensionMinimumBloc(bloc, "min-height", 72));

    if (interactionEdition.type === "resize") {
      const deltaX = evenement.clientX - interactionEdition.origineX;
      const deltaY = evenement.clientY - interactionEdition.origineY;
      const zoneRedimensionnement = interactionEdition.zoneRedimensionnement;

      let gauche = interactionEdition.origineLeft;
      let haut = interactionEdition.origineTop;
      let largeur = interactionEdition.origineWidth;
      let hauteur = interactionEdition.origineHeight;

      if (zoneRedimensionnement.gauche) {
        gauche = interactionEdition.origineLeft + deltaX;
        largeur = interactionEdition.origineWidth - deltaX;
      }

      if (zoneRedimensionnement.droite) {
        largeur = interactionEdition.origineWidth + deltaX;
      }

      if (zoneRedimensionnement.haut) {
        haut = interactionEdition.origineTop + deltaY;
        hauteur = interactionEdition.origineHeight - deltaY;
      }

      if (zoneRedimensionnement.bas) {
        hauteur = interactionEdition.origineHeight + deltaY;
      }

      if (largeur < largeurMin) {
        if (zoneRedimensionnement.gauche) {
          gauche -= largeurMin - largeur;
        }
        largeur = largeurMin;
      }

      if (hauteur < hauteurMin) {
        if (zoneRedimensionnement.haut) {
          haut -= hauteurMin - hauteur;
        }
        hauteur = hauteurMin;
      }

      if (gauche < 0) {
        if (zoneRedimensionnement.gauche) {
          largeur = Math.max(largeurMin, largeur + gauche);
        }
        gauche = 0;
      }

      if (haut < 0) {
        if (zoneRedimensionnement.haut) {
          hauteur = Math.max(hauteurMin, hauteur + haut);
        }
        haut = 0;
      }

      bloc.style.left = `${gauche}px`;
      bloc.style.top = `${haut}px`;
      bloc.style.width = `${largeur}px`;
      bloc.style.height = `${hauteur}px`;

      mettreAJourTailleMainDepuisLayout(capturerLayoutDepuisDom());
      appliquerRayonDynamiqueBloc(bloc);
      programmerMiseAJourAnimationGrille();
      return;
    }

    bloc.style.left = `${Math.max(0, evenement.clientX - mainRect.left - interactionEdition.decalageX)}px`;
    bloc.style.top = `${Math.max(0, evenement.clientY - mainRect.top - interactionEdition.decalageY)}px`;

    mettreAJourTailleMainDepuisLayout(capturerLayoutDepuisDom());
    appliquerRayonDynamiqueBloc(bloc);
    programmerMiseAJourAnimationGrille();
  });

  document.addEventListener("pointerup", (evenement) => {
    if (interactionEdition) {
      interactionEdition.bloc.style.cursor = modeEdition ? "move" : "";

      if (interactionEdition.bloc.hasPointerCapture?.(evenement.pointerId)) {
        interactionEdition.bloc.releasePointerCapture(evenement.pointerId);
      }

      interactionEdition = null;
    }

    if (modeEdition) {
      for (const bloc of obtenirBlocsEditables()) {
        garantirContraintesBloc(bloc);
      }

      sauvegarderLayoutDepuisDom();
    }
  });
}

function appliquerEtat(etat) {
  const enteteBloc = document.getElementById("entete-bloc");
  const imageConsole = document.getElementById("image-console");
  const badgeSuccesCourant = document.getElementById("badge-succes-courant");
  const progression = etat?.progression?.resume || "";
  const pourcentage = etat?.progression?.pourcentage || "";
  const imageFond = normaliserUrlImageJeu(etat?.jeu?.image);
  const iconeConsole = normaliserUrlImageJeu(etat?.jeu?.imageConsole);
  const imageBadgeSuccesCourant = normaliserUrlImageJeu(etat?.succesCourant?.badge);

  document.getElementById("jeu").textContent = etat?.jeu?.titre || "Aucun jeu";
  document.getElementById("progression").textContent = progression;
  document.getElementById("pourcentage").textContent = pourcentage;
  document.getElementById("barre").style.width = `${Math.max(0, Math.min(100, etat?.progression?.valeur || 0))}%`;
  document.getElementById("succes").textContent = etat?.succesCourant?.titre || "";
  document.getElementById("description").textContent = etat?.succesCourant?.description || "";
  badgeSuccesCourant.src = imageBadgeSuccesCourant;
  badgeSuccesCourant.style.visibility = imageBadgeSuccesCourant ? "visible" : "hidden";
  appliquerGrilleSucces(etat?.grilleSuccesJeu);
  enteteBloc.style.setProperty(
    "--image-fond-entete",
    imageFond ? `url("${imageFond}")` : "none",
  );
  imageConsole.src = iconeConsole;
  imageConsole.classList.toggle("visible", !!iconeConsole);
  appliquerRayonsDynamiques();
  programmerAjustementTitreJeu();
  ajusterContenuSuccesCourant();
  programmerMiseAJourAnimationGrille();
  dernierMessageErreur = "";
}

function afficherErreur(message) {
  if (dernierMessageErreur === message) {
    return;
  }

  document.getElementById("jeu").textContent = "Données OBS indisponibles";
  document.getElementById("progression").textContent = "";
  document.getElementById("pourcentage").textContent = "";
  document.getElementById("barre").style.width = "0%";
  document.getElementById("succes").textContent = "Overlay indisponible";
  document.getElementById("description").textContent = message;
  document.getElementById("entete-bloc").style.setProperty("--image-fond-entete", "none");
  document.getElementById("image-console").removeAttribute("src");
  document.getElementById("image-console").classList.remove("visible");
  document.getElementById("badge-succes-courant").removeAttribute("src");
  document.getElementById("badge-succes-courant").style.visibility = "hidden";
  appliquerGrilleSucces([]);
  appliquerRayonsDynamiques();
  programmerAjustementTitreJeu();
  ajusterContenuSuccesCourant();
  programmerMiseAJourAnimationGrille();
  dernierMessageErreur = message;
}

function forcerRenduEtat(etat) {
  const racine = document.querySelector("main");

  if (racine) {
    racine.style.visibility = "hidden";
  }

  appliquerEtat(etat);

  if (racine) {
    void racine.offsetHeight;
    racine.style.visibility = "visible";
  }

  mettreAJourDimensionsLiensPreview();
}

async function lireEtatJsonAvecFetch() {
  const reponse = await fetch(`state.json?cache=${Date.now()}`, { cache: "no-store" });
  if (!reponse.ok) {
    throw new Error("state.json introuvable");
  }

  return await reponse.json();
}

function lireEtatJsonAvecXhr() {
  return new Promise((resolve, reject) => {
    const requete = new XMLHttpRequest();
    requete.open("GET", `state.json?cache=${Date.now()}`, true);
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

      reject(new Error("state.json introuvable"));
    };
    requete.onerror = () => reject(new Error("Lecture locale de state.json bloquée"));
    requete.send();
  });
}

async function chargerLayoutInitial() {
  try {
    const layout = await lireLayoutDistant();
    if (layout && typeof layout === "object" && Object.keys(layout).length > 0) {
      sauvegarderLayoutLocal(layout);
      return layout;
    }
  } catch {
  }

  return lireLayoutSauvegarde();
}

async function rafraichir() {
  try {
    const etat = await lireEtatJsonAvecFetch();
    const signature = construireSignatureEtat(etat);

    if (signature !== signatureEtatApplique) {
      signatureEtatApplique = signature;
      forcerRenduEtat(etat);
    }
  } catch {
    try {
      const etat = await lireEtatJsonAvecXhr();
      const signature = construireSignatureEtat(etat);

      if (signature !== signatureEtatApplique) {
        signatureEtatApplique = signature;
        forcerRenduEtat(etat);
      }
    } catch {
      afficherErreur(
        "Impossible de lire state.json. Vérifie que index.html et state.json sont dans le même dossier OBS.",
      );
    }
  }
}

async function initialiserOverlay() {
  document.body.classList.toggle("obs-mode", EST_MODE_OBS);
  initialiserEdition();

  const layout = await chargerLayoutInitial();
  if (layout) {
    appliquerLayoutPersonnalise(layout);
  }

  appliquerFiltreSectionSiNecessaire();
  initialiserPanneauLiensPreview();

  await rafraichir();
  setInterval(rafraichir, 1000);
}

initialiserOverlay();

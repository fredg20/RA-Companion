let signatureEtatApplique = "";
let dernierMessageErreur = "";
let modeEdition = false;
let interactionEdition = null;
let observateurRedimensionnement = null;
let minuterieSauvegardeLayout = null;
let ecritureLayoutEnCours = Promise.resolve();
let miseAJourAnimationGrillePlanifiee = false;

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
const IDS_BLOCS_EDITABLES = [
  "entete-bloc",
  "succes-bloc",
  "grille-zone",
];

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

function calculerRayonDynamiqueBloc(bloc) {
  const rect = bloc.getBoundingClientRect();
  const coteCourt = Math.min(rect.width, rect.height);

  if (!(coteCourt > 0)) {
    return null;
  }

  const rayonMin = lireNombreCss("--rayon-or-petit", 8);
  const rayonMax = lireNombreCss("--rayon-or-grand", 21);
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
}

function lireNombreCss(nomVariable, valeurRepli) {
  const brut = getComputedStyle(document.documentElement).getPropertyValue(nomVariable).trim();
  const valeur = Number.parseFloat(brut);
  return Number.isFinite(valeur) ? valeur : valeurRepli;
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
  programmerMiseAJourAnimationGrille();
}

function sauvegarderLayoutDepuisDom() {
  const layout = capturerLayoutDepuisDom();
  sauvegarderLayoutLocal(layout);
  programmerSauvegardeLayoutDistant(layout);
  appliquerLayoutPersonnalise(layout);
}

function garantirContraintesBloc(bloc) {
  const largeurMin = Math.max(180, Number.parseFloat(bloc.style.minWidth) || 180);
  const hauteurMin = Math.max(72, Number.parseFloat(bloc.style.minHeight) || 72);
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
    const largeurMin = Math.max(180, Number.parseFloat(bloc.style.minWidth) || 180);
    const hauteurMin = Math.max(72, Number.parseFloat(bloc.style.minHeight) || 72);

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
  initialiserEdition();

  const layout = await chargerLayoutInitial();
  if (layout) {
    appliquerLayoutPersonnalise(layout);
  }

  await rafraichir();
  setInterval(rafraichir, 1000);
}

initialiserOverlay();

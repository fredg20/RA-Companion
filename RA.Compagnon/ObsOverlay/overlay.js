let signatureEtatApplique = "";
let dernierMessageErreur = "";
let modeEdition = false;
let interactionEdition = null;
let observateurRedimensionnement = null;

const CLE_LAYOUT = "ra-compagnon-overlay-layout-v1";
const IDS_BLOCS_EDITABLES = [
  "entete-bloc",
  "progression-bloc",
  "succes-bloc",
  "grille-zone",
];

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
  const info = document.getElementById("grille-info");
  grille.innerHTML = "";

  if (!Array.isArray(badges) || badges.length === 0) {
    zone.classList.remove("visible");
    info.textContent = "";
    return;
  }

  zone.classList.add("visible");
  info.textContent = badges.length === 1 ? "1 badge" : `${badges.length} badges`;

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
  const progression = colonneGauche?.querySelector(".progression-bloc");
  const succes = colonneDroite?.querySelector(".bloc");
  const grille = document.getElementById("grille-zone");

  if (progression) {
    progression.id = "progression-bloc";
    progression.dataset.section = "Progression";
  }

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

  for (const bloc of [entete, progression, succes, grille]) {
    if (bloc) {
      bloc.classList.add("bloc-editable");
    }
  }

  if (entete && entete.parentElement !== main) {
    main.insertBefore(entete, colonneGauche);
  }

  if (progression && progression.parentElement !== main) {
    main.insertBefore(progression, colonneDroite ?? grille);
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

function sauvegarderLayout(layout) {
  try {
    localStorage.setItem(CLE_LAYOUT, JSON.stringify(layout));
  } catch {
  }
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
}

function sauvegarderLayoutDepuisDom() {
  const layout = capturerLayoutDepuisDom();
  sauvegarderLayout(layout);
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

  observateurRedimensionnement = new ResizeObserver(() => {
    if (!modeEdition) {
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
}

function activerModeEdition() {
  let layout = lireLayoutSauvegarde();

  if (!layout) {
    layout = capturerLayoutDepuisDom();
    sauvegarderLayout(layout);
  }

  appliquerLayoutPersonnalise(layout);
  modeEdition = true;
  document.body.classList.add("mode-edition");
  obtenirMain().classList.add("mode-edition");
}

function quitterModeEdition() {
  modeEdition = false;
  document.body.classList.remove("mode-edition");
  obtenirMain().classList.remove("mode-edition");
  appliquerLayoutPersonnalise(lireLayoutSauvegarde());
}

function reinitialiserLayoutPersonnalise() {
  supprimerLayoutSauvegarde();
  appliquerLayoutPersonnalise(null);

  if (modeEdition) {
    requestAnimationFrame(() => {
      const layout = capturerLayoutDepuisDom();
      sauvegarderLayout(layout);
      appliquerLayoutPersonnalise(layout);
    });
  }
}

function initialiserEdition() {
  const main = obtenirMain();
  normaliserStructureOverlay();
  initialiserObservateurRedimensionnement();

  const layout = lireLayoutSauvegarde();
  if (layout) {
    appliquerLayoutPersonnalise(layout);
  }

  document.getElementById("quitter-edition").addEventListener("click", quitterModeEdition);
  document
    .getElementById("reinitialiser-layout")
    .addEventListener("click", reinitialiserLayoutPersonnalise);

  document.addEventListener("keydown", (evenement) => {
    if (evenement.altKey && evenement.key.toLowerCase() === "e") {
      evenement.preventDefault();

      if (modeEdition) {
        quitterModeEdition();
      } else {
        activerModeEdition();
      }
    }
  });

  main.addEventListener("pointerdown", (evenement) => {
    if (!modeEdition) {
      return;
    }

    const bloc = evenement.target.closest(".bloc-editable");
    if (!bloc) {
      return;
    }

    const rect = bloc.getBoundingClientRect();
    const margeRedimensionnement = lireNombreCss("--poignee-edition-taille", 18);
    const procheCoinBasDroite =
      rect.right - evenement.clientX <= margeRedimensionnement &&
      rect.bottom - evenement.clientY <= margeRedimensionnement;

    if (procheCoinBasDroite) {
      return;
    }

    interactionEdition = {
      bloc,
      decalageX: evenement.clientX - rect.left,
      decalageY: evenement.clientY - rect.top,
    };

    bloc.setPointerCapture(evenement.pointerId);
    evenement.preventDefault();
  });

  main.addEventListener("pointermove", (evenement) => {
    if (!interactionEdition) {
      return;
    }

    const mainRect = main.getBoundingClientRect();
    const bloc = interactionEdition.bloc;

    bloc.style.left = `${Math.max(0, evenement.clientX - mainRect.left - interactionEdition.decalageX)}px`;
    bloc.style.top = `${Math.max(0, evenement.clientY - mainRect.top - interactionEdition.decalageY)}px`;

    mettreAJourTailleMainDepuisLayout(capturerLayoutDepuisDom());
  });

  document.addEventListener("pointerup", (evenement) => {
    if (interactionEdition) {
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
  const progression = etat?.progression?.resume || "";
  const pourcentage = etat?.progression?.pourcentage || "";
  const imageFond = normaliserUrlImageJeu(etat?.jeu?.image);
  const iconeConsole = normaliserUrlImageJeu(etat?.jeu?.imageConsole);

  document.getElementById("jeu").textContent = etat?.jeu?.titre || "Aucun jeu";
  document.getElementById("progression").textContent = progression;
  document.getElementById("pourcentage").textContent = pourcentage;
  document.getElementById("barre").style.width = `${Math.max(0, Math.min(100, etat?.progression?.valeur || 0))}%`;
  document.getElementById("succes").textContent = etat?.succesCourant?.titre || "";
  document.getElementById("description").textContent = etat?.succesCourant?.description || "";
  appliquerGrilleSucces(etat?.grilleSuccesJeu);
  enteteBloc.style.setProperty(
    "--image-fond-entete",
    imageFond ? `url("${imageFond}")` : "none",
  );
  imageConsole.src = iconeConsole;
  imageConsole.classList.toggle("visible", !!iconeConsole);
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
  appliquerGrilleSucces([]);
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
        "Impossible de lire state.json. Vérifie que overlay.html et state.json sont dans le même dossier OBS.",
      );
    }
  }
}

initialiserEdition();
rafraichir();
setInterval(rafraichir, 1000);

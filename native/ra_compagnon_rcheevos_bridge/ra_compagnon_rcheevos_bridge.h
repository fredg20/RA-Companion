#ifndef RA_COMPAGNON_RCHEEVOS_BRIDGE_H
#define RA_COMPAGNON_RCHEEVOS_BRIDGE_H

#ifdef _WIN32
#define RA_COMPAGNON_EXPORT __declspec(dllexport)
#else
#define RA_COMPAGNON_EXPORT
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct ra_compagnon_rcheevos_progress_indicator_t
{
    /* Texte formaté retourné par rcheevos, par exemple "7/10". */
    char texte[64];
    /* Pourcentage calculé quand une cible mesurée est connue. */
    float pourcentage;
    /* Indique si le pourcentage ci-dessus peut être affiché. */
    int pourcentage_disponible;
} ra_compagnon_rcheevos_progress_indicator_t;

typedef int (*ra_compagnon_rcheevos_read_memory_callback_t)(
    unsigned int adresse,
    unsigned int nombre_octets,
    unsigned char* tampon,
    unsigned int taille_tampon,
    void* contexte);

/* Enregistre le callback de lecture mémoire fourni par l'application hôte. */
RA_COMPAGNON_EXPORT int ra_compagnon_rcheevos_register_memory_reader(
    ra_compagnon_rcheevos_read_memory_callback_t callback,
    void* contexte);

/* Efface la source mémoire active quand aucun lecteur natif n'est disponible. */
RA_COMPAGNON_EXPORT int ra_compagnon_rcheevos_clear_memory_reader(void);

/* Lecture utilitaire brute, surtout pratique pour les tests de câblage. */
RA_COMPAGNON_EXPORT int ra_compagnon_rcheevos_probe_memory_reader(
    unsigned int adresse,
    unsigned int nombre_octets,
    unsigned char* tampon,
    unsigned int taille_tampon,
    unsigned int* octets_lus);

/* Réinitialise toutes les définitions de succès actuellement chargées. */
RA_COMPAGNON_EXPORT int ra_compagnon_rcheevos_clear_achievement_definitions(void);

/* Ajoute ou remplace la définition rcheevos d'un succès. */
RA_COMPAGNON_EXPORT int ra_compagnon_rcheevos_set_achievement_definition(
    int identifiant_succes,
    const char* definition);

/* Injecte une progression sérialisée extraite d'un savestate RALibRetro. */
RA_COMPAGNON_EXPORT int ra_compagnon_rcheevos_set_serialized_progress(
    const unsigned char* progression_serialisee,
    unsigned int taille_progression);

/* Efface la progression sérialisée précédemment injectée. */
RA_COMPAGNON_EXPORT int ra_compagnon_rcheevos_clear_serialized_progress(void);

/* Retourne le Progress Indicator calculé pour un succès donné. */
RA_COMPAGNON_EXPORT int ra_compagnon_rcheevos_get_progress_indicator(
    int identifiant_jeu,
    int identifiant_console,
    int identifiant_succes,
    ra_compagnon_rcheevos_progress_indicator_t* indicateur);

#ifdef __cplusplus
}
#endif

#endif

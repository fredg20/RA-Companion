#include "ra_compagnon_rcheevos_bridge.h"

#include <stddef.h>
#include <stdlib.h>
#include <string.h>

#ifdef RA_COMPAGNON_HAS_RCHEEVOS
#include "rc_runtime.h"
#endif

typedef struct ra_compagnon_rcheevos_achievement_definition_entry_t
{
    int identifiant_succes;
    char* definition;
} ra_compagnon_rcheevos_achievement_definition_entry_t;

/* Etat global minimal du bridge partage entre les appels C# et rcheevos. */
static ra_compagnon_rcheevos_read_memory_callback_t g_memory_reader = 0;
static void* g_memory_reader_context = 0;
static ra_compagnon_rcheevos_achievement_definition_entry_t* g_definitions = 0;
static size_t g_nombre_definitions = 0;
static size_t g_capacite_definitions = 0;
static unsigned char* g_progression_serialisee = 0;
static size_t g_taille_progression_serialisee = 0;

#ifdef RA_COMPAGNON_HAS_RCHEEVOS
static rc_runtime_t g_runtime;
static int g_runtime_initialise = 0;
/* Force une reconstruction du runtime après un changement de définitions. */
static int g_runtime_definitions_stale = 1;

static void ra_compagnon_runtime_event_handler(const rc_runtime_event_t* runtime_event)
{
    /* Le bridge ne relaie pas encore les événements : seul le calcul mesuré nous intéresse. */
    (void)runtime_event;
}
#endif

static char* ra_compagnon_strdup(const char* texte)
{
    size_t longueur;
    char* copie;

    if (texte == 0)
        return 0;

    longueur = strlen(texte) + 1;
    copie = (char*)malloc(longueur);
    if (copie != 0)
        memcpy(copie, texte, longueur);

    return copie;
}

#ifdef RA_COMPAGNON_HAS_RCHEEVOS
static void ra_compagnon_reset_runtime(void)
{
    if (g_runtime_initialise)
        rc_runtime_destroy(&g_runtime);

    rc_runtime_init(&g_runtime);
    g_runtime_initialise = 1;
}

static uint32_t ra_compagnon_peek(uint32_t address, uint32_t num_bytes, void* ud)
{
    unsigned char buffer[8];
    uint32_t value = 0;
    uint32_t index;
    int bytes_read;

    (void)ud;

    if (g_memory_reader == 0 || num_bytes == 0 || num_bytes > sizeof(buffer))
        return 0;

    bytes_read = g_memory_reader(
        address,
        num_bytes,
        buffer,
        (unsigned int)sizeof(buffer),
        g_memory_reader_context);

    if (bytes_read < (int)num_bytes)
        return 0;

    /* rcheevos attend ici une lecture little-endian sur 1 a 4 octets. */
    for (index = 0; index < num_bytes; ++index)
        value |= ((uint32_t)buffer[index]) << (index * 8);

    return value;
}

static int ra_compagnon_ensure_runtime(void)
{
    size_t index;
    int resultat;

    if (!g_runtime_initialise)
        ra_compagnon_reset_runtime();

    if (!g_runtime_definitions_stale)
        return RC_OK;

    /* Toute modification de définitions repart d'un runtime propre. */
    ra_compagnon_reset_runtime();

    for (index = 0; index < g_nombre_definitions; ++index)
    {
        resultat = rc_runtime_activate_achievement(
            &g_runtime,
            (uint32_t)g_definitions[index].identifiant_succes,
            g_definitions[index].definition,
            0,
            0);

        if (resultat != RC_OK)
            return resultat;
    }

    g_runtime_definitions_stale = 0;
    return RC_OK;
}
#endif

int ra_compagnon_rcheevos_register_memory_reader(
    ra_compagnon_rcheevos_read_memory_callback_t callback,
    void* contexte)
{
    g_memory_reader = callback;
    g_memory_reader_context = contexte;
    return 0;
}

int ra_compagnon_rcheevos_clear_memory_reader(void)
{
    g_memory_reader = 0;
    g_memory_reader_context = 0;
    return 0;
}

int ra_compagnon_rcheevos_probe_memory_reader(
    unsigned int adresse,
    unsigned int nombre_octets,
    unsigned char* tampon,
    unsigned int taille_tampon,
    unsigned int* octets_lus)
{
    int resultat;

    if (octets_lus != 0)
        *octets_lus = 0;

    if (g_memory_reader == 0)
        return 3;

    if (tampon == 0 || taille_tampon == 0)
        return 2;

    resultat = g_memory_reader(
        adresse,
        nombre_octets,
        tampon,
        taille_tampon,
        g_memory_reader_context);

    if (resultat < 0)
        return 4;

    if (octets_lus != 0)
        *octets_lus = (unsigned int)resultat;

    return 0;
}

int ra_compagnon_rcheevos_clear_achievement_definitions(void)
{
    size_t index;

    /* Les définitions sont possédées par le bridge, donc libérées ici. */
    for (index = 0; index < g_nombre_definitions; ++index)
    {
        free(g_definitions[index].definition);
        g_definitions[index].definition = 0;
        g_definitions[index].identifiant_succes = 0;
    }

    free(g_definitions);
    g_definitions = 0;
    g_nombre_definitions = 0;
    g_capacite_definitions = 0;

#ifdef RA_COMPAGNON_HAS_RCHEEVOS
    g_runtime_definitions_stale = 1;
#endif

    return 0;
}

int ra_compagnon_rcheevos_set_serialized_progress(
    const unsigned char* progression_serialisee,
    unsigned int taille_progression)
{
    unsigned char* copie;

    if (progression_serialisee == 0 || taille_progression == 0)
        return 2;

    copie = (unsigned char*)malloc((size_t)taille_progression);
    if (copie == 0)
        return 5;

    memcpy(copie, progression_serialisee, (size_t)taille_progression);

    /* On remplace l'ancien buffer en une seule fois pour garder un état cohérent. */
    free(g_progression_serialisee);
    g_progression_serialisee = copie;
    g_taille_progression_serialisee = (size_t)taille_progression;
    return 0;
}

int ra_compagnon_rcheevos_clear_serialized_progress(void)
{
    free(g_progression_serialisee);
    g_progression_serialisee = 0;
    g_taille_progression_serialisee = 0;
    return 0;
}

int ra_compagnon_rcheevos_set_achievement_definition(
    int identifiant_succes,
    const char* definition)
{
    size_t index;
    char* copie;

    if (identifiant_succes <= 0 || definition == 0 || definition[0] == '\0')
        return 2;

    for (index = 0; index < g_nombre_definitions; ++index)
    {
        if (g_definitions[index].identifiant_succes == identifiant_succes)
        {
            copie = ra_compagnon_strdup(definition);

            if (copie == 0)
                return 5;

            free(g_definitions[index].definition);
            g_definitions[index].definition = copie;

#ifdef RA_COMPAGNON_HAS_RCHEEVOS
            g_runtime_definitions_stale = 1;
#endif
            return 0;
        }
    }

    if (g_nombre_definitions == g_capacite_definitions)
    {
        size_t nouvelle_capacite =
            g_capacite_definitions == 0 ? 16 : g_capacite_definitions * 2;
        /* Petit tableau dynamique maison : suffisant vu le volume de succès. */
        void* nouveau_tampon = realloc(
            g_definitions,
            nouvelle_capacite * sizeof(*g_definitions));

        if (nouveau_tampon == 0)
            return 5;

        g_definitions =
            (ra_compagnon_rcheevos_achievement_definition_entry_t*)nouveau_tampon;
        g_capacite_definitions = nouvelle_capacite;
    }

    copie = ra_compagnon_strdup(definition);

    if (copie == 0)
        return 5;

    g_definitions[g_nombre_definitions].identifiant_succes = identifiant_succes;
    g_definitions[g_nombre_definitions].definition = copie;
    ++g_nombre_definitions;

#ifdef RA_COMPAGNON_HAS_RCHEEVOS
    g_runtime_definitions_stale = 1;
#endif

    return 0;
}

int ra_compagnon_rcheevos_get_progress_indicator(
    int identifiant_jeu,
    int identifiant_console,
    int identifiant_succes,
    ra_compagnon_rcheevos_progress_indicator_t* indicateur)
{
    (void)identifiant_jeu;
    (void)identifiant_console;

    if (indicateur == 0)
        return 2;

    memset(indicateur, 0, sizeof(*indicateur));

#ifndef RA_COMPAGNON_HAS_RCHEEVOS
    return 1;
#else
    {
        unsigned measured_value = 0;
        unsigned measured_target = 0;
        unsigned measured_effective_value;
        int resultat = ra_compagnon_ensure_runtime();

        if (resultat != RC_OK)
            return resultat;

        if (g_memory_reader != 0)
        {
            /* Chemin live : on fait avancer le runtime à partir d'une vraie source mémoire. */
            rc_runtime_do_frame(
                &g_runtime,
                ra_compagnon_runtime_event_handler,
                ra_compagnon_peek,
                0,
                0);
        }
        else if (g_progression_serialisee != 0 && g_taille_progression_serialisee > 0)
        {
            /* Chemin passif : on recharge une progression sérialisée depuis un savestate. */
            resultat = rc_runtime_deserialize_progress_sized(
                &g_runtime,
                g_progression_serialisee,
                (uint32_t)g_taille_progression_serialisee,
                0);

            if (resultat != RC_OK)
                return resultat;
        }
        else
        {
            return 3;
        }

        if (!rc_runtime_format_achievement_measured(
                &g_runtime,
                (uint32_t)identifiant_succes,
                indicateur->texte,
                sizeof(indicateur->texte)))
        {
            return 1;
        }

        if (!rc_runtime_get_achievement_measured(
                &g_runtime,
                (uint32_t)identifiant_succes,
                &measured_value,
                &measured_target))
        {
            return 1;
        }

        if (measured_target > 0)
        {
            measured_effective_value =
                measured_value > measured_target ? measured_target : measured_value;
            /* On borne la valeur mesurée pour éviter un pourcentage > 100 %. */
            indicateur->pourcentage =
                ((float)measured_effective_value * 100.0f) / (float)measured_target;
            indicateur->pourcentage_disponible = 1;
        }

        return 0;
    }
#endif
}

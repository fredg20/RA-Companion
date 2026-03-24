cmake_minimum_required(VERSION 3.20)

project(ra_compagnon_rcheevos_bridge C)

set(RA_COMPAGNON_RCHEEVOS_DIR "" CACHE PATH "Chemin vers un dépôt rcheevos vendorisé")

set(_rcheevos_candidates
    "${RA_COMPAGNON_RCHEEVOS_DIR}"
    "${CMAKE_CURRENT_SOURCE_DIR}/third_party/rcheevos"
    "${CMAKE_CURRENT_SOURCE_DIR}/../third_party/rcheevos"
    "${CMAKE_CURRENT_SOURCE_DIR}/../../third_party/rcheevos"
)

set(RA_COMPAGNON_RCHEEVOS_FOUND FALSE)
foreach(_candidate IN LISTS _rcheevos_candidates)
    if(_candidate AND EXISTS "${_candidate}/include" AND EXISTS "${_candidate}/src")
        set(RA_COMPAGNON_RCHEEVOS_FOUND TRUE)
        set(RA_COMPAGNON_RCHEEVOS_ROOT "${_candidate}")
        break()
    endif()
endforeach()

add_library(ra_compagnon_rcheevos_bridge SHARED
    ra_compagnon_rcheevos_bridge.c
)

target_include_directories(ra_compagnon_rcheevos_bridge
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}
)

if(RA_COMPAGNON_RCHEEVOS_FOUND)
    file(GLOB_RECURSE RA_COMPAGNON_RCHEEVOS_SOURCES
        CONFIGURE_DEPENDS
        "${RA_COMPAGNON_RCHEEVOS_ROOT}/src/*.c"
    )

    list(FILTER RA_COMPAGNON_RCHEEVOS_SOURCES EXCLUDE REGEX ".*/src/rc_libretro\\.c$")

    if(RA_COMPAGNON_RCHEEVOS_SOURCES)
        target_sources(ra_compagnon_rcheevos_bridge PRIVATE ${RA_COMPAGNON_RCHEEVOS_SOURCES})
        target_include_directories(ra_compagnon_rcheevos_bridge
            PRIVATE
                "${RA_COMPAGNON_RCHEEVOS_ROOT}/include"
                "${RA_COMPAGNON_RCHEEVOS_ROOT}/src"
                "${RA_COMPAGNON_RCHEEVOS_ROOT}/src/rcheevos"
                "${RA_COMPAGNON_RCHEEVOS_ROOT}/src/rapi"
                "${RA_COMPAGNON_RCHEEVOS_ROOT}/src/rhash"
        )
        target_compile_definitions(
            ra_compagnon_rcheevos_bridge
            PRIVATE
                RA_COMPAGNON_HAS_RCHEEVOS=1
        )
        message(STATUS "rcheevos détecté dans : ${RA_COMPAGNON_RCHEEVOS_ROOT}")
    else()
        message(WARNING "Dépôt rcheevos détecté, mais aucune source .c n'a été trouvée dans src/")
    endif()
else()
    message(STATUS "rcheevos non détecté : le bridge sera compilé en mode squelette.")
endif()

target_compile_features(ra_compagnon_rcheevos_bridge PRIVATE c_std_11)

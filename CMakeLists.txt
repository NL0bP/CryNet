cmake_minimum_required(VERSION 3.25)

if(POLICY CMP0141)
    cmake_policy(SET CMP0141 NEW)
endif()
set(CMAKE_POLICY_DEFAULT_CMP0141 NEW)
set(CMAKE_MSVC_DEBUG_INFORMATION_FORMAT "$<$<CONFIG:Debug,RelWithDebInfo>:Embedded>")

project(CryPhysics LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
set(CMAKE_VISIBILITY_INLINES_HIDDEN ON)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)

set(LY_SRC "${CMAKE_CURRENT_SOURCE_DIR}/lumberyard-src")
set(CRYCOMMON_MIN "${CMAKE_CURRENT_SOURCE_DIR}/crycommon-min")
set(AZCORE_SRC "${CMAKE_CURRENT_SOURCE_DIR}/azcore-src")
set(STUBS "${CMAKE_CURRENT_SOURCE_DIR}/stubs")

set(LY_CRYPHYSICS_SOURCES)

if(EXISTS "${LY_SRC}/physicalworld.cpp")
    # Canonical source list from lumberyard-src/cryphysics.waf_files.
    # Includes all CryPhysics .cpp to avoid cross-module symbol breaks; size cost is small.
    list(APPEND LY_CRYPHYSICS_SOURCES
        ${LY_SRC}/CryPhysics.cpp
        ${LY_SRC}/StdAfx.cpp
        ${LY_SRC}/rigidbody.cpp
        ${LY_SRC}/physicalworld.cpp
        ${LY_SRC}/physicalentity.cpp
        ${LY_SRC}/physicalplaceholder.cpp
        ${LY_SRC}/physarea.cpp
        ${LY_SRC}/rigidentity.cpp
        ${LY_SRC}/articulatedentity.cpp
        ${LY_SRC}/livingentity.cpp
        ${LY_SRC}/ropeentity.cpp
        ${LY_SRC}/softentity.cpp
        ${LY_SRC}/particleentity.cpp
        ${LY_SRC}/wheeledvehicleentity.cpp
        ${LY_SRC}/waterman.cpp
        ${LY_SRC}/boolean2d.cpp
        ${LY_SRC}/boolean3d.cpp
        ${LY_SRC}/matrixnm.cpp
        ${LY_SRC}/qhull.cpp
        ${LY_SRC}/utils.cpp
        ${LY_SRC}/worldump.cpp
        ${LY_SRC}/tetrlattice.cpp
        ${LY_SRC}/trimesh.cpp
        ${LY_SRC}/voxelbv.cpp
        ${LY_SRC}/voxelgeom.cpp
        ${LY_SRC}/aabbtree.cpp
        ${LY_SRC}/boxgeom.cpp
        ${LY_SRC}/capsulegeom.cpp
        ${LY_SRC}/cylindergeom.cpp
        ${LY_SRC}/geoman.cpp
        ${LY_SRC}/geometry.cpp
        ${LY_SRC}/obbtree.cpp
        ${LY_SRC}/overlapchecks.cpp
        ${LY_SRC}/raybv.cpp
        ${LY_SRC}/raygeom.cpp
        ${LY_SRC}/rwi.cpp
        ${LY_SRC}/rotunprojectionchecks.cpp
        ${LY_SRC}/singleboxtree.cpp
        ${LY_SRC}/spheregeom.cpp
        ${LY_SRC}/heightfieldbv.cpp
        ${LY_SRC}/heightfieldgeom.cpp
        ${LY_SRC}/intersectionchecks.cpp
        ${LY_SRC}/linunprojectionchecks.cpp
    )
    message(STATUS "CryPhysics: building with Lumberyard source from ${LY_SRC}")
else()
    message(WARNING "CryPhysics: lumberyard-src/ not found - building API scaffold only. Run scripts/fetch-lumberyard.sh first.")
endif()

add_library(CryPhysics SHARED
    src/api.cpp
    src/azcore_stubs.cpp
    ${LY_CRYPHYSICS_SOURCES}
)

if(WIN32)
    set(AZCORE_PLATFORM_DIR "${AZCORE_SRC}/Platform/Windows")
else()
    set(AZCORE_PLATFORM_DIR "${AZCORE_SRC}/Platform/Linux")
endif()

target_include_directories(CryPhysics
    PUBLIC  include
    PRIVATE
        ${STUBS}
        ${CRYCOMMON_MIN}
        ${AZCORE_SRC}
        ${AZCORE_PLATFORM_DIR}
        ${LY_SRC}
)

target_compile_definitions(CryPhysics PRIVATE
    ENABLE_CRY_PHYSICS=1
    # Improved net-sync debug draw path pulls ITimer/SDrawTextInfo/DrawTextQueued
    # from CrySystem & CryRenderer — irrelevant on a headless server. Disable.
    USE_IMPROVED_RIGID_ENTITY_SYNCHRONISATION=0
)

if(MSVC)
    target_compile_options(CryPhysics PRIVATE /W3 /fp:precise /EHsc)
    target_compile_definitions(CryPhysics PRIVATE _CRT_SECURE_NO_WARNINGS NOMINMAX WIN32_LEAN_AND_MEAN)
else()
    target_compile_options(CryPhysics PRIVATE -Wall -Wno-unused-parameter -fno-fast-math -fvisibility=hidden)
    target_link_options(CryPhysics PRIVATE -static-libstdc++ -static-libgcc)
endif()

set_target_properties(CryPhysics PROPERTIES
    OUTPUT_NAME "CryPhysics"
    PREFIX ""
)

if(WIN32)
    set(CP_OUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/bin/win-x64")
else()
    set(CP_OUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/bin/linux-x64")
endif()

set_target_properties(CryPhysics PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${CP_OUT_DIR}
    LIBRARY_OUTPUT_DIRECTORY ${CP_OUT_DIR}
    # MSVC multi-config generators append /<Config>/ by default; override so
    # every config writes straight to bin/<rid>/ — the C# csproj `None Include`
    # points at that path.
    RUNTIME_OUTPUT_DIRECTORY_DEBUG          ${CP_OUT_DIR}
    RUNTIME_OUTPUT_DIRECTORY_RELEASE        ${CP_OUT_DIR}
    RUNTIME_OUTPUT_DIRECTORY_RELWITHDEBINFO ${CP_OUT_DIR}
    LIBRARY_OUTPUT_DIRECTORY_DEBUG          ${CP_OUT_DIR}
    LIBRARY_OUTPUT_DIRECTORY_RELEASE        ${CP_OUT_DIR}
    LIBRARY_OUTPUT_DIRECTORY_RELWITHDEBINFO ${CP_OUT_DIR}
)

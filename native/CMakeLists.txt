cmake_minimum_required(VERSION 3.20)
project(lumenrtc LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

if(NOT LIBWEBRTC_ROOT)
  if(DEFINED ENV{LIBWEBRTC_ROOT})
    set(LIBWEBRTC_ROOT $ENV{LIBWEBRTC_ROOT})
  elseif(EXISTS "${CMAKE_SOURCE_DIR}/../vendor/libwebrtc/include")
    set(LIBWEBRTC_ROOT "${CMAKE_SOURCE_DIR}/../vendor/libwebrtc")
  elseif(EXISTS "${CMAKE_SOURCE_DIR}/../../libwebrtc/include")
    set(LIBWEBRTC_ROOT "${CMAKE_SOURCE_DIR}/../../libwebrtc")
  endif()
endif()

if(NOT LIBWEBRTC_ROOT)
  message(FATAL_ERROR "Set LIBWEBRTC_ROOT to the libwebrtc repo path")
endif()

if(NOT LIBWEBRTC_BUILD_DIR)
  if(DEFINED ENV{LIBWEBRTC_BUILD_DIR})
    set(LIBWEBRTC_BUILD_DIR $ENV{LIBWEBRTC_BUILD_DIR})
  endif()
endif()

if(NOT LIBWEBRTC_BUILD_DIR)
  message(FATAL_ERROR "Set LIBWEBRTC_BUILD_DIR to the directory containing libwebrtc")
endif()

set(LIBWEBRTC_INCLUDE_DIR "${LIBWEBRTC_ROOT}/include")

add_library(lumenrtc SHARED
  src/lumenrtc.cpp
)

if(NOT WIN32)
  set_target_properties(lumenrtc PROPERTIES
    C_VISIBILITY_PRESET hidden
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN YES
  )
endif()

target_include_directories(lumenrtc PUBLIC
  include
  "${LIBWEBRTC_INCLUDE_DIR}"
)

target_compile_definitions(lumenrtc PRIVATE LUMENRTC_EXPORTS RTC_DESKTOP_DEVICE)

if(WIN32)
  target_compile_definitions(lumenrtc PRIVATE LIB_WEBRTC_API_DLL)
endif()

if(WIN32)
  set(_libwebrtc_import "${LIBWEBRTC_BUILD_DIR}/libwebrtc.dll.lib")
  set(_libwebrtc_fallback "${LIBWEBRTC_BUILD_DIR}/libwebrtc.lib")
  if(EXISTS "${_libwebrtc_import}")
    target_link_libraries(lumenrtc PRIVATE "${_libwebrtc_import}")
  elseif(EXISTS "${_libwebrtc_fallback}")
    target_link_libraries(lumenrtc PRIVATE "${_libwebrtc_fallback}")
  else()
    message(FATAL_ERROR "libwebrtc import library not found in ${LIBWEBRTC_BUILD_DIR}. Expected libwebrtc.dll.lib or libwebrtc.lib.")
  endif()
else()
  target_link_libraries(lumenrtc PRIVATE "${LIBWEBRTC_BUILD_DIR}/libwebrtc.so")
endif()

if(UNIX AND NOT APPLE)
  target_link_options(lumenrtc PRIVATE
    "-Wl,--version-script=${CMAKE_CURRENT_SOURCE_DIR}/lumenrtc.map"
  )
  set_target_properties(lumenrtc PROPERTIES
    BUILD_RPATH "${LIBWEBRTC_BUILD_DIR}"
    INSTALL_RPATH "${LIBWEBRTC_BUILD_DIR}"
  )
endif()

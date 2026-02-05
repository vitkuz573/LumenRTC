cmake_minimum_required(VERSION 3.20)
project(lumenrtc LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

option(LUMENRTC_ENABLE_DESKTOP_CAPTURE "Enable desktop capture support" ON)

if(NOT LIBWEBRTC_ROOT)
  message(FATAL_ERROR "Set LIBWEBRTC_ROOT to the libwebrtc repo path")
endif()

if(NOT LIBWEBRTC_BUILD_DIR)
  message(FATAL_ERROR "Set LIBWEBRTC_BUILD_DIR to the directory containing libwebrtc")
endif()

set(LIBWEBRTC_INCLUDE_DIR "${LIBWEBRTC_ROOT}/include")

add_library(lumenrtc SHARED
  src/lumenrtc.cpp
)

target_include_directories(lumenrtc PUBLIC
  include
  "${LIBWEBRTC_INCLUDE_DIR}"
)

target_compile_definitions(lumenrtc PRIVATE LUMENRTC_EXPORTS)
if(LUMENRTC_ENABLE_DESKTOP_CAPTURE)
  target_compile_definitions(lumenrtc PRIVATE RTC_DESKTOP_DEVICE)
endif()

if(WIN32)
  target_compile_definitions(lumenrtc PRIVATE LIB_WEBRTC_API_DLL)
endif()

if(WIN32)
  target_link_libraries(lumenrtc PRIVATE "${LIBWEBRTC_BUILD_DIR}/libwebrtc.lib")
else()
  target_link_libraries(lumenrtc PRIVATE "${LIBWEBRTC_BUILD_DIR}/libwebrtc.so")
endif()

if(UNIX AND NOT APPLE)
  set_target_properties(lumenrtc PROPERTIES
    BUILD_RPATH "${LIBWEBRTC_BUILD_DIR}"
    INSTALL_RPATH "${LIBWEBRTC_BUILD_DIR}"
  )
endif()

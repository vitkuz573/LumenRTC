set(
	SOURCE_FILES
	peerconnection.test.cc
	tests.cc
)

# Create taget.
add_executable(test_lumenrtc_bridge ${SOURCE_FILES})

if(APPLE)
	find_library(APPLICATION_SERVICES ApplicationServices)
	find_library(AUDIO_TOOLBOX AudioToolbox)
	find_library(AV_FOUNDATION AVFoundation)
	find_library(CORE_AUDIO CoreAudio)
	find_library(CORE_FOUNDATION Foundation)
	find_library(CORE_MEDIA CoreMedia)
	find_library(CORE_VIDEO CoreVideo)
	find_library(CORE_SERVICES CoreServices)

	target_link_libraries(test_lumenrtc_bridge PRIVATE
		${APPLICATION_SERVICES}
		${AUDIO_TOOLBOX}
		${AV_FOUNDATION}
		${CORE_AUDIO}
		${CORE_FOUNDATION}
		${CORE_MEDIA}
		${CORE_VIDEO}
		${CORE_SERVICES}
	)

	# Bundle it.
	set_target_properties(test_lumenrtc_bridge PROPERTIES
		MACOSX_BUNDLE TRUE
		MACOSX_BUNDLE_INFO_PLIST ${CMAKE_CURRENT_SOURCE_DIR}/Info.plist
	)
endif(APPLE)

# Private (implementation) header files.
target_include_directories(test_lumenrtc_bridge PRIVATE
	${lumenrtc_bridge_SOURCE_DIR}/include
	include
)

# Private dependencies.
target_link_libraries(test_lumenrtc_bridge PRIVATE lumenrtc_bridge)

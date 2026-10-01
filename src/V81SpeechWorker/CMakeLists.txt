cmake_minimum_required(VERSION 3.20)
cmake_policy(SET CMP0091 NEW)

project(V81SpeechWorker VERSION 3.3.0 LANGUAGES CXX)

if(NOT WIN32 OR NOT MSVC OR NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(FATAL_ERROR "V81SpeechWorker requires Windows x64 and the MSVC toolchain.")
endif()

# This is the unmodified v1.13.8 header used by the shipped native engine.
file(SHA256 "${CMAKE_CURRENT_SOURCE_DIR}/third_party/sherpa-onnx/c-api.h" SHERPA_HEADER_SHA256)
if(NOT SHERPA_HEADER_SHA256 STREQUAL "2a1b95084be8fd1deb3228fcad2fd3f7f0258b64582f7402281ec174c7b7f4ce")
    message(FATAL_ERROR "The pinned sherpa-onnx v1.13.8 C API header has changed.")
endif()

add_executable(V81SpeechWorker main.cpp)
target_compile_features(V81SpeechWorker PRIVATE cxx_std_17)
target_compile_definitions(V81SpeechWorker PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX UNICODE _UNICODE)
target_compile_options(V81SpeechWorker PRIVATE /W4 /WX /utf-8 /permissive- /EHsc)
set_property(TARGET V81SpeechWorker PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
target_link_options(V81SpeechWorker PRIVATE /DYNAMICBASE /NXCOMPAT /HIGHENTROPYVA)

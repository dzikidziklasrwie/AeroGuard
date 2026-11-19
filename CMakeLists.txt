cmake_minimum_required(VERSION 3.24)

project(AeroGuard VERSION 1.0.0 LANGUAGES CXX)

option(AEROGUARD_BUILD_TESTS "Build the AeroGuard GoogleTest suite" ON)
option(AEROGUARD_WARNINGS_AS_ERRORS "Treat compiler warnings as errors" OFF)
option(AEROGUARD_ENABLE_SANITIZERS "Enable AddressSanitizer and UndefinedBehaviorSanitizer" OFF)
option(AEROGUARD_ENABLE_CLANG_TIDY "Run clang-tidy while compiling" OFF)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

include(FetchContent)

find_package(nlohmann_json 3.11.3 QUIET)
if(NOT nlohmann_json_FOUND)
  FetchContent_Declare(
    nlohmann_json
    URL https://github.com/nlohmann/json/releases/download/v3.11.3/json.tar.xz
    URL_HASH SHA256=d6c65aca6b1ed68e7a182f4757257b107ae403032760ed6ef121c9d55e81757d
    DOWNLOAD_EXTRACT_TIMESTAMP TRUE
  )
  FetchContent_MakeAvailable(nlohmann_json)
endif()

function(aeroguard_set_warnings target)
  if(MSVC)
    target_compile_options(${target} PRIVATE /W4 /permissive- /Zc:__cplusplus)
    if(AEROGUARD_WARNINGS_AS_ERRORS)
      target_compile_options(${target} PRIVATE /WX)
    endif()
  else()
    target_compile_options(${target} PRIVATE -Wall -Wextra -Wpedantic -Wconversion -Wshadow)
    if(AEROGUARD_WARNINGS_AS_ERRORS)
      target_compile_options(${target} PRIVATE -Werror)
    endif()
  endif()
endfunction()

function(aeroguard_enable_sanitizers target)
  if(AEROGUARD_ENABLE_SANITIZERS AND NOT MSVC)
    target_compile_options(${target} PRIVATE -fsanitize=address,undefined -fno-omit-frame-pointer)
    target_link_options(${target} PRIVATE -fsanitize=address,undefined)
  endif()
endfunction()

add_library(aeroguard_core
  src/diagnostics/health_monitor.cpp
  src/core/state_machine.cpp
  src/core/telemetry.cpp
  src/core/types.cpp
  src/hal/simulated_hal.cpp
  src/logging/json_logger.cpp
  src/simulation/config.cpp
  src/simulation/simulator.cpp
  src/simulation/subsystems.cpp
)

target_include_directories(aeroguard_core PUBLIC include)
target_link_libraries(aeroguard_core PUBLIC nlohmann_json::nlohmann_json)
target_compile_features(aeroguard_core PUBLIC cxx_std_20)
aeroguard_set_warnings(aeroguard_core)
aeroguard_enable_sanitizers(aeroguard_core)

if(AEROGUARD_ENABLE_CLANG_TIDY)
  find_program(CLANG_TIDY_EXE NAMES clang-tidy REQUIRED)
  set_target_properties(aeroguard_core PROPERTIES CXX_CLANG_TIDY "${CLANG_TIDY_EXE}")
endif()

add_executable(aeroguard src/main.cpp)
target_link_libraries(aeroguard PRIVATE aeroguard_core)
aeroguard_set_warnings(aeroguard)
aeroguard_enable_sanitizers(aeroguard)

if(AEROGUARD_BUILD_TESTS)
  enable_testing()
  find_package(Threads REQUIRED)
  find_package(GTest 1.14 QUIET)
  if(NOT GTest_FOUND)
    set(gtest_force_shared_crt ON CACHE BOOL "" FORCE)
    FetchContent_Declare(
      googletest
      URL https://github.com/google/googletest/archive/refs/tags/v1.14.0.tar.gz
      URL_HASH SHA256=8ad598c73ad796e0d8280b082cebd82a630d73e73cd3c70057938a6501bba5d7
      DOWNLOAD_EXTRACT_TIMESTAMP TRUE
    )
    FetchContent_MakeAvailable(googletest)
  endif()

  add_executable(aeroguard_tests
    tests/test_bounded_queue.cpp
    tests/test_config.cpp
    tests/test_health_monitor.cpp
    tests/test_simulator.cpp
    tests/test_state_machine.cpp
    tests/test_telemetry.cpp
  )
  target_link_libraries(aeroguard_tests PRIVATE aeroguard_core GTest::gtest_main Threads::Threads)
  target_compile_definitions(aeroguard_tests PRIVATE
    AEROGUARD_SOURCE_DIR="${CMAKE_CURRENT_SOURCE_DIR}"
  )
  aeroguard_set_warnings(aeroguard_tests)
  aeroguard_enable_sanitizers(aeroguard_tests)

  include(GoogleTest)
  gtest_discover_tests(aeroguard_tests DISCOVERY_TIMEOUT 60)

  add_test(
    NAME REQ020_CLIExportAll
    COMMAND ${CMAKE_COMMAND}
      -DAEROGUARD_APP=$<TARGET_FILE:aeroguard>
      -DSOURCE_DIR=${CMAKE_CURRENT_SOURCE_DIR}
      -DEXPORT_DIR=${CMAKE_CURRENT_BINARY_DIR}/export-test
      -P ${CMAKE_CURRENT_SOURCE_DIR}/tests/verify_export.cmake
  )
  add_test(
    NAME REQ021_CLIRejectsInvalidTick
    COMMAND aeroguard --tick 0
  )
  set_tests_properties(REQ021_CLIRejectsInvalidTick PROPERTIES WILL_FAIL TRUE)
  add_test(
    NAME REQ021_CLIRejectsUnknownScenario
    COMMAND aeroguard --scenario definitely-not-a-scenario
            --scenarios-dir ${CMAKE_CURRENT_SOURCE_DIR}/scenarios
  )
  set_tests_properties(REQ021_CLIRejectsUnknownScenario PROPERTIES WILL_FAIL TRUE)
  add_test(NAME REQ021_CLIHelp COMMAND aeroguard --help)
  add_test(
    NAME REQ002_CLIListScenarios
    COMMAND aeroguard --list-scenarios
            --scenarios-dir ${CMAKE_CURRENT_SOURCE_DIR}/scenarios
  )
  add_test(NAME REQ021_CLIRejectsUnknownArgument
           COMMAND aeroguard --definitely-not-an-option)
  set_tests_properties(REQ021_CLIRejectsUnknownArgument PROPERTIES WILL_FAIL TRUE)
  add_test(NAME REQ021_CLIRejectsInvalidSeed COMMAND aeroguard --seed not-a-number)
  set_tests_properties(REQ021_CLIRejectsInvalidSeed PROPERTIES WILL_FAIL TRUE)
  add_test(NAME REQ021_CLIRejectsZeroDuration COMMAND aeroguard --duration 0)
  set_tests_properties(REQ021_CLIRejectsZeroDuration PROPERTIES WILL_FAIL TRUE)
endif()

install(TARGETS aeroguard RUNTIME DESTINATION bin)
install(DIRECTORY scenarios DESTINATION share/aeroguard)

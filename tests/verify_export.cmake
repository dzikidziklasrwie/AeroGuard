if(NOT DEFINED AEROGUARD_APP OR NOT DEFINED SOURCE_DIR OR NOT DEFINED EXPORT_DIR)
  message(FATAL_ERROR "Set AEROGUARD_APP, SOURCE_DIR and EXPORT_DIR")
endif()

file(REMOVE_RECURSE "${EXPORT_DIR}")
execute_process(
  COMMAND "${AEROGUARD_APP}" --export-all "${EXPORT_DIR}"
          --scenarios-dir "${SOURCE_DIR}/scenarios" --tick 100 --seed 42
  RESULT_VARIABLE result
  OUTPUT_VARIABLE output
  ERROR_VARIABLE error
)
if(NOT result EQUAL 0)
  message(FATAL_ERROR "export-all failed (${result})\n${output}\n${error}")
endif()

set(expected_files
  communication-interruption.jsonl
  invalid-sensor-data.jsonl
  multiple-subsystem-failures.jsonl
  normal-operation.jsonl
  power-fluctuations.jsonl
  recovery-after-temporary-failure.jsonl
  simulated-overheating.jsonl
)
foreach(filename IN LISTS expected_files)
  if(NOT EXISTS "${EXPORT_DIR}/${filename}")
    message(FATAL_ERROR "missing export: ${filename}")
  endif()
endforeach()

set(manifest_path "${EXPORT_DIR}/manifest.json")
if(NOT EXISTS "${manifest_path}")
  message(FATAL_ERROR "missing manifest.json")
endif()
file(READ "${manifest_path}" manifest)
string(JSON schema_version GET "${manifest}" schema_version)
string(JSON scenario_count LENGTH "${manifest}" scenarios)
if(NOT schema_version EQUAL 1 OR NOT scenario_count EQUAL 7)
  message(FATAL_ERROR "unexpected manifest contract")
endif()
foreach(index RANGE 0 6)
  string(JSON scenario_id GET "${manifest}" scenarios ${index} id)
  string(JSON duration_ms GET "${manifest}" scenarios ${index} duration_ms)
  if(scenario_id STREQUAL "simulated-overheating")
    if(NOT duration_ms EQUAL 105000)
      message(FATAL_ERROR "simulated-overheating must last 105 seconds")
    endif()
  elseif(duration_ms LESS 40000 OR duration_ms GREATER 55000)
    message(FATAL_ERROR "scenario ${scenario_id} duration is outside 40-55 seconds")
  endif()
endforeach()

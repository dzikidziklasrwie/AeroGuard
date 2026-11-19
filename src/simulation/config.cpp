#include "aeroguard/config.hpp"

#include <algorithm>
#include <cmath>
#include <fstream>
#include <sstream>

#include <nlohmann/json.hpp>

namespace aeroguard {
namespace {

using json = nlohmann::json;

[[nodiscard]] std::int64_t required_integer(const json& object,
                                            const char* field,
                                            const std::filesystem::path& path) {
  if (!object.contains(field) || !object.at(field).is_number_integer()) {
    throw ScenarioConfigError("scenario '" + path.string() +
                              "' requires integer field '" + field + "'");
  }
  return object.at(field).get<std::int64_t>();
}

[[nodiscard]] std::string required_string(const json& object, const char* field,
                                          const std::filesystem::path& path) {
  if (!object.contains(field) || !object.at(field).is_string()) {
    throw ScenarioConfigError("scenario '" + path.string() +
                              "' requires string field '" + field + "'");
  }
  auto value = object.at(field).get<std::string>();
  if (value.empty()) {
    throw ScenarioConfigError("scenario field '" + std::string(field) +
                              "' must not be empty");
  }
  return value;
}

}  // namespace

std::string_view to_string(const FaultType value) noexcept {
  switch (value) {
    case FaultType::MissingTelemetry:
      return "missing_telemetry";
    case FaultType::InvalidTelemetry:
      return "invalid_telemetry";
    case FaultType::Overheating:
      return "overheating";
    case FaultType::PowerFluctuation:
      return "power_fluctuation";
    case FaultType::CoolingFailure:
      return "cooling_failure";
    case FaultType::ComputerOverload:
      return "computer_overload";
    case FaultType::EquipmentFailure:
      return "equipment_failure";
  }
  return "unknown";
}

std::optional<FaultType> fault_type_from_string(
    const std::string_view value) noexcept {
  constexpr FaultType values[]{
      FaultType::MissingTelemetry, FaultType::InvalidTelemetry,
      FaultType::Overheating,      FaultType::PowerFluctuation,
      FaultType::CoolingFailure,   FaultType::ComputerOverload,
      FaultType::EquipmentFailure};
  for (const auto candidate : values) {
    if (to_string(candidate) == value) {
      return candidate;
    }
  }
  return std::nullopt;
}

ScenarioConfig load_scenario(const std::filesystem::path& path) {
  std::ifstream input(path);
  if (!input) {
    throw ScenarioConfigError("cannot open scenario file: " + path.string());
  }

  json document;
  try {
    input >> document;
  } catch (const json::exception& error) {
    throw ScenarioConfigError("invalid JSON in scenario '" + path.string() +
                              "': " + error.what());
  }
  if (!document.is_object()) {
    throw ScenarioConfigError("scenario root must be a JSON object: " +
                              path.string());
  }

  ScenarioConfig result;
  result.name = required_string(document, "name", path);
  result.description = required_string(document, "description", path);
  const auto duration_ms = required_integer(document, "duration_ms", path);
  if (duration_ms <= 0) {
    throw ScenarioConfigError("scenario duration_ms must be positive");
  }
  result.default_duration = SimTime{duration_ms};

  if (!document.contains("faults") || !document.at("faults").is_array()) {
    throw ScenarioConfigError("scenario requires an array field 'faults'");
  }
  for (const auto& entry : document.at("faults")) {
    if (!entry.is_object()) {
      throw ScenarioConfigError("each fault entry must be an object");
    }
    const auto type_text = required_string(entry, "type", path);
    const auto subsystem_text = required_string(entry, "subsystem", path);
    const auto type = fault_type_from_string(type_text);
    const auto subsystem = subsystem_from_string(subsystem_text);
    if (!type) {
      throw ScenarioConfigError("unsupported fault type: " + type_text);
    }
    if (!subsystem) {
      throw ScenarioConfigError("unsupported subsystem: " + subsystem_text);
    }
    const auto start_ms = required_integer(entry, "start_ms", path);
    const auto fault_duration_ms = required_integer(entry, "duration_ms", path);
    if (start_ms < 0 || fault_duration_ms <= 0 || start_ms > duration_ms ||
        fault_duration_ms > duration_ms - start_ms) {
      throw ScenarioConfigError(
          "fault timing must be non-negative and fit within the scenario duration");
    }

    double magnitude = 1.0;
    if (entry.contains("magnitude")) {
      if (!entry.at("magnitude").is_number()) {
        throw ScenarioConfigError("fault magnitude must be numeric");
      }
      magnitude = entry.at("magnitude").get<double>();
      if (!std::isfinite(magnitude) || magnitude <= 0.0) {
        throw ScenarioConfigError("fault magnitude must be finite and positive");
      }
    }
    result.faults.push_back(FaultInjection{*type, *subsystem, SimTime{start_ms},
                                           SimTime{fault_duration_ms}, magnitude});
  }
  return result;
}

std::filesystem::path resolve_scenario_path(
    const std::string_view name_or_path,
    const std::filesystem::path& scenarios_dir) {
  const std::filesystem::path direct{name_or_path};
  if (std::filesystem::is_regular_file(direct)) {
    return direct;
  }
  auto candidate = scenarios_dir / direct;
  if (!candidate.has_extension()) {
    candidate += ".json";
  }
  if (std::filesystem::is_regular_file(candidate)) {
    return candidate;
  }
  throw ScenarioConfigError("scenario not found: " + std::string(name_or_path));
}

std::vector<std::filesystem::path> list_scenario_files(
    const std::filesystem::path& scenarios_dir) {
  if (!std::filesystem::is_directory(scenarios_dir)) {
    throw ScenarioConfigError("scenario directory not found: " +
                              scenarios_dir.string());
  }
  std::vector<std::filesystem::path> result;
  for (const auto& entry : std::filesystem::directory_iterator(scenarios_dir)) {
    if (entry.is_regular_file() && entry.path().extension() == ".json") {
      result.push_back(entry.path());
    }
  }
  std::ranges::sort(result);
  return result;
}

}  // namespace aeroguard

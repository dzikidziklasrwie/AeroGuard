#pragma once

#include "aeroguard/types.hpp"

#include <cstdint>
#include <filesystem>
#include <optional>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace aeroguard {

enum class FaultType : std::uint8_t {
  MissingTelemetry,
  InvalidTelemetry,
  Overheating,
  PowerFluctuation,
  CoolingFailure,
  ComputerOverload,
  EquipmentFailure
};

struct FaultInjection {
  FaultType type{FaultType::MissingTelemetry};
  SubsystemId subsystem{SubsystemId::Communication};
  SimTime start{};
  SimTime duration{};
  double magnitude{1.0};

  [[nodiscard]] bool active_at(SimTime time) const noexcept {
    return duration.count() > 0 && time >= start && time - start < duration;
  }
};

struct ScenarioConfig {
  std::string name;
  std::string description;
  SimTime default_duration{10'000};
  std::vector<FaultInjection> faults;
};

struct SimulationOptions {
  std::uint32_t seed{42};
  SimTime duration{};  // zero selects the scenario default
  SimTime tick{100};
  SimTime heartbeat_timeout{500};
  SimTime recovery_hold{1'000};
};

class ScenarioConfigError final : public std::runtime_error {
 public:
  using std::runtime_error::runtime_error;
};

[[nodiscard]] std::string_view to_string(FaultType value) noexcept;
[[nodiscard]] std::optional<FaultType> fault_type_from_string(
    std::string_view value) noexcept;
[[nodiscard]] ScenarioConfig load_scenario(const std::filesystem::path& path);
[[nodiscard]] std::filesystem::path resolve_scenario_path(
    std::string_view name_or_path, const std::filesystem::path& scenarios_dir);
[[nodiscard]] std::vector<std::filesystem::path> list_scenario_files(
    const std::filesystem::path& scenarios_dir);

}  // namespace aeroguard

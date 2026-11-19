#include "aeroguard/hal.hpp"

#include <algorithm>
#include <cmath>
#include <utility>

namespace aeroguard {

SimulatedHardware::SimulatedHardware(ScenarioConfig scenario,
                                     const std::uint32_t seed)
    : scenario_(std::move(scenario)), random_(seed) {}

bool SimulatedHardware::fault_active(const FaultType type,
                                     const SubsystemId subsystem,
                                     const SimTime now) const noexcept {
  return std::ranges::any_of(scenario_.faults, [&](const auto& fault) {
    return fault.type == type && fault.subsystem == subsystem &&
           fault.active_at(now);
  });
}

double SimulatedHardware::fault_magnitude(const FaultType type,
                                          const SubsystemId subsystem,
                                          const SimTime now) const noexcept {
  for (const auto& fault : scenario_.faults) {
    if (fault.type == type && fault.subsystem == subsystem &&
        fault.active_at(now)) {
      return fault.magnitude;
    }
  }
  return 1.0;
}

double SimulatedHardware::noise(const double minimum, const double maximum) {
  // std::uniform_real_distribution does not promise the same mapping across
  // standard-library implementations. The C++ standard specifies mt19937
  // output, so this [0, 1) conversion replays identically on MSVC, GCC and Clang.
  constexpr double kInverseRange = 1.0 / 4'294'967'296.0;  // 2^32
  const auto unit = static_cast<double>(random_()) * kInverseRange;
  return minimum + (maximum - minimum) * unit;
}

std::optional<PowerTelemetry> SimulatedHardware::read_power(const SimTime now) {
  if (fault_active(FaultType::MissingTelemetry, SubsystemId::Power, now)) {
    return std::nullopt;
  }
  PowerTelemetry value{27.5 + noise(-0.35, 0.35), 20.0 + noise(-2.0, 2.0),
                       400.0 + noise(-0.7, 0.7)};
  if (fault_active(FaultType::PowerFluctuation, SubsystemId::Power, now)) {
    const auto low_phase = (now.count() / 300) % 2 == 0;
    const auto magnitude =
        fault_magnitude(FaultType::PowerFluctuation, SubsystemId::Power, now);
    value.voltage_v = low_phase ? 27.0 - 7.0 * magnitude
                                : 27.0 + 5.5 * magnitude;
  }
  if (fault_active(FaultType::InvalidTelemetry, SubsystemId::Power, now)) {
    value.voltage_v = 999.0;
  }
  return value;
}

std::optional<CoolingTelemetry> SimulatedHardware::read_cooling(
    const SimTime now) {
  if (fault_active(FaultType::MissingTelemetry, SubsystemId::Cooling, now)) {
    return std::nullopt;
  }
  CoolingTelemetry value{48.0 + noise(-2.0, 2.0),
                         4'500.0 + noise(-100.0, 100.0),
                         8.0 + noise(-0.3, 0.3)};
  if (fault_active(FaultType::CoolingFailure, SubsystemId::Cooling, now)) {
    value.coolant_temperature_c = 108.0;
    value.fan_speed_rpm = 300.0;
    value.flow_rate_lpm = 0.5;
  }
  if (fault_active(FaultType::Overheating, SubsystemId::Cooling, now)) {
    value.coolant_temperature_c =
        108.0 * fault_magnitude(FaultType::Overheating,
                                SubsystemId::Cooling, now);
  }
  if (fault_active(FaultType::InvalidTelemetry, SubsystemId::Cooling, now)) {
    value.flow_rate_lpm = 999.0;
  }
  return value;
}

std::optional<TemperatureTelemetry> SimulatedHardware::read_temperature(
    const SimTime now) {
  if (fault_active(FaultType::MissingTelemetry, SubsystemId::Temperature, now)) {
    return std::nullopt;
  }
  TemperatureTelemetry value{62.0 + noise(-1.5, 1.5),
                             63.0 + noise(-1.5, 1.5),
                             24.0 + noise(-1.0, 1.0)};
  if (fault_active(FaultType::Overheating, SubsystemId::Temperature, now)) {
    const auto magnitude =
        fault_magnitude(FaultType::Overheating, SubsystemId::Temperature, now);
    value.sensor_1_c = 112.0 + 5.0 * magnitude;
    value.sensor_2_c = 110.0 + 5.0 * magnitude;
  }
  if (fault_active(FaultType::InvalidTelemetry, SubsystemId::Temperature, now)) {
    value.sensor_1_c = 999.0;
  }
  return value;
}

std::optional<CommunicationTelemetry>
SimulatedHardware::read_communication(const SimTime now) {
  if (fault_active(FaultType::MissingTelemetry, SubsystemId::Communication,
                   now)) {
    return std::nullopt;
  }
  CommunicationTelemetry value{96.0 + noise(-2.0, 2.0),
                               std::max(0.0, 0.3 + noise(-0.2, 0.2)), true};
  if (fault_active(FaultType::InvalidTelemetry, SubsystemId::Communication,
                   now)) {
    value.link_quality_percent = 150.0;
  }
  return value;
}

std::optional<ComputerTelemetry> SimulatedHardware::read_computer(
    const SimTime now) {
  if (fault_active(FaultType::MissingTelemetry, SubsystemId::Computer, now)) {
    return std::nullopt;
  }
  ComputerTelemetry value{43.0 + noise(-4.0, 4.0),
                          57.0 + noise(-2.0, 2.0), true};
  if (fault_active(FaultType::ComputerOverload, SubsystemId::Computer, now)) {
    value.cpu_load_percent = 99.0;
    value.memory_used_percent = 99.0;
    value.watchdog_ok = false;
  }
  if (fault_active(FaultType::InvalidTelemetry, SubsystemId::Computer, now)) {
    value.cpu_load_percent = 150.0;
  }
  return value;
}

std::optional<EquipmentTelemetry> SimulatedHardware::read_equipment(
    const SimTime now) {
  if (fault_active(FaultType::MissingTelemetry, SubsystemId::Equipment, now)) {
    return std::nullopt;
  }
  EquipmentTelemetry value{true, 8U, 2.0 + noise(-0.2, 0.2)};
  if (fault_active(FaultType::EquipmentFailure, SubsystemId::Equipment, now)) {
    value.operational = false;
    value.active_channels = 0U;
    value.vibration_mm_s = 45.0;
  }
  if (fault_active(FaultType::InvalidTelemetry, SubsystemId::Equipment, now)) {
    value.active_channels = 999U;
  }
  return value;
}

}  // namespace aeroguard

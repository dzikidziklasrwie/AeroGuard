#pragma once

#include "aeroguard/config.hpp"
#include "aeroguard/types.hpp"

#include <optional>
#include <random>

namespace aeroguard {

// The application depends on this interface, never on a device API. A real
// adapter can implement it later without changing monitoring policy.
class IHardwareAbstraction {
 public:
  virtual ~IHardwareAbstraction() = default;
  [[nodiscard]] virtual std::optional<PowerTelemetry> read_power(
      SimTime now) = 0;
  [[nodiscard]] virtual std::optional<CoolingTelemetry> read_cooling(
      SimTime now) = 0;
  [[nodiscard]] virtual std::optional<TemperatureTelemetry> read_temperature(
      SimTime now) = 0;
  [[nodiscard]] virtual std::optional<CommunicationTelemetry>
  read_communication(SimTime now) = 0;
  [[nodiscard]] virtual std::optional<ComputerTelemetry> read_computer(
      SimTime now) = 0;
  [[nodiscard]] virtual std::optional<EquipmentTelemetry> read_equipment(
      SimTime now) = 0;
};

class SimulatedHardware final : public IHardwareAbstraction {
 public:
  SimulatedHardware(ScenarioConfig scenario, std::uint32_t seed);

  [[nodiscard]] std::optional<PowerTelemetry> read_power(
      SimTime now) override;
  [[nodiscard]] std::optional<CoolingTelemetry> read_cooling(
      SimTime now) override;
  [[nodiscard]] std::optional<TemperatureTelemetry> read_temperature(
      SimTime now) override;
  [[nodiscard]] std::optional<CommunicationTelemetry> read_communication(
      SimTime now) override;
  [[nodiscard]] std::optional<ComputerTelemetry> read_computer(
      SimTime now) override;
  [[nodiscard]] std::optional<EquipmentTelemetry> read_equipment(
      SimTime now) override;

 private:
  [[nodiscard]] bool fault_active(FaultType type, SubsystemId subsystem,
                                  SimTime now) const noexcept;
  [[nodiscard]] double fault_magnitude(FaultType type, SubsystemId subsystem,
                                       SimTime now) const noexcept;
  [[nodiscard]] double noise(double minimum, double maximum);

  ScenarioConfig scenario_;
  std::mt19937 random_;
};

}  // namespace aeroguard

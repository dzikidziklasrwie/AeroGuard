#pragma once

#include <array>
#include <chrono>
#include <cstdint>
#include <optional>
#include <string_view>
#include <variant>

namespace aeroguard {

using SimTime = std::chrono::milliseconds;

enum class SubsystemId : std::uint8_t {
  Power = 0,
  Cooling,
  Temperature,
  Communication,
  Computer,
  Equipment,
  Count
};

inline constexpr std::size_t kSubsystemCount =
    static_cast<std::size_t>(SubsystemId::Count);

inline constexpr std::array<SubsystemId, kSubsystemCount> kAllSubsystems{
    SubsystemId::Power,         SubsystemId::Cooling,
    SubsystemId::Temperature,   SubsystemId::Communication,
    SubsystemId::Computer,      SubsystemId::Equipment};

enum class HealthStatus : std::uint8_t { Unknown, Healthy, Degraded, Fault };
enum class SystemState : std::uint8_t {
  Initializing,
  Normal,
  Degraded,
  Fault,
  Recovery
};
enum class Severity : std::uint8_t { Debug, Info, Warning, Error, Critical };

struct PowerTelemetry {
  double voltage_v{};
  double current_a{};
  double frequency_hz{};
  bool operator==(const PowerTelemetry&) const = default;
};

struct CoolingTelemetry {
  double coolant_temperature_c{};
  double fan_speed_rpm{};
  double flow_rate_lpm{};
  bool operator==(const CoolingTelemetry&) const = default;
};

struct TemperatureTelemetry {
  double sensor_1_c{};
  double sensor_2_c{};
  double ambient_c{};
  bool operator==(const TemperatureTelemetry&) const = default;
};

struct CommunicationTelemetry {
  double link_quality_percent{};
  double packet_loss_percent{};
  bool connected{};
  bool operator==(const CommunicationTelemetry&) const = default;
};

struct ComputerTelemetry {
  double cpu_load_percent{};
  double memory_used_percent{};
  bool watchdog_ok{};
  bool operator==(const ComputerTelemetry&) const = default;
};

struct EquipmentTelemetry {
  bool operational{};
  std::uint32_t active_channels{};
  double vibration_mm_s{};
  bool operator==(const EquipmentTelemetry&) const = default;
};

using TelemetryPayload =
    std::variant<PowerTelemetry, CoolingTelemetry, TemperatureTelemetry,
                 CommunicationTelemetry, ComputerTelemetry, EquipmentTelemetry>;

struct TelemetryMessage {
  SubsystemId subsystem{SubsystemId::Power};
  SimTime timestamp{};
  std::uint64_t sequence{};
  bool heartbeat{true};
  TelemetryPayload payload{PowerTelemetry{}};
  bool operator==(const TelemetryMessage&) const = default;
};

enum class ValidationCode : std::uint8_t {
  Valid,
  PayloadTypeMismatch,
  NonFiniteValue,
  OutOfRange,
  HeartbeatNotSet,
  SequenceNotIncreasing,
  TimestampRegression,
  InvalidSequence,
  NegativeTimestamp,
  UnknownSubsystem
};

struct TelemetryEvaluation {
  bool valid{};
  HealthStatus health{HealthStatus::Unknown};
  ValidationCode code{ValidationCode::Valid};
};

constexpr std::size_t to_index(const SubsystemId id) noexcept {
  return static_cast<std::size_t>(id);
}

[[nodiscard]] std::string_view to_string(SubsystemId value) noexcept;
[[nodiscard]] std::string_view to_string(HealthStatus value) noexcept;
[[nodiscard]] std::string_view to_string(SystemState value) noexcept;
[[nodiscard]] std::string_view to_string(Severity value) noexcept;
[[nodiscard]] std::string_view to_string(ValidationCode value) noexcept;
[[nodiscard]] std::optional<SubsystemId> subsystem_from_string(
    std::string_view value) noexcept;

}  // namespace aeroguard

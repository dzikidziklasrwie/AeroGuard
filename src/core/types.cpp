#include "aeroguard/types.hpp"

namespace aeroguard {

std::string_view to_string(const SubsystemId value) noexcept {
  switch (value) {
    case SubsystemId::Power:
      return "power";
    case SubsystemId::Cooling:
      return "cooling";
    case SubsystemId::Temperature:
      return "temperature";
    case SubsystemId::Communication:
      return "communication";
    case SubsystemId::Computer:
      return "computer";
    case SubsystemId::Equipment:
      return "equipment";
    case SubsystemId::Count:
      break;
  }
  return "unknown";
}

std::string_view to_string(const HealthStatus value) noexcept {
  switch (value) {
    case HealthStatus::Unknown:
      return "UNKNOWN";
    case HealthStatus::Healthy:
      return "HEALTHY";
    case HealthStatus::Degraded:
      return "DEGRADED";
    case HealthStatus::Fault:
      return "FAULT";
  }
  return "UNKNOWN";
}

std::string_view to_string(const SystemState value) noexcept {
  switch (value) {
    case SystemState::Initializing:
      return "INITIALIZING";
    case SystemState::Normal:
      return "NORMAL";
    case SystemState::Degraded:
      return "DEGRADED";
    case SystemState::Fault:
      return "FAULT";
    case SystemState::Recovery:
      return "RECOVERY";
  }
  return "INITIALIZING";
}

std::string_view to_string(const Severity value) noexcept {
  switch (value) {
    case Severity::Debug:
      return "DEBUG";
    case Severity::Info:
      return "INFO";
    case Severity::Warning:
      return "WARNING";
    case Severity::Error:
      return "ERROR";
    case Severity::Critical:
      return "CRITICAL";
  }
  return "INFO";
}

std::string_view to_string(const ValidationCode value) noexcept {
  switch (value) {
    case ValidationCode::Valid:
      return "VALID";
    case ValidationCode::PayloadTypeMismatch:
      return "PAYLOAD_TYPE_MISMATCH";
    case ValidationCode::NonFiniteValue:
      return "NON_FINITE_VALUE";
    case ValidationCode::OutOfRange:
      return "OUT_OF_RANGE";
    case ValidationCode::HeartbeatNotSet:
      return "HEARTBEAT_NOT_SET";
    case ValidationCode::SequenceNotIncreasing:
      return "SEQUENCE_NOT_INCREASING";
    case ValidationCode::TimestampRegression:
      return "TIMESTAMP_REGRESSION";
    case ValidationCode::InvalidSequence:
      return "INVALID_SEQUENCE";
    case ValidationCode::NegativeTimestamp:
      return "NEGATIVE_TIMESTAMP";
    case ValidationCode::UnknownSubsystem:
      return "UNKNOWN_SUBSYSTEM";
  }
  return "UNKNOWN_VALIDATION_ERROR";
}

std::optional<SubsystemId> subsystem_from_string(
    const std::string_view value) noexcept {
  for (const auto subsystem : kAllSubsystems) {
    if (to_string(subsystem) == value) {
      return subsystem;
    }
  }
  return std::nullopt;
}

}  // namespace aeroguard

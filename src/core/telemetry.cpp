#include "aeroguard/telemetry.hpp"

#include <algorithm>
#include <cmath>
#include <type_traits>

namespace aeroguard {
namespace {

[[nodiscard]] bool finite(const double value) noexcept {
  return std::isfinite(value);
}

[[nodiscard]] TelemetryEvaluation invalid(const ValidationCode code) noexcept {
  return {false, HealthStatus::Fault, code};
}

[[nodiscard]] TelemetryEvaluation valid(const HealthStatus status) noexcept {
  return {true, status, ValidationCode::Valid};
}

}  // namespace

TelemetryEvaluation evaluate_telemetry(const TelemetryMessage& message) noexcept {
  if (to_index(message.subsystem) >= kSubsystemCount) {
    return invalid(ValidationCode::UnknownSubsystem);
  }
  if (message.timestamp.count() < 0) {
    return invalid(ValidationCode::NegativeTimestamp);
  }
  if (message.sequence == 0U) {
    return invalid(ValidationCode::InvalidSequence);
  }
  if (!message.heartbeat) {
    return invalid(ValidationCode::HeartbeatNotSet);
  }
  if (message.payload.index() != to_index(message.subsystem)) {
    return invalid(ValidationCode::PayloadTypeMismatch);
  }

  return std::visit(
      [](const auto& payload) -> TelemetryEvaluation {
        using T = std::decay_t<decltype(payload)>;
        if constexpr (std::is_same_v<T, PowerTelemetry>) {
          if (!finite(payload.voltage_v) || !finite(payload.current_a) ||
              !finite(payload.frequency_hz)) {
            return invalid(ValidationCode::NonFiniteValue);
          }
          if (payload.voltage_v < 18.0 || payload.voltage_v > 36.0 ||
              payload.current_a < 0.0 || payload.current_a > 120.0 ||
              payload.frequency_hz < 350.0 || payload.frequency_hz > 450.0) {
            return invalid(ValidationCode::OutOfRange);
          }
          if (payload.voltage_v < 21.0 || payload.voltage_v > 33.0 ||
              payload.current_a > 110.0 || payload.frequency_hz < 370.0 ||
              payload.frequency_hz > 430.0) {
            return valid(HealthStatus::Fault);
          }
          if (payload.voltage_v < 24.0 || payload.voltage_v > 30.0 ||
              payload.current_a > 80.0 || payload.frequency_hz < 390.0 ||
              payload.frequency_hz > 410.0) {
            return valid(HealthStatus::Degraded);
          }
          return valid(HealthStatus::Healthy);
        } else if constexpr (std::is_same_v<T, CoolingTelemetry>) {
          if (!finite(payload.coolant_temperature_c) ||
              !finite(payload.fan_speed_rpm) || !finite(payload.flow_rate_lpm)) {
            return invalid(ValidationCode::NonFiniteValue);
          }
          if (payload.coolant_temperature_c < -60.0 ||
              payload.coolant_temperature_c > 150.0 ||
              payload.fan_speed_rpm < 0.0 || payload.fan_speed_rpm > 20'000.0 ||
              payload.flow_rate_lpm < 0.0 || payload.flow_rate_lpm > 50.0) {
            return invalid(ValidationCode::OutOfRange);
          }
          if (payload.coolant_temperature_c > 105.0 ||
              payload.fan_speed_rpm < 500.0 || payload.flow_rate_lpm < 1.0) {
            return valid(HealthStatus::Fault);
          }
          if (payload.coolant_temperature_c > 80.0 ||
              payload.fan_speed_rpm < 2'000.0 || payload.flow_rate_lpm < 4.0) {
            return valid(HealthStatus::Degraded);
          }
          return valid(HealthStatus::Healthy);
        } else if constexpr (std::is_same_v<T, TemperatureTelemetry>) {
          if (!finite(payload.sensor_1_c) || !finite(payload.sensor_2_c) ||
              !finite(payload.ambient_c)) {
            return invalid(ValidationCode::NonFiniteValue);
          }
          if (payload.sensor_1_c < -100.0 || payload.sensor_1_c > 250.0 ||
              payload.sensor_2_c < -100.0 || payload.sensor_2_c > 250.0 ||
              payload.ambient_c < -100.0 || payload.ambient_c > 250.0) {
            return invalid(ValidationCode::OutOfRange);
          }
          const auto hottest = std::max(payload.sensor_1_c, payload.sensor_2_c);
          if (hottest > 110.0) {
            return valid(HealthStatus::Fault);
          }
          if (hottest > 85.0 ||
              std::abs(payload.sensor_1_c - payload.sensor_2_c) > 20.0) {
            return valid(HealthStatus::Degraded);
          }
          return valid(HealthStatus::Healthy);
        } else if constexpr (std::is_same_v<T, CommunicationTelemetry>) {
          if (!finite(payload.link_quality_percent) ||
              !finite(payload.packet_loss_percent)) {
            return invalid(ValidationCode::NonFiniteValue);
          }
          if (payload.link_quality_percent < 0.0 ||
              payload.link_quality_percent > 100.0 ||
              payload.packet_loss_percent < 0.0 ||
              payload.packet_loss_percent > 100.0) {
            return invalid(ValidationCode::OutOfRange);
          }
          if (!payload.connected || payload.link_quality_percent < 20.0 ||
              payload.packet_loss_percent > 50.0) {
            return valid(HealthStatus::Fault);
          }
          if (payload.link_quality_percent < 65.0 ||
              payload.packet_loss_percent > 10.0) {
            return valid(HealthStatus::Degraded);
          }
          return valid(HealthStatus::Healthy);
        } else if constexpr (std::is_same_v<T, ComputerTelemetry>) {
          if (!finite(payload.cpu_load_percent) ||
              !finite(payload.memory_used_percent)) {
            return invalid(ValidationCode::NonFiniteValue);
          }
          if (payload.cpu_load_percent < 0.0 ||
              payload.cpu_load_percent > 100.0 ||
              payload.memory_used_percent < 0.0 ||
              payload.memory_used_percent > 100.0) {
            return invalid(ValidationCode::OutOfRange);
          }
          if (!payload.watchdog_ok || payload.cpu_load_percent > 98.0 ||
              payload.memory_used_percent > 98.0) {
            return valid(HealthStatus::Fault);
          }
          if (payload.cpu_load_percent > 85.0 ||
              payload.memory_used_percent > 90.0) {
            return valid(HealthStatus::Degraded);
          }
          return valid(HealthStatus::Healthy);
        } else {
          static_assert(std::is_same_v<T, EquipmentTelemetry>);
          if (!finite(payload.vibration_mm_s)) {
            return invalid(ValidationCode::NonFiniteValue);
          }
          if (payload.active_channels > 64U || payload.vibration_mm_s < 0.0 ||
              payload.vibration_mm_s > 100.0) {
            return invalid(ValidationCode::OutOfRange);
          }
          if (!payload.operational || payload.active_channels == 0U ||
              payload.vibration_mm_s > 40.0) {
            return valid(HealthStatus::Fault);
          }
          if (payload.active_channels < 4U || payload.vibration_mm_s > 12.0) {
            return valid(HealthStatus::Degraded);
          }
          return valid(HealthStatus::Healthy);
        }
      },
      message.payload);
}

}  // namespace aeroguard

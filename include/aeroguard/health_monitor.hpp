#pragma once

#include "aeroguard/logging.hpp"
#include "aeroguard/state_machine.hpp"
#include "aeroguard/telemetry.hpp"

#include <array>
#include <cstdint>
#include <optional>

namespace aeroguard {

struct SubsystemSnapshot {
  HealthStatus health{HealthStatus::Unknown};
  SimTime last_seen{};
  std::uint64_t last_sequence{};
  bool has_seen{};
  bool missing_reported{};
};

struct MonitorConfig {
  SimTime heartbeat_timeout{500};
  SimTime recovery_hold{1'000};
};

class HealthMonitor final {
 public:
  HealthMonitor(MonitorConfig config, ILogSink& log_sink);

  [[nodiscard]] TelemetryEvaluation ingest(
      const TelemetryMessage& message);
  void evaluate(SimTime now);
  void reset() noexcept;

  [[nodiscard]] SystemState state() const noexcept {
    return state_machine_.state();
  }
  [[nodiscard]] const SubsystemSnapshot& subsystem(
      SubsystemId id) const noexcept {
    return subsystems_[to_index(id)];
  }
  [[nodiscard]] std::uint64_t invalid_count() const noexcept {
    return invalid_count_;
  }
  [[nodiscard]] std::uint64_t missing_heartbeat_count() const noexcept {
    return missing_heartbeat_count_;
  }
  [[nodiscard]] std::uint64_t transition_count() const noexcept {
    return state_machine_.transition_count();
  }

 private:
  void set_desired_state(SystemState desired, SimTime now);
  void transition_to(SystemState target, SimTime now, std::string_view reason,
                     Severity severity);

  MonitorConfig config_;
  ILogSink& log_sink_;
  SystemStateMachine state_machine_;
  std::array<SubsystemSnapshot, kSubsystemCount> subsystems_{};
  std::optional<SimTime> recovery_started_;
  SystemState recovery_target_{SystemState::Normal};
  std::uint64_t invalid_count_{};
  std::uint64_t missing_heartbeat_count_{};
};

}  // namespace aeroguard

#include "aeroguard/health_monitor.hpp"

#include <algorithm>
#include <stdexcept>

namespace aeroguard {

HealthMonitor::HealthMonitor(const MonitorConfig config, ILogSink& log_sink)
    : config_(config), log_sink_(log_sink) {
  if (config_.heartbeat_timeout.count() <= 0 ||
      config_.recovery_hold.count() < 0) {
    throw std::invalid_argument("invalid health monitor timing configuration");
  }
}

TelemetryEvaluation HealthMonitor::ingest(const TelemetryMessage& message) {
  if (to_index(message.subsystem) >= kSubsystemCount) {
    ++invalid_count_;
    log_sink_.write_event(message.timestamp, Severity::Error,
                          "INVALID_TELEMETRY", std::nullopt,
                          to_string(ValidationCode::UnknownSubsystem));
    return {false, HealthStatus::Fault, ValidationCode::UnknownSubsystem};
  }
  auto& snapshot = subsystems_[to_index(message.subsystem)];
  auto evaluation = evaluate_telemetry(message);
  bool ordering_valid =
      evaluation.code != ValidationCode::InvalidSequence &&
      evaluation.code != ValidationCode::NegativeTimestamp;

  if (snapshot.has_seen && message.sequence <= snapshot.last_sequence) {
    evaluation = {false, HealthStatus::Fault,
                  ValidationCode::SequenceNotIncreasing};
    ordering_valid = false;
  } else if (snapshot.has_seen && message.timestamp < snapshot.last_seen) {
    evaluation = {false, HealthStatus::Fault,
                  ValidationCode::TimestampRegression};
    ordering_valid = false;
  }

  if (ordering_valid && snapshot.missing_reported) {
    log_sink_.write_event(message.timestamp, Severity::Info,
                          "HEARTBEAT_RESTORED", message.subsystem,
                          "Telemetry resumed after a missing heartbeat");
  }

  if (ordering_valid) {
    snapshot.has_seen = true;
    snapshot.missing_reported = false;
    snapshot.last_seen = message.timestamp;
    snapshot.last_sequence = message.sequence;
  }
  snapshot.health = evaluation.health;

  if (!evaluation.valid) {
    ++invalid_count_;
    log_sink_.write_event(message.timestamp, Severity::Error,
                          "INVALID_TELEMETRY", message.subsystem,
                          to_string(evaluation.code));
  }
  return evaluation;
}

void HealthMonitor::evaluate(const SimTime now) {
  for (const auto id : kAllSubsystems) {
    auto& snapshot = subsystems_[to_index(id)];
    const bool timed_out =
        (!snapshot.has_seen && now >= config_.heartbeat_timeout) ||
        (snapshot.has_seen && now >= snapshot.last_seen &&
         now - snapshot.last_seen >= config_.heartbeat_timeout);
    if (timed_out && !snapshot.missing_reported) {
      snapshot.health = HealthStatus::Fault;
      snapshot.missing_reported = true;
      ++missing_heartbeat_count_;
      log_sink_.write_event(now, Severity::Error, "MISSING_HEARTBEAT", id,
                            "No telemetry arrived within the heartbeat timeout");
    }
  }

  const bool any_fault = std::ranges::any_of(
      subsystems_, [](const auto& item) { return item.health == HealthStatus::Fault; });
  const bool any_degraded = std::ranges::any_of(subsystems_, [](const auto& item) {
    return item.health == HealthStatus::Degraded;
  });
  const bool all_healthy = std::ranges::all_of(subsystems_, [](const auto& item) {
    return item.health == HealthStatus::Healthy;
  });

  if (any_fault) {
    set_desired_state(SystemState::Fault, now);
  } else if (any_degraded) {
    set_desired_state(SystemState::Degraded, now);
  } else if (all_healthy) {
    set_desired_state(SystemState::Normal, now);
  } else {
    set_desired_state(SystemState::Initializing, now);
  }
}

void HealthMonitor::set_desired_state(const SystemState desired,
                                      const SimTime now) {
  const auto current = state_machine_.state();
  if (desired == SystemState::Initializing || desired == current) {
    return;
  }

  if (desired == SystemState::Fault) {
    recovery_started_.reset();
    transition_to(SystemState::Fault, now,
                  "One or more subsystems reported a fault",
                  Severity::Critical);
    return;
  }

  if (desired == SystemState::Degraded) {
    if (current == SystemState::Initializing || current == SystemState::Normal) {
      transition_to(SystemState::Degraded, now,
                    "One or more subsystems are operating with reduced margin",
                    Severity::Warning);
      return;
    }
    if (current == SystemState::Fault) {
      recovery_target_ = SystemState::Degraded;
      recovery_started_ = now;
      transition_to(SystemState::Recovery, now,
                    "Fault cleared; validating degraded operation",
                    Severity::Info);
      return;
    }
    if (current == SystemState::Recovery) {
      if (recovery_target_ != SystemState::Degraded || !recovery_started_) {
        recovery_target_ = SystemState::Degraded;
        recovery_started_ = now;
      }
      if (now - *recovery_started_ >= config_.recovery_hold) {
        recovery_started_.reset();
        transition_to(SystemState::Degraded, now,
                      "Recovery hold completed with degraded health",
                      Severity::Warning);
      }
    }
    return;
  }

  if (desired == SystemState::Normal) {
    if (current == SystemState::Initializing) {
      transition_to(SystemState::Normal, now,
                    "All subsystem heartbeats are valid", Severity::Info);
      return;
    }
    if (current == SystemState::Degraded || current == SystemState::Fault) {
      recovery_target_ = SystemState::Normal;
      recovery_started_ = now;
      transition_to(SystemState::Recovery, now,
                    "Health restored; recovery hold started", Severity::Info);
      return;
    }
    if (current == SystemState::Recovery) {
      if (recovery_target_ != SystemState::Normal || !recovery_started_) {
        recovery_target_ = SystemState::Normal;
        recovery_started_ = now;
      }
      if (now - *recovery_started_ >= config_.recovery_hold) {
        recovery_started_.reset();
        transition_to(SystemState::Normal, now,
                      "Recovery hold completed with healthy telemetry",
                      Severity::Info);
      }
    }
  }
}

void HealthMonitor::transition_to(const SystemState target, const SimTime now,
                                  const std::string_view reason,
                                  const Severity severity) {
  const auto from = state_machine_.state();
  if (state_machine_.request(target) == TransitionOutcome::Changed) {
    log_sink_.write_transition(now, from, target, reason, severity);
  }
}

void HealthMonitor::reset() noexcept {
  state_machine_.reset();
  subsystems_ = {};
  recovery_started_.reset();
  recovery_target_ = SystemState::Normal;
  invalid_count_ = 0U;
  missing_heartbeat_count_ = 0U;
}

}  // namespace aeroguard

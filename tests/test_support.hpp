#pragma once

#include "aeroguard/logging.hpp"
#include "aeroguard/types.hpp"

#include <optional>
#include <string>
#include <vector>

namespace aeroguard::test {

struct EventRecord {
  SimTime time{};
  Severity severity{Severity::Info};
  std::string code;
  std::optional<SubsystemId> subsystem;
  std::string message;
};

struct TransitionRecord {
  SimTime time{};
  SystemState from{SystemState::Initializing};
  SystemState to{SystemState::Initializing};
  std::string reason;
};

class RecordingLogSink final : public ILogSink {
 public:
  void write_metadata(const RunMetadata& value) override {
    metadata.push_back(value);
  }
  void write_telemetry(const TelemetryMessage&,
                       const TelemetryEvaluation& value) override {
    telemetry.push_back(value);
  }
  void write_event(const SimTime time, const Severity severity,
                   const std::string_view code,
                   const std::optional<SubsystemId> subsystem,
                   const std::string_view message) override {
    events.push_back({time, severity, std::string(code), subsystem,
                      std::string(message)});
  }
  void write_transition(const SimTime time, const SystemState from,
                        const SystemState to, const std::string_view reason,
                        Severity) override {
    transitions.push_back({time, from, to, std::string(reason)});
  }
  void write_summary(SimTime, const SimulationSummary& value) override {
    summaries.push_back(value);
  }

  std::vector<RunMetadata> metadata;
  std::vector<TelemetryEvaluation> telemetry;
  std::vector<EventRecord> events;
  std::vector<TransitionRecord> transitions;
  std::vector<SimulationSummary> summaries;
};

inline TelemetryMessage healthy_message(const SubsystemId id,
                                         const std::uint64_t sequence = 1U,
                                         const SimTime time = SimTime{0}) {
  TelemetryPayload payload;
  switch (id) {
    case SubsystemId::Power:
      payload = PowerTelemetry{27.5, 20.0, 400.0};
      break;
    case SubsystemId::Cooling:
      payload = CoolingTelemetry{48.0, 4'500.0, 8.0};
      break;
    case SubsystemId::Temperature:
      payload = TemperatureTelemetry{62.0, 63.0, 24.0};
      break;
    case SubsystemId::Communication:
      payload = CommunicationTelemetry{96.0, 0.3, true};
      break;
    case SubsystemId::Computer:
      payload = ComputerTelemetry{43.0, 57.0, true};
      break;
    case SubsystemId::Equipment:
      payload = EquipmentTelemetry{true, 8U, 2.0};
      break;
    case SubsystemId::Count:
      payload = PowerTelemetry{};
      break;
  }
  return {id, time, sequence, true, payload};
}

}  // namespace aeroguard::test

#pragma once

#include "aeroguard/types.hpp"

#include <cstdint>
#include <filesystem>
#include <fstream>
#include <mutex>
#include <optional>
#include <string>
#include <string_view>

namespace aeroguard {

struct RunMetadata {
  std::string scenario;
  std::string description;
  std::uint32_t seed{};
  SimTime tick{};
  SimTime duration{};
};

struct SimulationSummary {
  std::string scenario;
  std::uint32_t seed{};
  SystemState final_state{SystemState::Initializing};
  std::uint64_t ticks{};
  std::uint64_t telemetry_count{};
  std::uint64_t invalid_count{};
  std::uint64_t missing_heartbeat_count{};
  std::uint64_t dropped_message_count{};
  std::uint64_t state_transition_count{};
};

class ILogSink {
 public:
  virtual ~ILogSink() = default;
  virtual void write_metadata(const RunMetadata& metadata) = 0;
  virtual void write_telemetry(const TelemetryMessage& message,
                               const TelemetryEvaluation& evaluation) = 0;
  virtual void write_event(SimTime time, Severity severity,
                           std::string_view event_code,
                           std::optional<SubsystemId> subsystem,
                           std::string_view message) = 0;
  virtual void write_transition(SimTime time, SystemState from, SystemState to,
                                std::string_view reason,
                                Severity severity) = 0;
  virtual void write_summary(SimTime time,
                             const SimulationSummary& summary) = 0;
};

class NullLogSink final : public ILogSink {
 public:
  void write_metadata(const RunMetadata&) override {}
  void write_telemetry(const TelemetryMessage&,
                       const TelemetryEvaluation&) override {}
  void write_event(SimTime, Severity, std::string_view,
                   std::optional<SubsystemId>, std::string_view) override {}
  void write_transition(SimTime, SystemState, SystemState, std::string_view,
                        Severity) override {}
  void write_summary(SimTime, const SimulationSummary&) override {}
};

class JsonLinesLogger final : public ILogSink {
 public:
  explicit JsonLinesLogger(const std::filesystem::path& output_path);
  ~JsonLinesLogger() override;

  JsonLinesLogger(const JsonLinesLogger&) = delete;
  JsonLinesLogger& operator=(const JsonLinesLogger&) = delete;

  void write_metadata(const RunMetadata& metadata) override;
  void write_telemetry(const TelemetryMessage& message,
                       const TelemetryEvaluation& evaluation) override;
  void write_event(SimTime time, Severity severity, std::string_view event_code,
                   std::optional<SubsystemId> subsystem,
                   std::string_view message) override;
  void write_transition(SimTime time, SystemState from, SystemState to,
                        std::string_view reason, Severity severity) override;
  void write_summary(SimTime time, const SimulationSummary& summary) override;

  void flush();

 private:
  void write_line(std::string line);

  std::ofstream stream_;
  std::mutex mutex_;
};

}  // namespace aeroguard

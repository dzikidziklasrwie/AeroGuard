#include "aeroguard/logging.hpp"

#include <array>
#include <stdexcept>
#include <type_traits>

#include <nlohmann/json.hpp>

namespace aeroguard {
namespace {

using json = nlohmann::json;

[[nodiscard]] json base_record(const std::string_view type, const SimTime time) {
  return {{"schema_version", 1},
          {"record_type", type},
          {"timestamp_ms", time.count()}};
}

[[nodiscard]] json payload_to_json(const TelemetryPayload& payload) {
  return std::visit(
      [](const auto& value) -> json {
        using T = std::decay_t<decltype(value)>;
        if constexpr (std::is_same_v<T, PowerTelemetry>) {
          return {{"voltage_v", value.voltage_v},
                  {"current_a", value.current_a},
                  {"frequency_hz", value.frequency_hz}};
        } else if constexpr (std::is_same_v<T, CoolingTelemetry>) {
          return {{"coolant_temperature_c", value.coolant_temperature_c},
                  {"fan_speed_rpm", value.fan_speed_rpm},
                  {"flow_rate_lpm", value.flow_rate_lpm}};
        } else if constexpr (std::is_same_v<T, TemperatureTelemetry>) {
          return {{"sensor_1_c", value.sensor_1_c},
                  {"sensor_2_c", value.sensor_2_c},
                  {"ambient_c", value.ambient_c}};
        } else if constexpr (std::is_same_v<T, CommunicationTelemetry>) {
          return {{"link_quality_percent", value.link_quality_percent},
                  {"packet_loss_percent", value.packet_loss_percent},
                  {"connected", value.connected}};
        } else if constexpr (std::is_same_v<T, ComputerTelemetry>) {
          return {{"cpu_load_percent", value.cpu_load_percent},
                  {"memory_used_percent", value.memory_used_percent},
                  {"watchdog_ok", value.watchdog_ok}};
        } else {
          static_assert(std::is_same_v<T, EquipmentTelemetry>);
          return {{"operational", value.operational},
                  {"active_channels", value.active_channels},
                  {"vibration_mm_s", value.vibration_mm_s}};
        }
      },
      payload);
}

}  // namespace

JsonLinesLogger::JsonLinesLogger(const std::filesystem::path& output_path) {
  if (output_path.has_parent_path()) {
    std::filesystem::create_directories(output_path.parent_path());
  }
  stream_.open(output_path, std::ios::out | std::ios::trunc);
  if (!stream_) {
    throw std::runtime_error("cannot open log output: " + output_path.string());
  }
}

JsonLinesLogger::~JsonLinesLogger() {
  try {
    flush();
  } catch (...) {
    // Destructors must not propagate an I/O exception.
  }
}

void JsonLinesLogger::write_metadata(const RunMetadata& metadata) {
  auto record = base_record("metadata", SimTime{0});
  record["scenario"] = metadata.scenario;
  record["description"] = metadata.description;
  record["seed"] = metadata.seed;
  record["tick_ms"] = metadata.tick.count();
  record["duration_ms"] = metadata.duration.count();
  record["subsystems"] = json::array();
  for (const auto subsystem : kAllSubsystems) {
    record["subsystems"].push_back(to_string(subsystem));
  }
  write_line(record.dump());
}

void JsonLinesLogger::write_telemetry(
    const TelemetryMessage& message,
    const TelemetryEvaluation& evaluation) {
  auto record = base_record("telemetry", message.timestamp);
  record["sequence"] = message.sequence;
  record["subsystem"] = to_string(message.subsystem);
  record["health"] = to_string(evaluation.health);
  record["valid"] = evaluation.valid;
  record["reason"] = to_string(evaluation.code);
  record["data"] = payload_to_json(message.payload);
  write_line(record.dump());
}

void JsonLinesLogger::write_event(const SimTime time, const Severity severity,
                                  const std::string_view event_code,
                                  const std::optional<SubsystemId> subsystem,
                                  const std::string_view message) {
  auto record = base_record("event", time);
  record["severity"] = to_string(severity);
  record["event_code"] = event_code;
  record["subsystem"] =
      subsystem ? json(to_string(*subsystem)) : json(nullptr);
  record["message"] = message;
  write_line(record.dump());
}

void JsonLinesLogger::write_transition(const SimTime time,
                                       const SystemState from,
                                       const SystemState to,
                                       const std::string_view reason,
                                       const Severity severity) {
  auto record = base_record("state_transition", time);
  record["from"] = to_string(from);
  record["to"] = to_string(to);
  record["reason"] = reason;
  record["severity"] = to_string(severity);
  write_line(record.dump());
}

void JsonLinesLogger::write_summary(const SimTime time,
                                    const SimulationSummary& summary) {
  auto record = base_record("summary", time);
  record["scenario"] = summary.scenario;
  record["seed"] = summary.seed;
  record["final_state"] = to_string(summary.final_state);
  record["ticks"] = summary.ticks;
  record["telemetry_count"] = summary.telemetry_count;
  record["invalid_count"] = summary.invalid_count;
  record["missing_heartbeat_count"] = summary.missing_heartbeat_count;
  record["dropped_message_count"] = summary.dropped_message_count;
  record["state_transition_count"] = summary.state_transition_count;
  write_line(record.dump());
}

void JsonLinesLogger::flush() {
  std::lock_guard lock(mutex_);
  stream_.flush();
  if (!stream_) {
    throw std::runtime_error("failed to flush JSON Lines output");
  }
}

void JsonLinesLogger::write_line(std::string line) {
  std::lock_guard lock(mutex_);
  stream_ << line << '\n';
  if (!stream_) {
    throw std::runtime_error("failed to write JSON Lines output");
  }
}

}  // namespace aeroguard

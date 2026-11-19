#include "aeroguard/clock.hpp"
#include "aeroguard/config.hpp"
#include "aeroguard/hal.hpp"
#include "aeroguard/logging.hpp"
#include "aeroguard/simulator.hpp"

#include <gtest/gtest.h>

#include <algorithm>
#include <chrono>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <string>

#include <nlohmann/json.hpp>

namespace aeroguard {
namespace {

const std::filesystem::path kSourceDirectory{AEROGUARD_SOURCE_DIR};

class TemporaryDirectory final {
 public:
  TemporaryDirectory() {
    const auto suffix = std::chrono::steady_clock::now().time_since_epoch().count();
    path_ = std::filesystem::temp_directory_path() /
            ("aeroguard-tests-" + std::to_string(suffix));
    std::filesystem::create_directories(path_);
  }
  ~TemporaryDirectory() { std::filesystem::remove_all(path_); }
  [[nodiscard]] const std::filesystem::path& path() const noexcept {
    return path_;
  }

 private:
  std::filesystem::path path_;
};

[[nodiscard]] std::string read_text(const std::filesystem::path& path) {
  std::ifstream stream(path);
  std::ostringstream contents;
  contents << stream.rdbuf();
  return contents.str();
}

[[nodiscard]] SimulationSummary run_to_file(const ScenarioConfig& scenario,
                                            const SimulationOptions options,
                                            const std::filesystem::path& path) {
  JsonLinesLogger logger(path);
  Simulator simulator(scenario, options, logger);
  const auto summary = simulator.run();
  logger.flush();
  return summary;
}

[[nodiscard]] std::vector<nlohmann::json> parse_lines(
    const std::filesystem::path& path) {
  std::ifstream stream(path);
  std::vector<nlohmann::json> result;
  std::string line;
  while (std::getline(stream, line)) {
    if (!line.empty()) {
      result.push_back(nlohmann::json::parse(line));
    }
  }
  return result;
}

}  // namespace

TEST(REQ001_DeterministicReplay, SameSeedProducesByteIdenticalJsonLines) {
  TemporaryDirectory temporary;
  const auto scenario = load_scenario(kSourceDirectory / "scenarios" /
                                      "normal-operation.json");
  SimulationOptions options;
  options.seed = 7U;
  options.duration = SimTime{1'000};
  options.tick = SimTime{100};

  static_cast<void>(
      run_to_file(scenario, options, temporary.path() / "first.jsonl"));
  static_cast<void>(
      run_to_file(scenario, options, temporary.path() / "second.jsonl"));
  EXPECT_EQ(read_text(temporary.path() / "first.jsonl"),
            read_text(temporary.path() / "second.jsonl"));
}

TEST(REQ001_DeterministicReplay, RawMt19937MappingHasPortableGoldenSample) {
  ScenarioConfig scenario{"golden", "golden", SimTime{100}, {}};
  SimulatedHardware hardware(scenario, 42U);
  const auto power = hardware.read_power(SimTime{0});
  ASSERT_TRUE(power.has_value());
  EXPECT_DOUBLE_EQ(power->voltage_v, 27.412178080016748);
  EXPECT_DOUBLE_EQ(power->current_a, 21.186171936802566);
  EXPECT_DOUBLE_EQ(power->frequency_hz, 400.63100003618746);
}

TEST(REQ003_SimulationConfiguration, RejectsInvalidOrFractionalTickGeometry) {
  NullLogSink log;
  ScenarioConfig scenario{"test", "test", SimTime{1'000}, {}};
  SimulationOptions zero_tick;
  zero_tick.tick = SimTime{0};
  EXPECT_THROW(Simulator(scenario, zero_tick, log), std::invalid_argument);

  SimulationOptions fractional;
  fractional.duration = SimTime{1'050};
  fractional.tick = SimTime{100};
  EXPECT_THROW(Simulator(scenario, fractional, log), std::invalid_argument);
}

TEST(REQ004_Simulator, NormalRunPollsAllSixSubsystemsOnEveryTick) {
  TemporaryDirectory temporary;
  const auto scenario = load_scenario(kSourceDirectory / "scenarios" /
                                      "normal-operation.json");
  SimulationOptions options;
  options.duration = SimTime{1'000};
  options.tick = SimTime{100};
  const auto summary =
      run_to_file(scenario, options, temporary.path() / "normal.jsonl");
  EXPECT_EQ(summary.ticks, 10U);
  EXPECT_EQ(summary.telemetry_count, 60U);
  EXPECT_EQ(summary.final_state, SystemState::Normal);
  EXPECT_EQ(summary.dropped_message_count, 0U);
}

TEST(REQ018_JSONLines, WritesMetadataFirstAndSummaryLastWithSchemaV1) {
  TemporaryDirectory temporary;
  const auto scenario = load_scenario(kSourceDirectory / "scenarios" /
                                      "normal-operation.json");
  SimulationOptions options;
  options.duration = SimTime{500};
  options.tick = SimTime{100};
  const auto path = temporary.path() / "schema.jsonl";
  static_cast<void>(run_to_file(scenario, options, path));
  const auto records = parse_lines(path);

  ASSERT_GE(records.size(), 3U);
  EXPECT_EQ(records.front().at("record_type"), "metadata");
  EXPECT_EQ(records.back().at("record_type"), "summary");
  for (const auto& record : records) {
    EXPECT_EQ(record.at("schema_version"), 1);
    EXPECT_TRUE(record.contains("record_type"));
    EXPECT_TRUE(record.contains("timestamp_ms"));
  }
}

TEST(REQ005_JSONLinesTelemetry, ContainsIdentityHealthValidityReasonAndData) {
  TemporaryDirectory temporary;
  const auto scenario = load_scenario(kSourceDirectory / "scenarios" /
                                      "normal-operation.json");
  SimulationOptions options;
  options.duration = SimTime{100};
  const auto path = temporary.path() / "typed.jsonl";
  static_cast<void>(run_to_file(scenario, options, path));
  const auto records = parse_lines(path);
  const auto telemetry = std::ranges::find_if(records, [](const auto& item) {
    return item.at("record_type") == "telemetry";
  });
  ASSERT_NE(telemetry, records.end());
  EXPECT_TRUE(telemetry->contains("sequence"));
  EXPECT_TRUE(telemetry->contains("subsystem"));
  EXPECT_TRUE(telemetry->contains("health"));
  EXPECT_TRUE(telemetry->contains("valid"));
  EXPECT_TRUE(telemetry->contains("reason"));
  EXPECT_TRUE(telemetry->at("data").is_object());
}

TEST(REQ007_Integration, CommunicationOutageFaultsThenRecoversToNormal) {
  TemporaryDirectory temporary;
  const auto scenario = load_scenario(kSourceDirectory / "scenarios" /
                                      "communication-interruption.json");
  SimulationOptions options;
  const auto path = temporary.path() / "outage.jsonl";
  const auto summary = run_to_file(scenario, options, path);
  EXPECT_EQ(summary.missing_heartbeat_count, 1U);
  EXPECT_EQ(summary.final_state, SystemState::Normal);

  bool saw_missing = false;
  bool saw_fault = false;
  bool saw_recovery = false;
  for (const auto& record : parse_lines(path)) {
    const auto type = record.at("record_type").get<std::string>();
    if (type == "event" && record.at("event_code") == "MISSING_HEARTBEAT") {
      saw_missing = true;
      EXPECT_TRUE(record.contains("severity"));
      EXPECT_EQ(record.at("subsystem"), "communication");
      EXPECT_TRUE(record.contains("message"));
    }
    if (type == "state_transition" && record.at("to") == "FAULT") {
      saw_fault = true;
    }
    if (type == "state_transition" && record.at("to") == "RECOVERY") {
      saw_recovery = true;
    }
  }
  EXPECT_TRUE(saw_missing);
  EXPECT_TRUE(saw_fault);
  EXPECT_TRUE(saw_recovery);
}

TEST(REQ006_Integration, InvalidSensorScenarioRecordsRejectedTelemetry) {
  TemporaryDirectory temporary;
  const auto scenario = load_scenario(kSourceDirectory / "scenarios" /
                                      "invalid-sensor-data.json");
  SimulationOptions options;
  const auto summary = run_to_file(
      scenario, options, temporary.path() / "invalid.jsonl");
  EXPECT_GT(summary.invalid_count, 0U);
  EXPECT_EQ(summary.final_state, SystemState::Normal);
}

TEST(REQ009_VirtualClock, AdvancesAndResetsWithoutWallClockDelay) {
  ManualSimulationClock clock(SimTime{25});
  EXPECT_EQ(clock.now(), SimTime{0});
  clock.advance();
  clock.advance();
  EXPECT_EQ(clock.now(), SimTime{50});
  clock.reset();
  EXPECT_EQ(clock.now(), SimTime{0});
  EXPECT_THROW(ManualSimulationClock(SimTime{0}), std::invalid_argument);
}

TEST(REQ010_SimulatedHAL, IsolatesInjectedFaultToConfiguredSubsystemAndWindow) {
  ScenarioConfig scenario{
      "hal", "hal", SimTime{1'000},
      {{FaultType::EquipmentFailure, SubsystemId::Equipment, SimTime{100},
        SimTime{200}, 1.0}}};
  SimulatedHardware hardware(scenario, 42U);
  ASSERT_TRUE(hardware.read_power(SimTime{150}).has_value());
  const auto before = hardware.read_equipment(SimTime{50});
  const auto during = hardware.read_equipment(SimTime{150});
  const auto after = hardware.read_equipment(SimTime{350});
  ASSERT_TRUE(before && during && after);
  EXPECT_TRUE(before->operational);
  EXPECT_FALSE(during->operational);
  EXPECT_TRUE(after->operational);
}

}  // namespace aeroguard

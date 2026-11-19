#include "aeroguard/config.hpp"

#include <gtest/gtest.h>

#include <chrono>
#include <filesystem>
#include <fstream>
#include <limits>
#include <string>

namespace aeroguard {
namespace {

const std::filesystem::path kSourceDirectory{AEROGUARD_SOURCE_DIR};

class TemporaryJson final {
 public:
  explicit TemporaryJson(const std::string_view contents) {
    const auto suffix = std::chrono::steady_clock::now().time_since_epoch().count();
    path_ = std::filesystem::temp_directory_path() /
            ("aeroguard-config-" + std::to_string(suffix) + ".json");
    std::ofstream stream(path_);
    stream << contents;
    if (!stream) {
      throw std::runtime_error("failed to create temporary test JSON");
    }
  }
  ~TemporaryJson() { std::filesystem::remove(path_); }
  [[nodiscard]] const std::filesystem::path& path() const noexcept {
    return path_;
  }

 private:
  std::filesystem::path path_;
};

}  // namespace

TEST(REQ002_ScenarioConfiguration, ResolvesBothSlugAndExplicitFile) {
  const auto directory = kSourceDirectory / "scenarios";
  const auto by_slug = resolve_scenario_path("normal-operation", directory);
  const auto by_file = resolve_scenario_path(by_slug.string(), directory);
  EXPECT_EQ(std::filesystem::equivalent(by_slug, by_file), true);
  EXPECT_THROW(
      {
        const auto ignored =
            resolve_scenario_path("does-not-exist", directory);
        static_cast<void>(ignored);
      },
      ScenarioConfigError);
}

TEST(REQ003_SimulationConfiguration, ParsesScenarioDurationAndFaultTiming) {
  const auto scenario = load_scenario(kSourceDirectory / "scenarios" /
                                      "communication-interruption.json");
  EXPECT_EQ(scenario.name, "communication-interruption");
  EXPECT_EQ(scenario.default_duration, SimTime{8'000});
  ASSERT_EQ(scenario.faults.size(), 1U);
  EXPECT_EQ(scenario.faults.front().start, SimTime{2'000});
  EXPECT_EQ(scenario.faults.front().duration, SimTime{2'000});
  EXPECT_EQ(scenario.faults.front().subsystem, SubsystemId::Communication);
}

TEST(REQ017_ScenarioCatalog, ContainsAllSevenRequiredScenarioFiles) {
  const auto files = list_scenario_files(kSourceDirectory / "scenarios");
  ASSERT_EQ(files.size(), 7U);
  const std::vector<std::string> expected{
      "communication-interruption", "invalid-sensor-data",
      "multiple-subsystem-failures", "normal-operation",
      "power-fluctuations", "recovery-after-temporary-failure",
      "simulated-overheating"};
  for (std::size_t index = 0; index < expected.size(); ++index) {
    EXPECT_EQ(files[index].stem().string(), expected[index]);
  }
}

TEST(REQ021_ScenarioDiagnostics, RejectsMalformedAndUnsupportedConfiguration) {
  const TemporaryJson malformed{"{not-json"};
  EXPECT_THROW(
      {
        const auto ignored = load_scenario(malformed.path());
        static_cast<void>(ignored);
      },
      ScenarioConfigError);

  const TemporaryJson unknown_fault{R"({
    "name":"bad", "description":"bad fault", "duration_ms":1000,
    "faults":[{"type":"not_supported", "subsystem":"power",
      "start_ms":0, "duration_ms":100}]
  })"};
  EXPECT_THROW(
      {
        const auto ignored = load_scenario(unknown_fault.path());
        static_cast<void>(ignored);
      },
      ScenarioConfigError);
}

TEST(REQ021_ScenarioDiagnostics, RejectsOverflowingOrOutOfBoundsFaultWindow) {
  const TemporaryJson overflow{R"({
    "name":"overflow", "description":"hostile timing", "duration_ms":1000,
    "faults":[{"type":"missing_telemetry", "subsystem":"power",
      "start_ms":9223372036854775807, "duration_ms":10}]
  })"};
  EXPECT_THROW(
      {
        const auto ignored = load_scenario(overflow.path());
        static_cast<void>(ignored);
      },
      ScenarioConfigError);

  const TemporaryJson negative{R"({
    "name":"negative", "description":"hostile timing", "duration_ms":1000,
    "faults":[{"type":"missing_telemetry", "subsystem":"power",
      "start_ms":-1, "duration_ms":10}]
  })"};
  EXPECT_THROW(
      {
        const auto ignored = load_scenario(negative.path());
        static_cast<void>(ignored);
      },
      ScenarioConfigError);
}

}  // namespace aeroguard

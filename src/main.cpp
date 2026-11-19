#include "aeroguard/config.hpp"
#include "aeroguard/logging.hpp"
#include "aeroguard/simulator.hpp"

#include <charconv>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

#include <nlohmann/json.hpp>

namespace {

struct CliOptions {
  std::string scenario{"normal-operation"};
  std::filesystem::path scenarios_dir{"scenarios"};
  std::filesystem::path output{"output/aeroguard.jsonl"};
  std::filesystem::path export_directory;
  std::uint32_t seed{42U};
  std::int64_t duration_ms{};
  std::int64_t tick_ms{100};
  bool list_scenarios{};
  bool help{};
  bool output_was_set{};
  bool duration_was_set{};
};

[[nodiscard]] std::string_view require_value(const int argc, char* argv[],
                                             int& index,
                                             const std::string_view option) {
  if (index + 1 >= argc) {
    throw std::invalid_argument("missing value for " + std::string(option));
  }
  ++index;
  return argv[index];
}

template <typename Integer>
[[nodiscard]] Integer parse_integer(const std::string_view text,
                                    const std::string_view option) {
  Integer value{};
  const auto* begin = text.data();
  const auto* end = text.data() + text.size();
  const auto [position, error] = std::from_chars(begin, end, value);
  if (error != std::errc{} || position != end) {
    throw std::invalid_argument("invalid integer for " + std::string(option) +
                                ": " + std::string(text));
  }
  return value;
}

[[nodiscard]] CliOptions parse_cli(const int argc, char* argv[]) {
  CliOptions result;
  for (int index = 1; index < argc; ++index) {
    const std::string_view argument{argv[index]};
    if (argument == "--help" || argument == "-h") {
      result.help = true;
    } else if (argument == "--list-scenarios") {
      result.list_scenarios = true;
    } else if (argument == "--scenario") {
      result.scenario = require_value(argc, argv, index, argument);
    } else if (argument == "--scenarios-dir") {
      result.scenarios_dir = require_value(argc, argv, index, argument);
    } else if (argument == "--output") {
      result.output = require_value(argc, argv, index, argument);
      result.output_was_set = true;
    } else if (argument == "--export-all") {
      result.export_directory = require_value(argc, argv, index, argument);
    } else if (argument == "--seed") {
      result.seed = parse_integer<std::uint32_t>(
          require_value(argc, argv, index, argument), argument);
    } else if (argument == "--duration") {
      result.duration_ms = parse_integer<std::int64_t>(
          require_value(argc, argv, index, argument), argument);
      result.duration_was_set = true;
    } else if (argument == "--tick") {
      result.tick_ms = parse_integer<std::int64_t>(
          require_value(argc, argv, index, argument), argument);
    } else {
      throw std::invalid_argument("unknown argument: " + std::string(argument));
    }
  }
  if (result.duration_was_set && result.duration_ms <= 0) {
    throw std::invalid_argument("if set, --duration must be positive");
  }
  if (result.tick_ms <= 0) {
    throw std::invalid_argument("--tick must be positive");
  }
  if (!result.export_directory.empty() && result.output_was_set) {
    throw std::invalid_argument("do not combine --output with --export-all");
  }
  return result;
}

void print_help() {
  std::cout
      << "AeroGuard - Avionics Health Monitoring Simulator\n\n"
      << "Usage:\n"
      << "  aeroguard [--scenario <file-or-slug>] [--seed <u32>]\n"
      << "            [--duration <ms>] [--tick <ms>] [--output <file>]\n"
      << "  aeroguard --export-all <directory> [--scenarios-dir <directory>]\n"
      << "  aeroguard --list-scenarios [--scenarios-dir <directory>]\n\n"
      << "Options:\n"
      << "  --scenario       Scenario JSON path or slug (default: normal-operation)\n"
      << "  --seed           Deterministic random seed (default: 42)\n"
      << "  --duration       Override scenario duration in milliseconds\n"
      << "  --tick           Simulation step in milliseconds (default: 100)\n"
      << "  --output         JSON Lines output (default: output/aeroguard.jsonl)\n"
      << "  --export-all     Run every scenario and create a dashboard manifest\n"
      << "  --scenarios-dir  Scenario directory (default: scenarios)\n"
      << "  --list-scenarios Print available scenarios\n"
      << "  --help, -h       Show this help\n";
}

[[nodiscard]] aeroguard::SimulationOptions simulation_options(
    const CliOptions& cli) {
  aeroguard::SimulationOptions result;
  result.seed = cli.seed;
  result.duration = aeroguard::SimTime{cli.duration_ms};
  result.tick = aeroguard::SimTime{cli.tick_ms};
  return result;
}

[[nodiscard]] aeroguard::SimulationSummary run_scenario(
    const aeroguard::ScenarioConfig& scenario, const CliOptions& cli,
    const std::filesystem::path& output) {
  aeroguard::JsonLinesLogger logger(output);
  aeroguard::Simulator simulator(scenario, simulation_options(cli), logger);
  auto summary = simulator.run();
  logger.flush();
  return summary;
}

[[nodiscard]] std::string label_from_id(std::string id) {
  for (auto& character : id) {
    if (character == '-') {
      character = ' ';
    }
  }
  if (!id.empty() && id.front() >= 'a' && id.front() <= 'z') {
    id.front() = static_cast<char>(id.front() - 'a' + 'A');
  }
  return id;
}

int export_all(const CliOptions& cli) {
  std::filesystem::create_directories(cli.export_directory);
  nlohmann::json manifest{{"schema_version", 1},
                          {"generated_by", "AeroGuard"},
                          {"scenarios", nlohmann::json::array()}};

  for (const auto& path : aeroguard::list_scenario_files(cli.scenarios_dir)) {
    const auto scenario = aeroguard::load_scenario(path);
    const auto filename = path.stem().string() + ".jsonl";
    const auto output = cli.export_directory / filename;
    const auto summary = run_scenario(scenario, cli, output);
    const auto duration = cli.duration_ms == 0 ? scenario.default_duration.count()
                                               : cli.duration_ms;
    manifest["scenarios"].push_back(
        {{"id", path.stem().string()},
         {"label", label_from_id(path.stem().string())},
         {"description", scenario.description},
         {"file", filename},
         {"duration_ms", duration},
         {"tick_ms", cli.tick_ms},
         {"final_state", aeroguard::to_string(summary.final_state)}});
    std::cout << "exported " << output.string() << " ("
              << aeroguard::to_string(summary.final_state) << ")\n";
  }

  const auto manifest_path = cli.export_directory / "manifest.json";
  std::ofstream output(manifest_path, std::ios::out | std::ios::trunc);
  if (!output) {
    throw std::runtime_error("cannot open manifest output: " +
                             manifest_path.string());
  }
  output << manifest.dump(2) << '\n';
  if (!output) {
    throw std::runtime_error("failed to write manifest: " +
                             manifest_path.string());
  }
  std::cout << "wrote " << manifest_path.string() << '\n';
  return 0;
}

}  // namespace

int main(const int argc, char* argv[]) {
  try {
    const auto cli = parse_cli(argc, argv);
    if (cli.help) {
      print_help();
      return 0;
    }
    if (cli.list_scenarios) {
      for (const auto& path : aeroguard::list_scenario_files(cli.scenarios_dir)) {
        const auto scenario = aeroguard::load_scenario(path);
        std::cout << path.stem().string() << "\t" << scenario.description
                  << '\n';
      }
      return 0;
    }
    if (!cli.export_directory.empty()) {
      return export_all(cli);
    }

    const auto path =
        aeroguard::resolve_scenario_path(cli.scenario, cli.scenarios_dir);
    const auto scenario = aeroguard::load_scenario(path);
    const auto summary = run_scenario(scenario, cli, cli.output);
    std::cout << "AeroGuard completed scenario '" << scenario.name << "'\n"
              << "  final state: "
              << aeroguard::to_string(summary.final_state) << '\n'
              << "  ticks: " << summary.ticks << '\n'
              << "  telemetry records: " << summary.telemetry_count << '\n'
              << "  output: " << cli.output.string() << '\n';
    return 0;
  } catch (const std::exception& error) {
    std::cerr << "AeroGuard error: " << error.what() << '\n';
    return 2;
  }
}

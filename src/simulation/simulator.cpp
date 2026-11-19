#include "aeroguard/simulator.hpp"

#include <stdexcept>
#include <utility>

namespace aeroguard {

Simulator::Simulator(ScenarioConfig scenario, SimulationOptions options,
                     ILogSink& log_sink,
                     std::unique_ptr<IHardwareAbstraction> hardware,
                     std::unique_ptr<ISimulationClock> clock)
    : scenario_(std::move(scenario)),
      options_(options),
      duration_(options_.duration.count() == 0 ? scenario_.default_duration
                                               : options_.duration),
      log_sink_(log_sink),
      hardware_(std::move(hardware)),
      clock_(std::move(clock)),
      subsystems_(make_subsystems()),
      monitor_({options_.heartbeat_timeout, options_.recovery_hold}, log_sink_) {
  if (duration_.count() <= 0 || options_.tick.count() <= 0) {
    throw std::invalid_argument("simulation duration and tick must be positive");
  }
  if (duration_.count() % options_.tick.count() != 0) {
    throw std::invalid_argument(
        "simulation duration must be an exact multiple of the tick");
  }
  if (!hardware_) {
    hardware_ =
        std::make_unique<SimulatedHardware>(scenario_, options_.seed);
  }
  if (!clock_) {
    clock_ = std::make_unique<ManualSimulationClock>(options_.tick);
  }
}

SimulationSummary Simulator::run() {
  clock_->reset();
  monitor_.reset();

  log_sink_.write_metadata(
      {scenario_.name, scenario_.description, options_.seed, options_.tick,
       duration_});

  SimulationSummary summary;
  summary.scenario = scenario_.name;
  summary.seed = options_.seed;

  while (clock_->now() < duration_) {
    const auto now = clock_->now();
    for (auto& subsystem : subsystems_) {
      auto message = subsystem->poll(*hardware_, now);
      if (message && !queue_.try_push(std::move(*message))) {
        ++summary.dropped_message_count;
        log_sink_.write_event(now, Severity::Error, "QUEUE_OVERFLOW",
                              subsystem->id(),
                              "Telemetry queue reached its fixed capacity");
      }
    }

    while (auto message = queue_.try_pop()) {
      const auto evaluation = monitor_.ingest(*message);
      log_sink_.write_telemetry(*message, evaluation);
      ++summary.telemetry_count;
    }
    monitor_.evaluate(now);
    ++summary.ticks;
    clock_->advance();
  }

  monitor_.evaluate(clock_->now());
  summary.final_state = monitor_.state();
  summary.invalid_count = monitor_.invalid_count();
  summary.missing_heartbeat_count = monitor_.missing_heartbeat_count();
  summary.state_transition_count = monitor_.transition_count();
  log_sink_.write_summary(clock_->now(), summary);
  return summary;
}

}  // namespace aeroguard

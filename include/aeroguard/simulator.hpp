#pragma once

#include "aeroguard/bounded_queue.hpp"
#include "aeroguard/clock.hpp"
#include "aeroguard/config.hpp"
#include "aeroguard/hal.hpp"
#include "aeroguard/health_monitor.hpp"
#include "aeroguard/logging.hpp"
#include "aeroguard/subsystem.hpp"

#include <memory>

namespace aeroguard {

class Simulator final {
 public:
  Simulator(ScenarioConfig scenario, SimulationOptions options,
            ILogSink& log_sink,
            std::unique_ptr<IHardwareAbstraction> hardware = {},
            std::unique_ptr<ISimulationClock> clock = {});

  [[nodiscard]] SimulationSummary run();

 private:
  static constexpr std::size_t kQueueCapacity = 64U;

  ScenarioConfig scenario_;
  SimulationOptions options_;
  SimTime duration_{};
  ILogSink& log_sink_;
  std::unique_ptr<IHardwareAbstraction> hardware_;
  std::unique_ptr<ISimulationClock> clock_;
  SubsystemCollection subsystems_;
  HealthMonitor monitor_;
  BoundedQueue<TelemetryMessage, kQueueCapacity> queue_;
};

}  // namespace aeroguard

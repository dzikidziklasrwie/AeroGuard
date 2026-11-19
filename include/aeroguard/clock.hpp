#pragma once

#include "aeroguard/types.hpp"

#include <stdexcept>

namespace aeroguard {

class ISimulationClock {
 public:
  virtual ~ISimulationClock() = default;
  [[nodiscard]] virtual SimTime now() const noexcept = 0;
  virtual void advance() noexcept = 0;
  virtual void reset() noexcept = 0;
};

class ManualSimulationClock final : public ISimulationClock {
 public:
  explicit ManualSimulationClock(SimTime tick) : tick_(tick) {
    if (tick_.count() <= 0) {
      throw std::invalid_argument("simulation tick must be positive");
    }
  }

  [[nodiscard]] SimTime now() const noexcept override { return now_; }
  void advance() noexcept override { now_ += tick_; }
  void reset() noexcept override { now_ = SimTime{0}; }
  [[nodiscard]] SimTime tick() const noexcept { return tick_; }

 private:
  SimTime tick_;
  SimTime now_{};
};

}  // namespace aeroguard

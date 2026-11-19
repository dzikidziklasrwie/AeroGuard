#pragma once

#include "aeroguard/types.hpp"

#include <cstdint>

namespace aeroguard {

enum class TransitionOutcome : std::uint8_t { Changed, NoChange, Rejected };

class SystemStateMachine final {
 public:
  [[nodiscard]] SystemState state() const noexcept { return state_; }
  [[nodiscard]] std::uint64_t transition_count() const noexcept {
    return transition_count_;
  }

  [[nodiscard]] static bool can_transition(SystemState from,
                                           SystemState to) noexcept;
  [[nodiscard]] TransitionOutcome request(SystemState target) noexcept;
  void reset() noexcept;

 private:
  SystemState state_{SystemState::Initializing};
  std::uint64_t transition_count_{};
};

}  // namespace aeroguard

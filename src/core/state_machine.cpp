#include "aeroguard/state_machine.hpp"

namespace aeroguard {

bool SystemStateMachine::can_transition(const SystemState from,
                                        const SystemState to) noexcept {
  if (from == to) {
    return true;
  }
  switch (from) {
    case SystemState::Initializing:
      return to == SystemState::Normal || to == SystemState::Degraded ||
             to == SystemState::Fault;
    case SystemState::Normal:
      return to == SystemState::Degraded || to == SystemState::Fault;
    case SystemState::Degraded:
      return to == SystemState::Fault || to == SystemState::Recovery;
    case SystemState::Fault:
      return to == SystemState::Recovery;
    case SystemState::Recovery:
      return to == SystemState::Normal || to == SystemState::Degraded ||
             to == SystemState::Fault;
  }
  return false;
}

TransitionOutcome SystemStateMachine::request(const SystemState target) noexcept {
  if (target == state_) {
    return TransitionOutcome::NoChange;
  }
  if (!can_transition(state_, target)) {
    return TransitionOutcome::Rejected;
  }
  state_ = target;
  ++transition_count_;
  return TransitionOutcome::Changed;
}

void SystemStateMachine::reset() noexcept {
  state_ = SystemState::Initializing;
  transition_count_ = 0U;
}

}  // namespace aeroguard

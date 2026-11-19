#include "aeroguard/state_machine.hpp"

#include <gtest/gtest.h>

#include <array>

namespace aeroguard {
namespace {

constexpr std::array<SystemState, 5> kStates{
    SystemState::Initializing, SystemState::Normal, SystemState::Degraded,
    SystemState::Fault, SystemState::Recovery};

[[nodiscard]] constexpr bool expected_transition(const SystemState from,
                                                 const SystemState to) {
  if (from == to) {
    return true;
  }
  if (from == SystemState::Initializing) {
    return to == SystemState::Normal || to == SystemState::Degraded ||
           to == SystemState::Fault;
  }
  if (from == SystemState::Normal) {
    return to == SystemState::Degraded || to == SystemState::Fault;
  }
  if (from == SystemState::Degraded) {
    return to == SystemState::Fault || to == SystemState::Recovery;
  }
  if (from == SystemState::Fault) {
    return to == SystemState::Recovery;
  }
  return to == SystemState::Normal || to == SystemState::Degraded ||
         to == SystemState::Fault;
}

}  // namespace

// REQ-011/REQ-012: these cases cover every cell in the five-by-five transition matrix.
TEST(REQ012_StateMachine, ImplementsCompleteValidAndInvalidTransitionMatrix) {
  for (const auto from : kStates) {
    for (const auto to : kStates) {
      EXPECT_EQ(SystemStateMachine::can_transition(from, to),
                expected_transition(from, to))
          << "from=" << to_string(from) << " to=" << to_string(to);
    }
  }
}

TEST(REQ012_StateMachine, RejectsInvalidRequestWithoutMutatingStateOrCount) {
  SystemStateMachine machine;
  EXPECT_EQ(machine.request(SystemState::Recovery), TransitionOutcome::Rejected);
  EXPECT_EQ(machine.state(), SystemState::Initializing);
  EXPECT_EQ(machine.transition_count(), 0U);

  EXPECT_EQ(machine.request(SystemState::Normal), TransitionOutcome::Changed);
  EXPECT_EQ(machine.request(SystemState::Recovery), TransitionOutcome::Rejected);
  EXPECT_EQ(machine.state(), SystemState::Normal);
  EXPECT_EQ(machine.transition_count(), 1U);
}

TEST(REQ011_StateMachine, SupportsAllFiveStatesThroughValidRecoveryPaths) {
  SystemStateMachine machine;
  EXPECT_EQ(machine.request(SystemState::Degraded), TransitionOutcome::Changed);
  EXPECT_EQ(machine.request(SystemState::Fault), TransitionOutcome::Changed);
  EXPECT_EQ(machine.request(SystemState::Recovery), TransitionOutcome::Changed);
  EXPECT_EQ(machine.request(SystemState::Normal), TransitionOutcome::Changed);
  EXPECT_EQ(machine.state(), SystemState::Normal);
}

TEST(REQ012_StateMachine, SameStateRequestIsAnExplicitNoOp) {
  SystemStateMachine machine;
  EXPECT_EQ(machine.request(SystemState::Initializing),
            TransitionOutcome::NoChange);
  EXPECT_EQ(machine.transition_count(), 0U);
}

}  // namespace aeroguard

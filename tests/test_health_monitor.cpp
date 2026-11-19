#include "aeroguard/health_monitor.hpp"

#include "test_support.hpp"

#include <gtest/gtest.h>

#include <algorithm>

namespace aeroguard {
namespace {

void ingest_all_healthy(HealthMonitor& monitor, const SimTime time,
                        const std::uint64_t sequence) {
  for (const auto id : kAllSubsystems) {
    EXPECT_TRUE(monitor.ingest(test::healthy_message(id, sequence, time)).valid);
  }
}

}  // namespace

TEST(REQ011_HealthMonitor, RemainsInitializingUntilEverySubsystemIsKnown) {
  test::RecordingLogSink log;
  HealthMonitor monitor({SimTime{500}, SimTime{1'000}}, log);
  for (std::size_t index = 0; index + 1U < kAllSubsystems.size(); ++index) {
    static_cast<void>(
        monitor.ingest(test::healthy_message(kAllSubsystems[index])));
  }
  monitor.evaluate(SimTime{0});
  EXPECT_EQ(monitor.state(), SystemState::Initializing);

  static_cast<void>(
      monitor.ingest(test::healthy_message(SubsystemId::Equipment)));
  monitor.evaluate(SimTime{0});
  EXPECT_EQ(monitor.state(), SystemState::Normal);
}

TEST(REQ007_HeartbeatMonitoring, DetectsOnlyTheSubsystemWhoseHeartbeatExpired) {
  test::RecordingLogSink log;
  HealthMonitor monitor({SimTime{500}, SimTime{1'000}}, log);
  ingest_all_healthy(monitor, SimTime{0}, 1U);
  monitor.evaluate(SimTime{0});

  for (const auto id : kAllSubsystems) {
    if (id != SubsystemId::Communication) {
      static_cast<void>(
          monitor.ingest(test::healthy_message(id, 2U, SimTime{400})));
    }
  }
  monitor.evaluate(SimTime{500});

  EXPECT_EQ(monitor.state(), SystemState::Fault);
  EXPECT_EQ(monitor.missing_heartbeat_count(), 1U);
  EXPECT_TRUE(monitor.subsystem(SubsystemId::Communication).missing_reported);
  EXPECT_EQ(monitor.subsystem(SubsystemId::Power).health,
            HealthStatus::Healthy);
}

TEST(REQ014_FaultHandling, InvalidTelemetryMovesOverallSystemToFault) {
  test::RecordingLogSink log;
  HealthMonitor monitor({SimTime{5'000}, SimTime{1'000}}, log);
  ingest_all_healthy(monitor, SimTime{0}, 1U);
  monitor.evaluate(SimTime{0});

  auto invalid = test::healthy_message(SubsystemId::Temperature, 2U,
                                       SimTime{100});
  std::get<TemperatureTelemetry>(invalid.payload).sensor_1_c = 999.0;
  const auto result = monitor.ingest(invalid);
  monitor.evaluate(SimTime{100});

  EXPECT_FALSE(result.valid);
  EXPECT_EQ(monitor.state(), SystemState::Fault);
  EXPECT_EQ(monitor.invalid_count(), 1U);
  ASSERT_FALSE(log.events.empty());
  EXPECT_EQ(log.events.back().code, "INVALID_TELEMETRY");
}

TEST(REQ015_Recovery, EnforcesRecoveryHoldBeforeReturningToNormal) {
  test::RecordingLogSink log;
  HealthMonitor monitor({SimTime{5'000}, SimTime{1'000}}, log);
  ingest_all_healthy(monitor, SimTime{0}, 1U);
  monitor.evaluate(SimTime{0});

  auto fault = test::healthy_message(SubsystemId::Computer, 2U, SimTime{100});
  std::get<ComputerTelemetry>(fault.payload).watchdog_ok = false;
  static_cast<void>(monitor.ingest(fault));
  monitor.evaluate(SimTime{100});
  ASSERT_EQ(monitor.state(), SystemState::Fault);

  static_cast<void>(monitor.ingest(
      test::healthy_message(SubsystemId::Computer, 3U, SimTime{200})));
  monitor.evaluate(SimTime{200});
  EXPECT_EQ(monitor.state(), SystemState::Recovery);
  monitor.evaluate(SimTime{1'199});
  EXPECT_EQ(monitor.state(), SystemState::Recovery);
  monitor.evaluate(SimTime{1'200});
  EXPECT_EQ(monitor.state(), SystemState::Normal);
}

TEST(REQ013_SubsystemIsolation, FaultDoesNotOverwriteHealthySubsystemStatus) {
  test::RecordingLogSink log;
  HealthMonitor monitor({SimTime{5'000}, SimTime{1'000}}, log);
  ingest_all_healthy(monitor, SimTime{0}, 1U);
  auto fault = test::healthy_message(SubsystemId::Equipment, 2U, SimTime{100});
  std::get<EquipmentTelemetry>(fault.payload).operational = false;
  static_cast<void>(monitor.ingest(fault));
  monitor.evaluate(SimTime{100});

  EXPECT_EQ(monitor.subsystem(SubsystemId::Equipment).health,
            HealthStatus::Fault);
  EXPECT_EQ(monitor.subsystem(SubsystemId::Power).health,
            HealthStatus::Healthy);
  EXPECT_EQ(monitor.subsystem(SubsystemId::Cooling).health,
            HealthStatus::Healthy);
}

TEST(REQ006_HealthMonitorValidation, RejectsReplayAndUnknownEnumSafely) {
  test::RecordingLogSink log;
  HealthMonitor monitor({SimTime{500}, SimTime{1'000}}, log);
  EXPECT_TRUE(
      monitor.ingest(test::healthy_message(SubsystemId::Power, 2U)).valid);
  const auto replay =
      monitor.ingest(test::healthy_message(SubsystemId::Power, 2U));
  EXPECT_FALSE(replay.valid);
  EXPECT_EQ(replay.code, ValidationCode::SequenceNotIncreasing);

  auto unknown = test::healthy_message(SubsystemId::Power, 3U);
  unknown.subsystem = static_cast<SubsystemId>(255);
  const auto unknown_result = monitor.ingest(unknown);
  EXPECT_FALSE(unknown_result.valid);
  EXPECT_EQ(unknown_result.code, ValidationCode::UnknownSubsystem);
  EXPECT_EQ(monitor.invalid_count(), 2U);
}

TEST(REQ006_HealthMonitorValidation, RejectsTimestampRegressionWithoutRefreshingHeartbeat) {
  test::RecordingLogSink log;
  HealthMonitor monitor({SimTime{500}, SimTime{1'000}}, log);
  EXPECT_TRUE(monitor
                  .ingest(test::healthy_message(SubsystemId::Power, 1U,
                                                SimTime{200}))
                  .valid);
  const auto regressed = monitor.ingest(
      test::healthy_message(SubsystemId::Power, 2U, SimTime{100}));
  EXPECT_FALSE(regressed.valid);
  EXPECT_EQ(regressed.code, ValidationCode::TimestampRegression);
  EXPECT_EQ(monitor.subsystem(SubsystemId::Power).last_seen, SimTime{200});
  EXPECT_EQ(monitor.subsystem(SubsystemId::Power).last_sequence, 1U);
}

TEST(REQ016_EventLogging, EmitsStructuredHeartbeatAndTransitionEvents) {
  test::RecordingLogSink log;
  HealthMonitor monitor({SimTime{100}, SimTime{50}}, log);
  ingest_all_healthy(monitor, SimTime{0}, 1U);
  monitor.evaluate(SimTime{0});
  monitor.evaluate(SimTime{100});

  const auto missing = std::ranges::find_if(log.events, [](const auto& event) {
    return event.code == "MISSING_HEARTBEAT";
  });
  ASSERT_NE(missing, log.events.end());
  EXPECT_EQ(missing->severity, Severity::Error);
  EXPECT_TRUE(missing->subsystem.has_value());
  EXPECT_FALSE(missing->message.empty());
  ASSERT_GE(log.transitions.size(), 2U);
  EXPECT_EQ(log.transitions.front().from, SystemState::Initializing);
  EXPECT_EQ(log.transitions.front().to, SystemState::Normal);
}

}  // namespace aeroguard

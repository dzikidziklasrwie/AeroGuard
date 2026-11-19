#include "aeroguard/telemetry.hpp"

#include "test_support.hpp"

#include <gtest/gtest.h>

#include <limits>

namespace aeroguard {

// REQ-004/REQ-005: every source has a distinct strongly typed payload.
TEST(REQ005_Telemetry, AcceptsHealthyTypedPayloadForAllSixSubsystems) {
  for (const auto id : kAllSubsystems) {
    const auto result = evaluate_telemetry(test::healthy_message(id));
    EXPECT_TRUE(result.valid) << to_string(id);
    EXPECT_EQ(result.health, HealthStatus::Healthy) << to_string(id);
    EXPECT_EQ(result.code, ValidationCode::Valid) << to_string(id);
  }
}

TEST(REQ006_TelemetryValidation, RejectsPayloadThatDoesNotMatchSource) {
  auto message = test::healthy_message(SubsystemId::Power);
  message.payload = CoolingTelemetry{48.0, 4'500.0, 8.0};
  const auto result = evaluate_telemetry(message);
  EXPECT_FALSE(result.valid);
  EXPECT_EQ(result.code, ValidationCode::PayloadTypeMismatch);
  EXPECT_EQ(result.health, HealthStatus::Fault);
}

TEST(REQ006_TelemetryValidation, RejectsNonFiniteAndOutOfRangeMeasurements) {
  auto non_finite = test::healthy_message(SubsystemId::Temperature);
  std::get<TemperatureTelemetry>(non_finite.payload).sensor_1_c =
      std::numeric_limits<double>::quiet_NaN();
  EXPECT_EQ(evaluate_telemetry(non_finite).code,
            ValidationCode::NonFiniteValue);

  auto out_of_range = test::healthy_message(SubsystemId::Communication);
  std::get<CommunicationTelemetry>(out_of_range.payload)
      .link_quality_percent = 101.0;
  EXPECT_EQ(evaluate_telemetry(out_of_range).code,
            ValidationCode::OutOfRange);
}

TEST(REQ006_TelemetryValidation, RejectsMissingHeartbeatAndUnknownSubsystem) {
  auto no_heartbeat = test::healthy_message(SubsystemId::Computer);
  no_heartbeat.heartbeat = false;
  EXPECT_EQ(evaluate_telemetry(no_heartbeat).code,
            ValidationCode::HeartbeatNotSet);

  auto unknown = test::healthy_message(SubsystemId::Power);
  unknown.subsystem = static_cast<SubsystemId>(255);
  EXPECT_EQ(evaluate_telemetry(unknown).code,
            ValidationCode::UnknownSubsystem);
}

TEST(REQ006_TelemetryValidation, RejectsZeroSequenceAndNegativeTimestamp) {
  auto zero_sequence = test::healthy_message(SubsystemId::Power);
  zero_sequence.sequence = 0U;
  EXPECT_EQ(evaluate_telemetry(zero_sequence).code,
            ValidationCode::InvalidSequence);

  auto negative_time = test::healthy_message(SubsystemId::Cooling);
  negative_time.timestamp = SimTime{-1};
  EXPECT_EQ(evaluate_telemetry(negative_time).code,
            ValidationCode::NegativeTimestamp);
}

TEST(REQ006_TelemetryValidation, ClassifiesValidDegradedAndFaultReadings) {
  auto degraded = test::healthy_message(SubsystemId::Power);
  std::get<PowerTelemetry>(degraded.payload).voltage_v = 23.0;
  const auto degraded_result = evaluate_telemetry(degraded);
  EXPECT_TRUE(degraded_result.valid);
  EXPECT_EQ(degraded_result.health, HealthStatus::Degraded);

  auto fault = test::healthy_message(SubsystemId::Computer);
  std::get<ComputerTelemetry>(fault.payload).watchdog_ok = false;
  const auto fault_result = evaluate_telemetry(fault);
  EXPECT_TRUE(fault_result.valid);
  EXPECT_EQ(fault_result.health, HealthStatus::Fault);
}

}  // namespace aeroguard

#pragma once

#include "aeroguard/types.hpp"

namespace aeroguard {

// Validation separates malformed telemetry from an operational health
// classification. A malformed payload always maps to Fault.
[[nodiscard]] TelemetryEvaluation evaluate_telemetry(
    const TelemetryMessage& message) noexcept;

}  // namespace aeroguard

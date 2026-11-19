# Telemetry model and scenarios

All measurements and thresholds in this document are fictional. They exist to
exercise validation and fault paths. Don't use them as aircraft limits.

## Message envelope

`TelemetryMessage` carries a `SubsystemId`, virtual timestamp, increasing
sequence number, heartbeat flag, and one `TelemetryPayload` variant. The payload
alternative must match the subsystem. Validation reports three values:

- whether the message is structurally and numerically valid;
- its `HEALTHY`, `DEGRADED`, or `FAULT` classification;
- a stable reason code such as `VALID`, `OUT_OF_RANGE`, or
  `PAYLOAD_TYPE_MISMATCH`.

Validation rejects an unknown subsystem, mismatched payload, unset heartbeat,
non-finite value, or value outside the accepted input domain and maps it to
fault health. The health monitor rejects a non-increasing sequence as
`SEQUENCE_NOT_INCREASING`.

## Fictional validation limits

The comparisons below match `src/core/telemetry.cpp`. Boundaries are inclusive
unless a rule shows `<` or `>`.

### Power supply

| Field | Accepted input | Degraded | Fault |
|---|---|---|---|
| `voltage_v` | 18 to 36 V | `< 24` or `> 30` | `< 21` or `> 33` |
| `current_a` | 0 to 120 A | `> 80` | `> 110` |
| `frequency_hz` | 350 to 450 Hz | `< 390` or `> 410` | `< 370` or `> 430` |

Fault rules take precedence over degraded rules.

### Cooling system

| Field | Accepted input | Degraded | Fault |
|---|---|---|---|
| `coolant_temperature_c` | -60 to 150 °C | `> 80` | `> 105` |
| `fan_speed_rpm` | 0 to 20,000 rpm | `< 2,000` | `< 500` |
| `flow_rate_lpm` | 0 to 50 L/min | `< 4` | `< 1` |

### Temperature sensors

`sensor_1_c`, `sensor_2_c`, and `ambient_c` accept -100 to 250 °C. The hotter
sensor causes fault above 110 °C. The validator returns degraded health above
85 °C or when the two sensor readings differ by more than 20 °C.

### Communication module

| Field | Accepted input | Degraded | Fault |
|---|---|---|---|
| `link_quality_percent` | 0 to 100% | `< 65` | `< 20` |
| `packet_loss_percent` | 0 to 100% | `> 10` | `> 50` |
| `connected` | Boolean | n/a | `false` |

### Onboard computer

| Field | Accepted input | Degraded | Fault |
|---|---|---|---|
| `cpu_load_percent` | 0 to 100% | `> 85` | `> 98` |
| `memory_used_percent` | 0 to 100% | `> 90` | `> 98` |
| `watchdog_ok` | Boolean | n/a | `false` |

### Equipment status monitor

| Field | Accepted input | Degraded | Fault |
|---|---|---|---|
| `active_channels` | 0 to 64 | `< 4` | `0` |
| `vibration_mm_s` | 0 to 100 mm/s | `> 12` | `> 40` |
| `operational` | Boolean | n/a | `false` |

## Fault injection types

| JSON `type` | Simulator effect |
|---|---|
| `missing_telemetry` | The selected HAL read returns no sample. |
| `invalid_telemetry` | The simulator sets a selected measurement outside its accepted input domain. |
| `overheating` | Cooling or temperature values cross their thermal fault rule. |
| `power_fluctuation` | Voltage alternates between low and high phases every 300 virtual milliseconds. |
| `cooling_failure` | Coolant temperature rises while fan speed and flow fall to fault values. |
| `computer_overload` | CPU/memory reach fault values and the watchdog reports false. |
| `equipment_failure` | The unit reports inoperative, zero channels, and fault-level vibration. |

Each fault has a subsystem, start, duration, and positive finite magnitude. Its
active interval is half-open: `[start_ms, start_ms + duration_ms)`.

## Supplied schedule

| Scenario | Duration | Scheduled fault intervals |
|---|---:|---|
| `normal-operation` | 8,000 ms | None |
| `communication-interruption` | 8,000 ms | Communications missing from 2,000 to 4,000 ms |
| `invalid-sensor-data` | 8,000 ms | Invalid temperature input from 2,000 to 3,800 ms |
| `simulated-overheating` | 9,000 ms | Temperature overheat from 2,500 to 5,000 ms |
| `power-fluctuations` | 9,000 ms | Power fluctuation from 2,000 to 5,000 ms |
| `multiple-subsystem-failures` | 11,000 ms | Communications 1,800-4,800; cooling 2,400-6,400; computer 3,000-7,500; equipment 3,500-8,500 ms |
| `recovery-after-temporary-failure` | 8,000 ms | Communications missing from 2,000 to 3,500 ms |

The default tick is 100 ms and seed is 42. CLI overrides apply to seed, tick,
and total duration. Scenario parsing rejects negative starts, non-positive fault
durations or magnitudes, non-finite magnitudes, unknown subsystem/fault names,
and faults extending beyond the scenario duration.

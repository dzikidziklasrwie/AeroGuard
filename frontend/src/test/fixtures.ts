export const manifestFixture = {
  schema_version: 1,
  generated_by: 'AeroGuard C++ simulator',
  scenarios: [
    {
      id: 'normal-operation',
      label: 'Normal operation',
      description: 'All simulated subsystems remain within configured limits.',
      file: 'normal-operation.jsonl',
      duration_ms: 1000,
      tick_ms: 100,
      final_state: 'NORMAL',
    },
  ],
};

const records = [
  {
    schema_version: 1,
    record_type: 'metadata',
    timestamp_ms: 0,
    scenario: 'normal-operation',
    description: 'All systems nominal',
    seed: 42,
    tick_ms: 100,
    duration_ms: 1000,
    subsystems: ['power', 'cooling', 'temperature', 'communication', 'computer', 'equipment'],
  },
  {
    schema_version: 1,
    record_type: 'state_transition',
    timestamp_ms: 0,
    from: 'INITIALIZING',
    to: 'NORMAL',
    reason: 'All subsystem heartbeats are valid',
    severity: 'INFO',
  },
  ...[
    ['power', { voltage_v: 28.1, current_a: 11.4, frequency_hz: 400.1 }],
    ['cooling', { coolant_temperature_c: 42.2, fan_speed_rpm: 3140, flow_rate_lpm: 18.7 }],
    ['temperature', { sensor_1_c: 51.4, sensor_2_c: 50.8, ambient_c: 22.1 }],
    ['communication', { link_quality_percent: 99, packet_loss_percent: 0.2, connected: true }],
    ['computer', { cpu_load_percent: 37, memory_used_percent: 44, watchdog_ok: true }],
    ['equipment', { operational: true, active_channels: 8, vibration_mm_s: 1.2 }],
  ].map(([subsystem, data], index) => ({
    schema_version: 1,
    record_type: 'telemetry',
    timestamp_ms: 0,
    sequence: index,
    subsystem,
    health: 'HEALTHY',
    valid: true,
    reason: '',
    data,
  })),
  {
    schema_version: 1,
    record_type: 'summary',
    timestamp_ms: 1000,
    scenario: 'normal-operation',
    seed: 42,
    final_state: 'NORMAL',
    ticks: 10,
    telemetry_count: 60,
    invalid_count: 0,
    missing_heartbeat_count: 0,
    dropped_message_count: 0,
    state_transition_count: 1,
  },
];

export const jsonlFixture = records.map((record) => JSON.stringify(record)).join('\n');

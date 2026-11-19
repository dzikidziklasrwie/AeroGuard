export const SUBSYSTEM_IDS = [
  'power',
  'cooling',
  'temperature',
  'communication',
  'computer',
  'equipment',
] as const;

export type SubsystemId = (typeof SUBSYSTEM_IDS)[number];

export const HEALTH_STATES = ['UNKNOWN', 'HEALTHY', 'DEGRADED', 'FAULT'] as const;
export type HealthState = (typeof HEALTH_STATES)[number];

export const SYSTEM_STATES = [
  'INITIALIZING',
  'NORMAL',
  'DEGRADED',
  'FAULT',
  'RECOVERY',
] as const;
export type SystemState = (typeof SYSTEM_STATES)[number];

export const SEVERITIES = ['DEBUG', 'INFO', 'WARNING', 'ERROR', 'CRITICAL'] as const;
export type Severity = (typeof SEVERITIES)[number];

export interface RecordBase {
  schema_version: 1;
  record_type: string;
  timestamp_ms: number;
}

export interface MetadataRecord extends RecordBase {
  record_type: 'metadata';
  scenario: string;
  description: string;
  seed: number;
  tick_ms: number;
  duration_ms: number;
  subsystems: string[];
}

export interface TelemetryRecord extends RecordBase {
  record_type: 'telemetry';
  sequence: number;
  subsystem: string;
  health: HealthState;
  valid: boolean;
  reason: string;
  data: Record<string, unknown>;
}

export interface EventRecord extends RecordBase {
  record_type: 'event';
  severity: Severity;
  event_code: string;
  subsystem: string | null;
  message: string;
}

export interface StateTransitionRecord extends RecordBase {
  record_type: 'state_transition';
  from: SystemState;
  to: SystemState;
  reason: string;
  severity: Severity;
}

export interface SummaryRecord extends RecordBase {
  record_type: 'summary';
  scenario: string;
  seed: number;
  final_state: SystemState;
  ticks: number;
  telemetry_count: number;
  invalid_count: number;
  missing_heartbeat_count: number;
  dropped_message_count: number;
  state_transition_count: number;
}

export type SimulationRecord =
  | MetadataRecord
  | TelemetryRecord
  | EventRecord
  | StateTransitionRecord
  | SummaryRecord;

export interface SimulationDataset {
  metadata: MetadataRecord;
  records: SimulationRecord[];
  summary?: SummaryRecord;
  warnings: string[];
  source: string;
}

export interface ScenarioManifestEntry {
  id: string;
  label: string;
  description: string;
  file: string;
  duration_ms: number;
  tick_ms: number;
  final_state: SystemState;
}

export interface ScenarioManifest {
  schema_version: 1;
  generated_by?: string;
  scenarios: ScenarioManifestEntry[];
  source?: 'api' | 'static';
}

export interface RunArchiveEntry {
  id: number | string;
  scenario: string;
  label: string;
  description: string;
  final_state: SystemState;
  duration_ms: number;
  tick_ms: number;
  record_count?: number;
  source_sha256?: string;
  imported_at_utc?: string;
  replay_url: string;
  source: 'api' | 'static';
}

export const SUBSYSTEM_LABELS: Record<string, string> = {
  power: 'Power Supply',
  cooling: 'Cooling System',
  temperature: 'Temperature Sensors',
  communication: 'Communication Module',
  computer: 'Onboard Computer',
  equipment: 'Equipment Monitor',
};

import {
  HEALTH_STATES,
  SEVERITIES,
  SYSTEM_STATES,
  type EventRecord,
  type MetadataRecord,
  type ScenarioManifest,
  type SimulationDataset,
  type SimulationRecord,
  type StateTransitionRecord,
  type SummaryRecord,
  type TelemetryRecord,
} from '../types';

const isObject = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

const isFiniteNumber = (value: unknown): value is number =>
  typeof value === 'number' && Number.isFinite(value);

const isNonNegativeNumber = (value: unknown): value is number =>
  isFiniteNumber(value) && value >= 0;

const isString = (value: unknown): value is string => typeof value === 'string';
const isStringArray = (value: unknown): value is string[] =>
  Array.isArray(value) && value.every(isString);

const isEnumValue = <T extends string>(values: readonly T[], value: unknown): value is T =>
  isString(value) && values.includes(value as T);

function validateBase(value: Record<string, unknown>): string | undefined {
  if (value.schema_version !== 1) return 'unsupported or missing schema_version';
  if (!isString(value.record_type)) return 'record_type must be a string';
  if (!isNonNegativeNumber(value.timestamp_ms)) return 'timestamp_ms must be non-negative';
  return undefined;
}

function parseRecord(value: unknown): { record?: SimulationRecord; error?: string } {
  if (!isObject(value)) return { error: 'record must be a JSON object' };
  const baseError = validateBase(value);
  if (baseError) return { error: baseError };

  switch (value.record_type) {
    case 'metadata': {
      if (
        !isString(value.scenario) ||
        !isString(value.description) ||
        !isFiniteNumber(value.seed) ||
        !isNonNegativeNumber(value.tick_ms) ||
        !isNonNegativeNumber(value.duration_ms) ||
        !isStringArray(value.subsystems)
      ) {
        return { error: 'metadata fields do not match schema v1' };
      }
      return { record: value as unknown as MetadataRecord };
    }
    case 'telemetry': {
      if (
        !isNonNegativeNumber(value.sequence) ||
        !isString(value.subsystem) ||
        !isEnumValue(HEALTH_STATES, value.health) ||
        typeof value.valid !== 'boolean' ||
        !isString(value.reason) ||
        !isObject(value.data)
      ) {
        return { error: 'telemetry fields do not match schema v1' };
      }
      return { record: value as unknown as TelemetryRecord };
    }
    case 'event': {
      if (
        !isEnumValue(SEVERITIES, value.severity) ||
        !isString(value.event_code) ||
        !(value.subsystem === null || isString(value.subsystem)) ||
        !isString(value.message)
      ) {
        return { error: 'event fields do not match schema v1' };
      }
      return { record: value as unknown as EventRecord };
    }
    case 'state_transition': {
      if (
        !isEnumValue(SYSTEM_STATES, value.from) ||
        !isEnumValue(SYSTEM_STATES, value.to) ||
        !isString(value.reason) ||
        !isEnumValue(SEVERITIES, value.severity)
      ) {
        return { error: 'state transition fields do not match schema v1' };
      }
      return { record: value as unknown as StateTransitionRecord };
    }
    case 'summary': {
      const countFields = [
        value.ticks,
        value.telemetry_count,
        value.invalid_count,
        value.missing_heartbeat_count,
        value.dropped_message_count,
        value.state_transition_count,
      ];
      if (
        !isString(value.scenario) ||
        !isFiniteNumber(value.seed) ||
        !isEnumValue(SYSTEM_STATES, value.final_state) ||
        !countFields.every(isNonNegativeNumber)
      ) {
        return { error: 'summary fields do not match schema v1' };
      }
      return { record: value as unknown as SummaryRecord };
    }
    default:
      return { error: `unknown record_type "${String(value.record_type)}"` };
  }
}

export class DatasetError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'DatasetError';
  }
}

export function parseJsonl(text: string, source = 'inline'): SimulationDataset {
  const records: SimulationRecord[] = [];
  const warnings: string[] = [];

  text.split(/\r?\n/).forEach((line, index) => {
    if (!line.trim()) return;
    try {
      const result = parseRecord(JSON.parse(line) as unknown);
      if (result.record) records.push(result.record);
      if (result.error) warnings.push(`Line ${index + 1}: ${result.error}`);
    } catch {
      warnings.push(`Line ${index + 1}: invalid JSON`);
    }
  });

  const metadataRecords = records.filter(
    (record): record is MetadataRecord => record.record_type === 'metadata',
  );
  if (metadataRecords.length === 0) {
    throw new DatasetError(`${source} has no valid metadata record.`);
  }
  if (metadataRecords.length > 1) {
    warnings.push('The artifact has multiple metadata records; AeroGuard uses the first.');
  }

  const metadata = metadataRecords[0];
  const sortedRecords = records
    .map((record, sourceIndex) => ({ record, sourceIndex }))
    .sort((a, b) => a.record.timestamp_ms - b.record.timestamp_ms || a.sourceIndex - b.sourceIndex)
    .map(({ record }) => record);
  const summary = sortedRecords.find(
    (record): record is SummaryRecord => record.record_type === 'summary',
  );

  return { metadata, records: sortedRecords, summary, warnings, source };
}

export async function fetchDataset(file: string, signal?: AbortSignal): Promise<SimulationDataset> {
  const response = await fetch(file, { signal });
  if (!response.ok) {
    throw new DatasetError(`Request for ${file} failed (HTTP ${response.status}).`);
  }
  return parseJsonl(await response.text(), file);
}

export async function fetchManifest(signal?: AbortSignal): Promise<ScenarioManifest> {
  const response = await fetch('/data/manifest.json', { signal });
  if (!response.ok) throw new DatasetError(`Scenario manifest request failed (HTTP ${response.status}).`);
  return parseManifest(await response.json());
}

export function parseManifest(value: unknown): ScenarioManifest {
  if (
    !isObject(value) ||
    value.schema_version !== 1 ||
    !Array.isArray(value.scenarios) ||
    value.scenarios.length === 0 ||
    !value.scenarios.every(
      (scenario) =>
        isObject(scenario) &&
        isString(scenario.id) &&
        isString(scenario.label) &&
        isString(scenario.description) &&
        isString(scenario.file) &&
        isNonNegativeNumber(scenario.duration_ms) &&
        isNonNegativeNumber(scenario.tick_ms) &&
        isEnumValue(SYSTEM_STATES, scenario.final_state),
    )
  ) {
    throw new DatasetError('Scenario manifest does not match schema v1.');
  }
  return value as unknown as ScenarioManifest;
}

export function numericTelemetryValues(data: Record<string, unknown>): [string, number][] {
  return Object.entries(data).filter(
    (entry): entry is [string, number] => isFiniteNumber(entry[1]),
  );
}

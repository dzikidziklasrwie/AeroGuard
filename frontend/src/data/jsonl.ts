import {
  HEALTH_STATES,
  SEVERITIES,
  SYSTEM_STATES,
  type EventRecord,
  type MetadataRecord,
  type ScenarioManifest,
  type RunArchiveEntry,
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
  try {
    const response = await fetch('/api/scenarios', { signal });
    if (!response.ok) throw new DatasetError(`Scenario API request failed (HTTP ${response.status}).`);
    return { ...parseManifest(await response.json()), source: 'api' };
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    const response = await fetch('/data/manifest.json', { signal });
    if (!response.ok) throw new DatasetError(`Scenario manifest request failed (HTTP ${response.status}).`);
    return { ...parseManifest(await response.json()), source: 'static' };
  }
}

export async function fetchRunArchive(
  manifest: ScenarioManifest,
  signal?: AbortSignal,
): Promise<RunArchiveEntry[]> {
  try {
    const response = await fetch('/api/runs?limit=12', { signal });
    if (!response.ok) throw new DatasetError(`Run archive request failed (HTTP ${response.status}).`);
    const value = await response.json() as unknown;
    if (!Array.isArray(value)) throw new DatasetError('Run archive response must be an array.');
    return value.map(parseRunArchiveEntry);
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    return manifest.scenarios.map((scenario) => ({
      id: `static-${scenario.id}`,
      scenario: scenario.id,
      label: scenario.label,
      description: scenario.description,
      final_state: scenario.final_state,
      duration_ms: scenario.duration_ms,
      tick_ms: scenario.tick_ms,
      replay_url: dataFilePath(scenario.file),
      source: 'static' as const,
    }));
  }
}

function dataFilePath(file: string): string {
  if (/^(?:https?:)?\//.test(file)) return file;
  return `/data/${file.replace(/^\.\//, '')}`;
}

function parseRunArchiveEntry(value: unknown): RunArchiveEntry {
  if (
    !isObject(value) ||
    !isFiniteNumber(value.id) ||
    !isString(value.scenario) ||
    !isString(value.label) ||
    !isString(value.description) ||
    !isEnumValue(SYSTEM_STATES, value.final_state) ||
    !isNonNegativeNumber(value.duration_ms) ||
    !isNonNegativeNumber(value.tick_ms) ||
    !isString(value.replay_url)
  ) {
    throw new DatasetError('Run archive entry does not match schema v1.');
  }
  return {
    id: value.id,
    scenario: value.scenario,
    label: value.label,
    description: value.description,
    final_state: value.final_state,
    duration_ms: value.duration_ms,
    tick_ms: value.tick_ms,
    record_count: isNonNegativeNumber(value.record_count) ? value.record_count : undefined,
    source_sha256: isString(value.source_sha256) ? value.source_sha256 : undefined,
    imported_at_utc: isString(value.imported_at_utc) ? value.imported_at_utc : undefined,
    replay_url: value.replay_url,
    source: 'api',
  };
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

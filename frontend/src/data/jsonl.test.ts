import { describe, expect, it } from 'vitest';
import { DatasetError, numericTelemetryValues, parseJsonl } from './jsonl';
import { jsonlFixture } from '../test/fixtures';

describe('parseJsonl', () => {
  it('parses a normal-operation artifact and keeps the final summary', () => {
    const dataset = parseJsonl(jsonlFixture, '/data/normal-operation.jsonl');

    expect(dataset.metadata.scenario).toBe('normal-operation');
    expect(dataset.metadata.subsystems).toHaveLength(6);
    expect(dataset.summary).toMatchObject({
      final_state: 'NORMAL',
      telemetry_count: 60,
      seed: 42,
    });
    expect(dataset.warnings).toEqual([]);
    expect(dataset.records).toHaveLength(9);
  });

  it('sorts records by timestamp while preserving source order for ties', () => {
    const lines = jsonlFixture.split('\n');
    const shuffled = [lines.at(-1), ...lines.slice(0, -1)].join('\n');
    const dataset = parseJsonl(shuffled);

    expect(dataset.records[0].record_type).toBe('metadata');
    expect(dataset.records.at(-1)?.record_type).toBe('summary');
  });

  it('keeps usable records and reports malformed or unknown lines', () => {
    const content = [
      jsonlFixture,
      '{bad json',
      JSON.stringify({ schema_version: 1, record_type: 'future_record', timestamp_ms: 10 }),
      JSON.stringify({ schema_version: 2, record_type: 'event', timestamp_ms: 20 }),
    ].join('\n');
    const dataset = parseJsonl(content);

    expect(dataset.warnings).toHaveLength(3);
    expect(dataset.warnings[0]).toContain('invalid JSON');
    expect(dataset.warnings[1]).toContain('unknown record_type');
    expect(dataset.warnings[2]).toContain('schema_version');
    expect(dataset.summary?.ticks).toBe(10);
  });

  it('parses event records emitted by the health monitor', () => {
    const event = {
      schema_version: 1,
      record_type: 'event',
      timestamp_ms: 600,
      severity: 'ERROR',
      event_code: 'MISSING_HEARTBEAT',
      subsystem: 'communication',
      message: 'No telemetry arrived within the heartbeat timeout',
    };
    const dataset = parseJsonl(`${jsonlFixture}\n${JSON.stringify(event)}`);

    expect(dataset.records).toContainEqual(event);
  });

  it('rejects an artifact without a valid metadata record', () => {
    expect(() => parseJsonl('{"schema_version":1,"record_type":"event","timestamp_ms":0}'))
      .toThrow(DatasetError);
  });
});

describe('numericTelemetryValues', () => {
  it('returns finite numeric values only', () => {
    expect(numericTelemetryValues({ voltage_v: 28.2, online: true, mode: 'normal', invalid: Infinity }))
      .toEqual([['voltage_v', 28.2]]);
  });
});

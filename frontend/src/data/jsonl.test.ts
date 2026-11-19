import { describe, expect, it, vi } from 'vitest';
import { DatasetError, fetchManifest, fetchRunArchive, numericTelemetryValues, parseJsonl } from './jsonl';
import { jsonlFixture, manifestFixture } from '../test/fixtures';

function response(body: unknown, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: vi.fn().mockResolvedValue(body),
  } as unknown as Response;
}

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

describe('REST archive fallback', () => {
  it('loads scenarios from the REST API first', async () => {
    const fetchMock = vi.fn().mockResolvedValue(response(manifestFixture));
    vi.stubGlobal('fetch', fetchMock);

    const manifest = await fetchManifest();

    expect(manifest.source).toBe('api');
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith('/api/scenarios', expect.any(Object));
  });

  it('uses the bundled manifest when the API is offline', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response('', 503))
      .mockResolvedValueOnce(response(manifestFixture));
    vi.stubGlobal('fetch', fetchMock);

    const manifest = await fetchManifest();

    expect(manifest.source).toBe('static');
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual(['/api/scenarios', '/data/manifest.json']);
  });

  it('normalizes stored runs returned by SQLite API', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response([{
      id: 17,
      scenario: 'normal-operation',
      label: 'Normal operation',
      description: 'All simulated subsystems stay within limits.',
      final_state: 'NORMAL',
      duration_ms: 1000,
      tick_ms: 100,
      record_count: 74,
      replay_url: '/api/runs/17/replay',
    }])));

    const runs = await fetchRunArchive(manifestFixture as never);

    expect(runs).toEqual([expect.objectContaining({ id: 17, source: 'api', record_count: 74 })]);
  });

  it('maps bundled scenarios into run cards when the archive is offline', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response('', 503)));

    const runs = await fetchRunArchive(manifestFixture as never);

    expect(runs[0]).toMatchObject({
      id: 'static-normal-operation',
      replay_url: '/data/normal-operation.jsonl',
      source: 'static',
    });
  });
});

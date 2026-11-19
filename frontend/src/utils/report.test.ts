import { describe, expect, it } from 'vitest';
import { parseJsonl } from '../data/jsonl';
import { jsonlFixture } from '../test/fixtures';
import { buildSimulationReport } from './report';

describe('buildSimulationReport', () => {
  it('builds a Markdown report from recorded values', () => {
    const report = buildSimulationReport(parseJsonl(jsonlFixture, '/data/normal-operation.jsonl'));

    expect(report).toContain('# AeroGuard Simulation Report');
    expect(report).toContain('| Seed | 42 |');
    expect(report).toContain('| Telemetry records | 60 |');
    expect(report).toContain('INITIALIZING | NORMAL');
    expect(report).toContain('All subsystem heartbeats are valid');
    expect(report).toContain('/data/normal-operation.jsonl');
  });
});

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { parseJsonl, parseManifest } from './jsonl';

describe('C++ simulator artifacts', () => {
  it('loads all seven exported scenarios through the frontend parser', () => {
    const dataDirectory = join(process.cwd(), 'public', 'data');
    const manifest = parseManifest(
      JSON.parse(readFileSync(join(dataDirectory, 'manifest.json'), 'utf8')) as unknown,
    );

    expect(manifest.generated_by).toBe('AeroGuard');
    expect(manifest.scenarios).toHaveLength(7);
    expect(manifest.scenarios.every((scenario) => scenario.duration_ms >= 40_000)).toBe(true);
    expect(manifest.scenarios.every((scenario) => scenario.duration_ms <= 105_000)).toBe(true);
    expect(manifest.scenarios.find((scenario) => scenario.id === 'simulated-overheating')?.duration_ms)
      .toBe(105_000);

    for (const scenario of manifest.scenarios) {
      const dataset = parseJsonl(readFileSync(join(dataDirectory, scenario.file), 'utf8'), scenario.file);

      expect(dataset.warnings, scenario.file).toEqual([]);
      expect(dataset.metadata.scenario).toBe(scenario.id);
      expect(dataset.metadata.duration_ms).toBe(scenario.duration_ms);
      expect(dataset.metadata.tick_ms).toBe(scenario.tick_ms);
      expect(dataset.metadata.subsystems).toHaveLength(6);
      expect(dataset.summary?.final_state).toBe(scenario.final_state);
      expect(dataset.summary?.telemetry_count).toBeGreaterThan(0);
    }
  });
});

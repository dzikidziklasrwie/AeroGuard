export type MapScenarioEffect =
  | 'none'
  | 'communication'
  | 'sensor'
  | 'thermal'
  | 'fire'
  | 'airburst'
  | 'debris'
  | 'impact-land'
  | 'impact-water'
  | 'aftermath-land'
  | 'aftermath-water'
  | 'power'
  | 'multiple'
  | 'recovery';

const OVERHEATING_WARNING_MS = 10_000;
const OVERHEATING_FIRE_MS = 60_000;
const OVERHEATING_AIRBURST_MS = 80_000;
const OVERHEATING_DEBRIS_MS = 83_000;
const OVERHEATING_IMPACT_MS = 91_000;
const OVERHEATING_AFTERMATH_MS = 97_000;

const TIMED_EFFECTS = new Map<string, { startMs: number; endMs: number; effect: MapScenarioEffect }>([
  ['communication-interruption', { startMs: 10_000, endMs: 22_000, effect: 'communication' }],
  ['invalid-sensor-data', { startMs: 10_000, endMs: 20_000, effect: 'sensor' }],
  ['power-fluctuations', { startMs: 10_000, endMs: 26_000, effect: 'power' }],
  ['multiple-subsystem-failures', { startMs: 8_000, endMs: 44_000, effect: 'multiple' }],
  ['recovery-after-temporary-failure', { startMs: 10_000, endMs: 30_000, effect: 'recovery' }],
]);

export function firstMapEffectStartsAt(scenario: string | null): number | undefined {
  if (scenario === 'simulated-overheating') return OVERHEATING_WARNING_MS;
  return scenario ? TIMED_EFFECTS.get(scenario)?.startMs : undefined;
}

export function getMapScenarioEffect(
  scenario: string | null,
  elapsedMs: number,
  overLand: () => boolean = () => false,
): MapScenarioEffect {
  if (scenario !== 'simulated-overheating') {
    const timing = scenario ? TIMED_EFFECTS.get(scenario) : undefined;
    return timing && elapsedMs >= timing.startMs && elapsedMs < timing.endMs ? timing.effect : 'none';
  }
  if (!(elapsedMs >= OVERHEATING_WARNING_MS)) return 'none';
  if (elapsedMs < OVERHEATING_FIRE_MS) return 'thermal';
  if (elapsedMs < OVERHEATING_AIRBURST_MS) return 'fire';
  if (elapsedMs < OVERHEATING_DEBRIS_MS) return 'airburst';
  if (elapsedMs < OVERHEATING_IMPACT_MS) return 'debris';
  if (elapsedMs < OVERHEATING_AFTERMATH_MS) return overLand() ? 'impact-land' : 'impact-water';
  return overLand() ? 'aftermath-land' : 'aftermath-water';
}

export function getMapScenarioEffectLabel(effect: MapScenarioEffect): string {
  switch (effect) {
    case 'communication': return 'Communication lost';
    case 'sensor': return 'Sensor alert';
    case 'thermal': return 'Sustained overheating';
    case 'fire': return 'Aircraft fire';
    case 'airburst': return 'Airframe breakup';
    case 'debris': return 'Debris descending';
    case 'impact-land': return 'Land impact';
    case 'impact-water': return 'Water impact';
    case 'aftermath-land': return 'Land wreckage';
    case 'aftermath-water': return 'Floating debris';
    case 'power': return 'Electrical fluctuation';
    case 'multiple': return 'Multiple subsystem fault';
    case 'recovery': return 'Recovery in progress';
    default: return 'Normal visual state';
  }
}

export function showsAircraft(effect: MapScenarioEffect): boolean {
  return ![
    'airburst',
    'debris',
    'impact-land',
    'impact-water',
    'aftermath-land',
    'aftermath-water',
  ].includes(effect);
}

export function debrisProgress(elapsedMs: number): number {
  return Math.max(0, Math.min(
    1,
    (elapsedMs - OVERHEATING_DEBRIS_MS)
      / (OVERHEATING_IMPACT_MS - OVERHEATING_DEBRIS_MS),
  ));
}

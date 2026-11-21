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

export function getMapScenarioEffect(
  scenario: string | null,
  elapsedMs: number,
  overLand: () => boolean = () => false,
): MapScenarioEffect {
  switch (scenario) {
    case 'communication-interruption':
      return elapsedMs >= 10_000 && elapsedMs < 22_000 ? 'communication' : 'none';
    case 'invalid-sensor-data':
      return elapsedMs >= 10_000 && elapsedMs < 20_000 ? 'sensor' : 'none';
    case 'power-fluctuations':
      return elapsedMs >= 10_000 && elapsedMs < 26_000 ? 'power' : 'none';
    case 'multiple-subsystem-failures':
      return elapsedMs >= 8_000 && elapsedMs < 44_000 ? 'multiple' : 'none';
    case 'recovery-after-temporary-failure':
      return elapsedMs >= 10_000 && elapsedMs < 30_000 ? 'recovery' : 'none';
    case 'simulated-overheating':
      if (elapsedMs >= OVERHEATING_WARNING_MS && elapsedMs < OVERHEATING_FIRE_MS) return 'thermal';
      if (elapsedMs >= OVERHEATING_FIRE_MS && elapsedMs < OVERHEATING_AIRBURST_MS) return 'fire';
      if (elapsedMs >= OVERHEATING_AIRBURST_MS && elapsedMs < OVERHEATING_DEBRIS_MS) return 'airburst';
      if (elapsedMs >= OVERHEATING_DEBRIS_MS && elapsedMs < OVERHEATING_IMPACT_MS) return 'debris';
      if (elapsedMs >= OVERHEATING_IMPACT_MS && elapsedMs < OVERHEATING_AFTERMATH_MS) {
        return overLand() ? 'impact-land' : 'impact-water';
      }
      if (elapsedMs >= OVERHEATING_AFTERMATH_MS) {
        return overLand() ? 'aftermath-land' : 'aftermath-water';
      }
      return 'none';
    default:
      return 'none';
  }
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

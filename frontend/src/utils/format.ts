import type { HealthState, Severity, SystemState } from '../types';

export function formatDuration(milliseconds: number): string {
  const totalSeconds = Math.max(0, milliseconds) / 1000;
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = Math.floor(totalSeconds % 60);
  const tenths = Math.floor((totalSeconds % 1) * 10);
  return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}.${tenths}`;
}

const UNITS: [RegExp, string][] = [
  [/_c$/i, '°C'],
  [/_v$/i, 'V'],
  [/_a$/i, 'A'],
  [/_rpm$/i, 'rpm'],
  [/_percent$/i, '%'],
  [/_pct$/i, '%'],
  [/_ms$/i, 'ms'],
  [/_hz$/i, 'Hz'],
  [/_mbps$/i, 'Mb/s'],
  [/_lpm$/i, 'L/min'],
  [/_mm_s$/i, 'mm/s'],
  [/_watts?$/i, 'W'],
];

export function metricLabel(key: string): string {
  const unit = UNITS.find(([pattern]) => pattern.test(key));
  const withoutUnit = unit ? key.replace(unit[0], '') : key;
  const label = withoutUnit
    .replace(/_/g, ' ')
    .replace(/\b\w/g, (character) => character.toUpperCase());
  return unit ? `${label} · ${unit[1]}` : label;
}

export function formatValue(value: unknown): string {
  if (typeof value === 'number') {
    if (!Number.isFinite(value)) return 'N/A';
    if (Number.isInteger(value)) return value.toLocaleString('en-US');
    return value.toLocaleString('en-US', { maximumFractionDigits: 2 });
  }
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (typeof value === 'string') return value;
  return 'N/A';
}

export function titleCase(value: string): string {
  return value
    .toLowerCase()
    .replace(/_/g, ' ')
    .replace(/\b\w/g, (character) => character.toUpperCase());
}

export function statusTone(status: HealthState | SystemState | Severity): string {
  switch (status) {
    case 'HEALTHY':
    case 'NORMAL':
    case 'INFO':
      return 'nominal';
    case 'DEGRADED':
    case 'RECOVERY':
    case 'WARNING':
      return 'caution';
    case 'FAULT':
    case 'ERROR':
    case 'CRITICAL':
      return 'critical';
    case 'DEBUG':
    case 'INITIALIZING':
    case 'UNKNOWN':
      return 'neutral';
  }
}

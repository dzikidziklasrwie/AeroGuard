import { useMemo, useState } from 'react';
import { numericTelemetryValues } from '../data/jsonl';
import { SUBSYSTEM_LABELS, type TelemetryRecord } from '../types';
import { formatValue, metricLabel } from '../utils/format';

interface TelemetryChartProps {
  records: TelemetryRecord[];
  subsystem: string;
  durationMs: number;
}

const WIDTH = 760;
const HEIGHT = 250;
const PADDING = { top: 28, right: 26, bottom: 34, left: 58 };

export function TelemetryChart({ records, subsystem, durationMs }: TelemetryChartProps) {
  const availableMetrics = useMemo(() => {
    const keys = new Set<string>();
    records.forEach((record) => numericTelemetryValues(record.data).forEach(([key]) => keys.add(key)));
    return [...keys];
  }, [records]);
  const [requestedMetric, setRequestedMetric] = useState('');
  const metric = availableMetrics.includes(requestedMetric)
    ? requestedMetric
    : availableMetrics[0] ?? '';

  const points = useMemo(
    () =>
      records
        .map((record) => ({ x: record.timestamp_ms, y: record.data[metric] }))
        .filter((point): point is { x: number; y: number } => typeof point.y === 'number' && Number.isFinite(point.y)),
    [metric, records],
  );

  const bounds = useMemo(() => {
    if (!points.length) return { min: 0, max: 1 };
    let min = Math.min(...points.map((point) => point.y));
    let max = Math.max(...points.map((point) => point.y));
    const spread = max - min || Math.max(Math.abs(max) * 0.1, 1);
    min -= spread * 0.16;
    max += spread * 0.16;
    return { min, max };
  }, [points]);

  const plotWidth = WIDTH - PADDING.left - PADDING.right;
  const plotHeight = HEIGHT - PADDING.top - PADDING.bottom;
  const xScale = (value: number) => PADDING.left + (value / Math.max(durationMs, 1)) * plotWidth;
  const yScale = (value: number) =>
    PADDING.top + (1 - (value - bounds.min) / Math.max(bounds.max - bounds.min, 1)) * plotHeight;
  const line = points.map((point, index) => `${index === 0 ? 'M' : 'L'} ${xScale(point.x)} ${yScale(point.y)}`).join(' ');
  const area = points.length
    ? `${line} L ${xScale(points.at(-1)?.x ?? 0)} ${PADDING.top + plotHeight} L ${xScale(points[0].x)} ${PADDING.top + plotHeight} Z`
    : '';

  return (
    <section className="panel chart-panel" aria-labelledby="telemetry-chart-title">
      <div className="panel-heading chart-heading">
        <div>
          <p className="section-kicker">Recorded signal</p>
          <h2 id="telemetry-chart-title">Telemetry trend</h2>
        </div>
        <label className="field compact-field">
          <span>Metric</span>
          <span className="select-wrap">
            <select
              value={metric}
              onChange={(event) => setRequestedMetric(event.target.value)}
              disabled={!availableMetrics.length}
              aria-label="Chart metric"
            >
              {!availableMetrics.length && <option value="">No numeric metrics</option>}
              {availableMetrics.map((key) => <option value={key} key={key}>{metricLabel(key)}</option>)}
            </select>
          </span>
        </label>
      </div>

      {points.length < 2 ? (
        <div className="chart-empty" role="status">
          <span className="chart-empty-mark" aria-hidden="true" />
          <p>Waiting for at least two {SUBSYSTEM_LABELS[subsystem] ?? subsystem} samples.</p>
        </div>
      ) : (
        <div className="chart-canvas">
          <svg viewBox={`0 0 ${WIDTH} ${HEIGHT}`} role="img" aria-label={`${metricLabel(metric)} time series`}>
            <defs>
              <linearGradient id="chartArea" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" stopColor="var(--accent)" stopOpacity=".28" />
                <stop offset="1" stopColor="var(--accent)" stopOpacity="0" />
              </linearGradient>
            </defs>
            {[0, 0.25, 0.5, 0.75, 1].map((ratio) => {
              const y = PADDING.top + ratio * plotHeight;
              const value = bounds.max - ratio * (bounds.max - bounds.min);
              return <g key={ratio}><line className="chart-grid" x1={PADDING.left} x2={WIDTH - PADDING.right} y1={y} y2={y} /><text className="chart-label" x={PADDING.left - 10} y={y + 4} textAnchor="end">{formatValue(value)}</text></g>;
            })}
            {[0, 0.25, 0.5, 0.75, 1].map((ratio) => {
              const x = PADDING.left + ratio * plotWidth;
              return <text className="chart-label" key={ratio} x={x} y={HEIGHT - 10} textAnchor={ratio === 0 ? 'start' : ratio === 1 ? 'end' : 'middle'}>{Math.round((durationMs / 1000) * ratio)}s</text>;
            })}
            <path d={area} fill="url(#chartArea)" />
            <path className="chart-line" d={line} />
            {points.map((point, index) => index === points.length - 1 ? <circle className="chart-point" key={`${point.x}-${index}`} cx={xScale(point.x)} cy={yScale(point.y)} r="4" /> : null)}
          </svg>
          <div className="chart-current" aria-label="Latest chart value">
            <span>Latest</span>
            <strong>{formatValue(points.at(-1)?.y)}</strong>
          </div>
        </div>
      )}
    </section>
  );
}

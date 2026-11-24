import { memo, type KeyboardEvent } from 'react';
import type { FlightRecord } from '../api/types';
import type { MapViewport } from './mapViewport';
import {
  debrisProgress,
  getMapScenarioEffectLabel,
  showsAircraft,
  type MapScenarioEffect,
} from './mapScenarioEffects';

const MARKER_LABEL_WIDTH = 76;
const MARKER_LABEL_GAP = 12;

const DEBRIS_FRAGMENTS = [
  { x: -4, y: -24, dx: -19, dy: 38, rotation: -150, shape: '-5,-2 5,-1 3,3 -4,2' },
  { x: 5, y: -22, dx: 16, dy: 35, rotation: 185, shape: '-4,-3 5,0 2,4 -3,2' },
  { x: -1, y: -18, dx: -8, dy: 42, rotation: 240, shape: '-3,-4 4,-2 3,3 -2,4' },
  { x: 2, y: -20, dx: 9, dy: 40, rotation: -210, shape: '-5,0 2,-3 5,2 -2,3' },
] as const;

interface OperationsMapAircraftMarkerProps {
  flight: FlightRecord;
  point: readonly [number, number];
  effect: MapScenarioEffect;
  active: boolean;
  viewport: MapViewport;
  onSelect: (flightId: number) => void;
}

function formatCoordinate(value: number, positive: string, negative: string): string {
  return `${Math.abs(value).toFixed(2)}° ${value >= 0 ? positive : negative}`;
}

function markerLabelTransform([x, y]: readonly [number, number], viewport: MapViewport): string {
  const labelX = x > viewport.x + viewport.width / 2
    ? -(MARKER_LABEL_WIDTH + MARKER_LABEL_GAP)
    : MARKER_LABEL_GAP;
  const labelY = y < viewport.y + 48 / viewport.zoom ? MARKER_LABEL_GAP : -14;
  return `translate(${labelX} ${labelY})`;
}

export const OperationsMapAircraftMarker = memo(function OperationsMapAircraftMarker({
  flight,
  point,
  effect,
  active,
  viewport,
  onSelect,
}: OperationsMapAircraftMarkerProps) {
  const fallProgress = debrisProgress(flight.scenario_elapsed_ms);

  function selectFromKeyboard(event: KeyboardEvent<SVGGElement>) {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    event.preventDefault();
    onSelect(flight.id);
  }

  return <g
    className={`aircraft-marker marker-${flight.condition.toLowerCase()}${active ? ' active' : ''} scenario-effect-${effect}`}
    transform={`translate(${point[0]} ${point[1]}) scale(${1 / viewport.zoom})`}
    role="button"
    tabIndex={0}
    aria-label={`${flight.tail_number}, simulated position ${formatCoordinate(flight.latitude, 'N', 'S')}, ${formatCoordinate(flight.longitude, 'E', 'W')}, ${getMapScenarioEffectLabel(effect)}, scenario ${flight.scenario_label ?? flight.scenario ?? 'session demo'}`}
    onClick={(event) => {
      event.stopPropagation();
      onSelect(flight.id);
    }}
    onKeyDown={selectFromKeyboard}
  >
    {active && <circle className="marker-selection-ring" cy="9" r="23" />}
    <ellipse className="marker-ground-shadow" cx="0" cy="9" rx={active ? 11 : 8} ry={active ? 4.5 : 3.5} />
    <circle className="marker-pulse" cy="9" r={active ? 18 : 13} />
    <circle className="marker-core" cy="9" r={active ? 5.5 : 4.5} />
    {showsAircraft(effect) && <line className="marker-altitude-link" x1="0" y1="4" x2="0" y2="-7" />}
    <g className="marker-flight-body" transform="translate(0 -13)">
      {showsAircraft(effect) && <>
        <g className="marker-contrail" transform={`rotate(${flight.heading_deg})`}>
          <circle cx="-12" cy="0" r="2.4" />
          <circle cx="-18" cy="0" r="1.7" />
          <circle cx="-24" cy="0" r="1.1" />
        </g>
        <g className="marker-plane-heading" transform={`rotate(${flight.heading_deg})`}>
          <path className="marker-aircraft" d="M0-13 L3.2-4.5 L10-1.5 L10 1.5 L3.5 0.8 L2.2 7.5 L6 9.8 L6 11.5 L0 10 L-6 11.5 L-6 9.8 L-2.2 7.5 L-3.5 0.8 L-10 1.5 L-10-1.5 L-3.2-4.5 Z" />
        </g>
      </>}
      {effect === 'communication' && <g className="scenario-effect scenario-communication" aria-hidden="true">
        <circle className="radio-ring radio-ring-outer" cx="0" cy="0" r="42" />
        <circle className="radio-ring radio-ring-middle" cx="0" cy="0" r="30" />
        <circle className="radio-ring radio-ring-inner" cx="0" cy="0" r="19" />
        <path className="radio-signal" d="M-13-18 A22 22 0 0 1 13-18 M-8-12 A13 13 0 0 1 8-12" />
        <path className="radio-cut" d="M-31-31 L31 31" />
      </g>}
      {effect === 'sensor' && <g className="scenario-effect scenario-sensor" aria-hidden="true">
        <path d="M0-17 L-5-7 L2-5 L-3 8" />
        <circle cx="-12" cy="-10" r="2" /><circle cx="12" cy="8" r="2" />
      </g>}
      {effect === 'thermal' && <g className="scenario-effect scenario-thermal" aria-hidden="true">
        <circle className="thermal-halo thermal-halo-outer" cx="0" cy="0" r="32" />
        <circle className="thermal-core" cx="0" cy="0" r="20" />
        <path className="heat-wave" d="M-23 20 C-31 10 -17 3 -23-10" /><path className="heat-wave" d="M-8 25 C-16 13 1 4 -8-12" /><path className="heat-wave" d="M8 25 C0 13 17 4 8-12" /><path className="heat-wave" d="M23 20 C15 10 30 3 23-10" />
      </g>}
      {effect === 'fire' && <g className="scenario-effect scenario-fire" aria-hidden="true">
        <circle className="fire-glow" cx="0" cy="0" r="31" />
        <path className="flame flame-large" d="M0 20 C-16 10 -17-5 -6-20 C-5-8 1-9 3-27 C17-10 16 8 0 20Z" />
        <path className="flame flame-small flame-left" d="M-12 17 C-25 7 -22-8 -14-16 C-13-7 -7-5 -5-19 C2-5 -2 10 -12 17Z" />
        <path className="flame flame-small flame-right" d="M12 17 C25 7 22-8 14-16 C13-7 7-5 5-19 C-2-5 2 10 12 17Z" />
        <circle className="smoke smoke-1" cx="-9" cy="-23" r="8" /><circle className="smoke smoke-2" cx="7" cy="-27" r="7" /><circle className="smoke smoke-3" cx="0" cy="-35" r="9" />
        <circle className="ember ember-1" cx="-18" cy="2" r="2" /><circle className="ember ember-2" cx="18" cy="-4" r="1.8" /><circle className="ember ember-3" cx="8" cy="16" r="1.5" />
      </g>}
      {effect === 'power' && <g className="scenario-effect scenario-power" aria-hidden="true">
        <circle className="power-halo" cx="0" cy="0" r="34" />
        <path className="power-arc arc-1" d="M-35-17 L-22-12 L-29-2 L-15 1 L-22 13 L-8 17" />
        <path className="power-arc arc-2" d="M34-20 L21-13 L29-5 L15 1 L23 11 L8 18" />
        <path className="power-arc arc-3" d="M-25 27 L-13 19 L-8 28 L0 17 L8 28 L15 18 L27 25" />
        <circle className="power-node node-1" cx="-31" cy="-18" r="3" /><circle className="power-node node-2" cx="31" cy="-20" r="3" /><circle className="power-node node-3" cx="0" cy="24" r="3" />
      </g>}
      {effect === 'multiple' && <g className="scenario-effect scenario-multiple" aria-hidden="true">
        <circle cx="-9" cy="-14" r="5" /><circle cx="8" cy="-12" r="7" />
        <path d="M-20 8 L-12 3 M20 8 L12 3 M0 16 L0 8" />
        <circle className="smoke" cx="-14" cy="-24" r="4" /><circle className="smoke" cx="12" cy="-25" r="5" />
      </g>}
      {effect === 'recovery' && <g className="scenario-effect scenario-recovery" aria-hidden="true">
        <circle className="recovery-wash" cx="0" cy="0" r="30" />
        <circle className="recovery-ring recovery-ring-outer" cx="0" cy="0" r="38" />
        <circle className="recovery-ring recovery-ring-inner" cx="0" cy="0" r="25" />
        <path className="recovery-orbit" d="M-27 0 A27 27 0 1 1 7 26" />
        <path className="recovery-check" d="M-12 0 L-3 10 L15-13" />
        <circle className="recovery-particle particle-1" cx="-29" cy="-19" r="2.5" /><circle className="recovery-particle particle-2" cx="31" cy="-10" r="2" /><circle className="recovery-particle particle-3" cx="18" cy="29" r="2.3" />
      </g>}
      {effect === 'airburst' && <g className="scenario-effect scenario-airburst" aria-hidden="true">
        <circle className="airburst-wave wave-outer" cx="0" cy="0" r="18" />
        <circle className="airburst-wave wave-inner" cx="0" cy="0" r="11" />
        <circle className="airburst-core" cx="0" cy="0" r="10" />
        <path d="M0-9 L0-38 M8-7 L30-30 M11 2 L39 10 M7 8 L27 31 M-8-7 L-30-30 M-11 2 L-39 10 M-7 8 L-27 31 M0 9 L0 37" />
      </g>}
    </g>
    {effect === 'debris' && <g className="scenario-effect scenario-debris" aria-hidden="true">
      {DEBRIS_FRAGMENTS.map((fragment, index) => <polygon
        key={fragment.shape}
        points={fragment.shape}
        transform={`translate(${fragment.x + fragment.dx * fallProgress} ${fragment.y + fragment.dy * fallProgress}) rotate(${fragment.rotation * fallProgress})`}
        className={`debris-fragment fragment-${index + 1}`}
      />)}
      <path className="debris-smoke" d={`M0 -20 Q${-8 + fallProgress * 5} ${-8 + fallProgress * 12} ${-4 + fallProgress * 8} ${fallProgress * 18}`} />
    </g>}
    {effect === 'impact-land' && <g className="scenario-effect scenario-impact-land" transform="translate(0 9)" aria-hidden="true">
      <circle className="impact-ring" cx="0" cy="0" r="18" />
      <circle className="impact-core" cx="0" cy="0" r="9" />
      <path className="debris" d="M-3-4 L-27-26 M4-3 L27-23 M-2 5 L-20 29 M3 5 L23 27" />
    </g>}
    {effect === 'impact-water' && <g className="scenario-effect scenario-impact-water" transform="translate(0 9)" aria-hidden="true">
      <circle className="splash-ring" cx="0" cy="0" r="19" />
      <path className="water-spray" d="M0 4 C-13-9 -8-28 0-37 C8-28 13-9 0 4Z" />
      <path className="debris" d="M-3 0 L-24-25 M3 0 L22-23" />
    </g>}
    {effect === 'aftermath-land' && <g className="scenario-effect scenario-aftermath-land" transform="translate(0 9)" aria-hidden="true">
      <ellipse cx="0" cy="2" rx="13" ry="5" /><path d="M-9-1 L-2-7 L3 0 L10-5 M-5 4 L6 3" />
    </g>}
    {effect === 'aftermath-water' && <g className="scenario-effect scenario-aftermath-water" transform="translate(0 9)" aria-hidden="true">
      <ellipse cx="0" cy="1" rx="14" ry="5" /><path d="M-9-1 L-3-5 L2 1 L9-3" />
    </g>}
    <g className="marker-label" transform={markerLabelTransform(point, viewport)}><rect width={MARKER_LABEL_WIDTH} height="25" rx="4" /><text x="9" y="17">{flight.tail_number}</text></g>
  </g>;
});

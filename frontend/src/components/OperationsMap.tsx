import { geoCentroid, geoContains, geoGraticule10, geoNaturalEarth1, geoPath } from 'd3-geo';
import type { Feature, FeatureCollection, GeoJsonProperties, Geometry } from 'geojson';
import {
  type FormEvent,
  type KeyboardEvent,
  type MouseEvent,
  type PointerEvent,
  type WheelEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { feature } from 'topojson-client';
import type { GeometryCollection, Topology } from 'topojson-specification';
import worldData from 'world-atlas/countries-110m.json';
import {
  createOperationFlight,
  deleteOperationFlight,
  getOperationFlights,
  getOperationMessages,
  getOperationPermissions,
  sendOperationMessage,
  updateOperationFlight,
  updateOperationFlightScenario,
  ApiError,
} from '../api/client';
import { connectOperationsHub } from '../api/operationsHub';
import {
  CHAT_SHARE_TYPES,
  FLIGHT_SCOPE_TYPES,
  OPERATION_PERMISSION_LABELS,
  type AircraftRecord,
  type ChatMessageRecord,
  type ChatShareType,
  type FlightInput,
  type FlightRecord,
  type FlightScopeType,
  type OperationPermission,
  type UserAccount,
} from '../api/types';
import type { RunArchiveEntry } from '../types';
import { titleCase } from '../utils/format';
import {
  centerViewport,
  clampViewport,
  clientToMapPoint,
  INITIAL_MAP_VIEWPORT,
  MAP_HEIGHT,
  MAP_WIDTH,
  MAP_ZOOM_STEP,
  MAX_MAP_ZOOM,
  MIN_MAP_ZOOM,
  type MapViewport,
  zoomViewport,
} from './mapViewport';
import {
  debrisProgress,
  getMapScenarioEffect,
  getMapScenarioEffectLabel,
  showsAircraft,
} from './mapScenarioEffects';

const topology = worldData as unknown as Topology<{ countries: GeometryCollection }>;
const countries = feature(topology, topology.objects.countries) as FeatureCollection<Geometry, GeoJsonProperties>;
const projection = geoNaturalEarth1().fitExtent([[18, 18], [MAP_WIDTH - 18, MAP_HEIGHT - 18]], { type: 'Sphere' });
const path = geoPath(projection);
const spherePath = path({ type: 'Sphere' }) ?? '';
const countriesPath = path(countries) ?? '';
const graticulePath = path(geoGraticule10()) ?? '';
const MARKER_LABEL_WIDTH = 76;
const MARKER_LABEL_GAP = 12;
const DISPLAY_LATITUDE_LIMIT = 82;
const DISPLAY_LONGITUDE_LIMIT = 176;

const DEMO_COORDINATES = [
  { latitude: 52.23, longitude: 21.01 },
  { latitude: 40.71, longitude: -74.01 },
  { latitude: -33.87, longitude: 151.21 },
  { latitude: 35.68, longitude: 139.65 },
  { latitude: -1.29, longitude: 36.82 },
  { latitude: -23.55, longitude: -46.63 },
];
const CONTINENT_OPTIONS = [
  ['AF', 'Africa', 1.0, 20.0],
  ['AN', 'Antarctica', -80.0, 0.0],
  ['AS', 'Asia', 40.0, 85.0],
  ['EU', 'Europe', 52.0, 15.0],
  ['NA', 'North America', 40.0, -100.0],
  ['OC', 'Oceania', -25.0, 134.0],
  ['SA', 'South America', -17.0, -60.0],
] as const;
const COUNTRY_OPTIONS = [
  ['ALB', 'Albania'], ['ARG', 'Argentina'], ['AUS', 'Australia'], ['AUT', 'Austria'],
  ['BEL', 'Belgium'], ['BIH', 'Bosnia and Herzegovina'], ['BLR', 'Belarus'], ['BRA', 'Brazil'],
  ['BGR', 'Bulgaria'], ['CAN', 'Canada'], ['CHE', 'Switzerland'], ['CHL', 'Chile'],
  ['CHN', 'China'], ['COL', 'Colombia'], ['CZE', 'Czechia'], ['DEU', 'Germany'],
  ['DNK', 'Denmark'], ['DZA', 'Algeria'], ['EGY', 'Egypt'], ['ESP', 'Spain'],
  ['EST', 'Estonia'], ['FIN', 'Finland'], ['FRA', 'France'], ['GBR', 'United Kingdom'],
  ['GRC', 'Greece'], ['HRV', 'Croatia'], ['HUN', 'Hungary'], ['IDN', 'Indonesia'],
  ['IND', 'India'], ['IRL', 'Ireland'], ['ISL', 'Iceland'], ['ITA', 'Italy'],
  ['JPN', 'Japan'], ['KOR', 'South Korea'], ['LTU', 'Lithuania'], ['LVA', 'Latvia'],
  ['MAR', 'Morocco'], ['MDA', 'Moldova'], ['MKD', 'North Macedonia'], ['MEX', 'Mexico'],
  ['MNE', 'Montenegro'], ['NLD', 'Netherlands'], ['NOR', 'Norway'], ['NZL', 'New Zealand'],
  ['PER', 'Peru'], ['POL', 'Poland'], ['PRT', 'Portugal'], ['ROU', 'Romania'],
  ['SAU', 'Saudi Arabia'], ['SRB', 'Serbia'], ['SVK', 'Slovakia'], ['SVN', 'Slovenia'],
  ['SWE', 'Sweden'], ['TUR', 'Turkey'], ['UKR', 'Ukraine'], ['USA', 'United States'],
  ['ZAF', 'South Africa'], ['NGA', 'Nigeria'],
] as const;
const COUNTRY_NAME_ALIASES: Record<string, string[]> = {
  ALB: ['Albania'], ARG: ['Argentina'], AUS: ['Australia'], AUT: ['Austria'],
  BEL: ['Belgium'], BIH: ['Bosnia and Herzegovina', 'Bosnia and Herz.'], BLR: ['Belarus'],
  BRA: ['Brazil'], BGR: ['Bulgaria'], CAN: ['Canada'], CHE: ['Switzerland'],
  CHL: ['Chile'], CHN: ['China'], COL: ['Colombia'], CZE: ['Czechia'],
  DEU: ['Germany'], DNK: ['Denmark'], DZA: ['Algeria'], EGY: ['Egypt'],
  ESP: ['Spain'], EST: ['Estonia'], FIN: ['Finland'], FRA: ['France'],
  GBR: ['United Kingdom'], GRC: ['Greece'], HRV: ['Croatia'], HUN: ['Hungary'],
  IDN: ['Indonesia'], IND: ['India'], IRL: ['Ireland'], ISL: ['Iceland'],
  ITA: ['Italy'], JPN: ['Japan'], KOR: ['South Korea'], LTU: ['Lithuania'],
  LVA: ['Latvia'], MAR: ['Morocco'], MDA: ['Moldova'], MKD: ['North Macedonia', 'Macedonia'],
  MEX: ['Mexico'], MNE: ['Montenegro'], NLD: ['Netherlands'], NOR: ['Norway'],
  NZL: ['New Zealand'], PER: ['Peru'], POL: ['Poland'], PRT: ['Portugal'],
  ROU: ['Romania'], SAU: ['Saudi Arabia'], SRB: ['Serbia'], SVK: ['Slovakia'],
  SVN: ['Slovenia'], SWE: ['Sweden'], TUR: ['Turkey'], UKR: ['Ukraine'],
  USA: ['United States', 'United States of America'], ZAF: ['South Africa'], NGA: ['Nigeria'],
};

type DataMode = 'loading' | 'api' | 'demo' | 'error';
type StreamState = 'connecting' | 'connected' | 'snapshot';

interface MapPan {
  pointerId: number;
  clientX: number;
  clientY: number;
  viewport: MapViewport;
  moved: boolean;
}

interface PlacementDraft {
  kind: 'create' | 'move';
  flightId?: number;
  aircraftId: string;
  runId: string;
  scopeType: FlightScopeType;
  scopeCode: string;
  speedKph: string;
  coordinates: string;
  latitude: string;
  longitude: string;
}

interface HoveredCountry {
  name: string;
  numericCode: string;
  latitude: number;
  longitude: number;
}

const DEBRIS_FRAGMENTS = [
  { x: -4, y: -24, dx: -19, dy: 38, rotation: -150, shape: '-5,-2 5,-1 3,3 -4,2' },
  { x: 5, y: -22, dx: 16, dy: 35, rotation: 185, shape: '-4,-3 5,0 2,4 -3,2' },
  { x: -1, y: -18, dx: -8, dy: 42, rotation: 240, shape: '-3,-4 4,-2 3,3 -2,4' },
  { x: 2, y: -20, dx: 9, dy: 40, rotation: -210, shape: '-5,0 2,-3 5,2 -2,3' },
] as const;

interface OperationsMapProps {
  token: string;
  currentUser: UserAccount;
  aircraft: AircraftRecord[];
  runs?: RunArchiveEntry[];
  onUnauthorized: () => void;
}

function hasRole(user: UserAccount, role: string): boolean {
  return user.roles.some((candidate) => candidate.toLowerCase() === role.toLowerCase());
}

function isApiUnavailable(error: unknown): boolean {
  return error instanceof TypeError
    || (error instanceof ApiError && (error.status === 404 || error.status >= 500));
}

function formatCoordinate(value: number, positive: string, negative: string): string {
  return `${Math.abs(value).toFixed(2)}° ${value >= 0 ? positive : negative}`;
}

function formatInputCoordinate(value: number): string {
  return (Math.round(value * 10_000) / 10_000).toFixed(4);
}

function isOverLand(latitude: number, longitude: number): boolean {
  return countries.features.some((country) =>
    geoContains(country, [longitude, latitude]));
}

function getFlightScenarioEffect(flight: FlightRecord) {
  return getMapScenarioEffect(
    flight.scenario,
    flight.scenario_elapsed_ms,
    () => isOverLand(flight.latitude, flight.longitude),
  );
}

function formatCoordinatePair(latitude: number, longitude: number): string {
  return `${formatInputCoordinate(latitude)} ${formatInputCoordinate(longitude)}`;
}

function parseCoordinatePair(value: string): [number, number] | null {
  const parts = value.trim().split(/[\s,;]+/).filter(Boolean);
  if (parts.length !== 2) return null;
  const latitude = Number(parts[0]);
  const longitude = Number(parts[1]);
  if (!isValidCoordinate(latitude, longitude)) return null;
  return [latitude, longitude];
}

function formatScenarioTime(elapsedMs: number, durationMs: number): string {
  const elapsed = Math.max(0, Math.round(elapsedMs / 1000));
  const duration = Math.max(0, Math.round(durationMs / 1000));
  return `${elapsed}s / ${duration}s`;
}

function permissionLabel(permission: OperationPermission): string {
  return OPERATION_PERMISSION_LABELS[permission];
}

function createDemoFlights(aircraft: AircraftRecord[], user: UserAccount): FlightRecord[] {
  const now = new Date().toISOString();
  const isAdmin = hasRole(user, 'Admin');
  return aircraft.slice(0, DEMO_COORDINATES.length).map((item, index) => {
    const point = DEMO_COORDINATES[index];
    const condition = item.status === 'ACTIVE' ? 'HEALTHY' : item.status === 'MAINTENANCE' ? 'DEGRADED' : 'CRITICAL';
    const currentUserOwnsAircraft = item.current_user_access === 'OWNER';
    return {
      id: -(index + 1),
      aircraft_id: item.id,
      tail_number: item.tail_number,
      display_name: item.display_name,
      owner_user_id: currentUserOwnsAircraft ? user.id : item.created_by,
      owner_username: currentUserOwnsAircraft ? user.username : `registry-owner-${item.created_by}`,
      owner_display_name: currentUserOwnsAircraft ? user.display_name : `Registry owner #${item.created_by}`,
      current_user_access: item.current_user_access,
      scope_type: 'WORLD',
      scope_code: 'WORLD',
      latitude: point.latitude,
      longitude: point.longitude,
      heading_deg: (35 + index * 61) % 360,
      speed_kph: item.status === 'GROUNDED' ? 0 : 690 + index * 17,
      route_phase: 'PATROL',
      route_progress: 1,
      run_id: 0,
      scenario: 'session-demo',
      scenario_label: 'Session demo',
      scenario_elapsed_ms: 18_000 + index * 4_000,
      scenario_duration_ms: 90_000,
      condition,
      telemetry: [
        { subsystem: 'Navigation', health: condition, valid: true, reason: null, data: { heading_deg: (35 + index * 61) % 360 } },
        { subsystem: 'Power', health: condition === 'CRITICAL' ? 'DEGRADED' : 'HEALTHY', valid: true, reason: null, data: { bus_voltage_v: 27.8 } },
      ],
      version: 1,
      started_at_utc: now,
      updated_at_utc: now,
      can_manage: isAdmin || currentUserOwnsAircraft,
      can_remove: isAdmin || item.current_user_access === 'OWNER',
    };
  });
}

function createDemoMessages(flight: FlightRecord, user: UserAccount): ChatMessageRecord[] {
  return [{
    id: -Math.abs(flight.id * 10 + 1),
    flight_id: flight.id,
    sender_user_id: user.id,
    sender_username: user.username,
    sender_display_name: user.display_name,
    message: `${flight.tail_number} joined the simulated operations room.`,
    share_type: 'TEXT',
    payload: null,
    sent_at_utc: flight.started_at_utc,
  }];
}

function flightInputFromDraft(draft: PlacementDraft): FlightInput | null {
  const aircraftId = Number(draft.aircraftId);
  const runId = draft.runId.trim() ? Number(draft.runId) : null;
  const latitude = Number(draft.latitude);
  const longitude = Number(draft.longitude);
  const speedKph = Number(draft.speedKph);
  if (!aircraftId || !Number.isFinite(latitude) || !Number.isFinite(longitude) || !Number.isFinite(speedKph)) return null;
  if (latitude < -85 || latitude > 85 || longitude < -180 || longitude > 180 || speedKph < 400 || speedKph > 1100) return null;
  if (runId === null || !Number.isInteger(runId) || runId <= 0) return null;
  if (draft.scopeType === 'CONTINENT' && !CONTINENT_OPTIONS.some(([code]) => code === draft.scopeCode.trim())) return null;
  if (draft.scopeType === 'COUNTRY'
    && !COUNTRY_OPTIONS.some(([code]) => code === draft.scopeCode.trim())) return null;
  return {
    aircraft_id: aircraftId,
    run_id: runId,
    scope_type: draft.scopeType,
    scope_code: draft.scopeType === 'WORLD' ? 'WORLD' : draft.scopeCode.trim(),
    latitude,
    longitude,
    speed_kph: speedKph,
  };
}

function isValidCoordinate(latitude: number, longitude: number): boolean {
  if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return false;
  return latitude >= -85 && latitude <= 85 && longitude >= -180 && longitude <= 180;
}

function projectMarkerCoordinate(latitude: number, longitude: number): [number, number] | null {
  if (!isValidCoordinate(latitude, longitude)) return null;
  const point = projection([
    Math.max(-DISPLAY_LONGITUDE_LIMIT, Math.min(DISPLAY_LONGITUDE_LIMIT, longitude)),
    Math.max(-DISPLAY_LATITUDE_LIMIT, Math.min(DISPLAY_LATITUDE_LIMIT, latitude)),
  ]);
  return point && point.every(Number.isFinite) ? point : null;
}

function hasValidFlightPosition(flight: FlightRecord): boolean {
  return isValidCoordinate(flight.latitude, flight.longitude)
    && Number.isFinite(flight.heading_deg)
    && flight.heading_deg >= 0
    && flight.heading_deg < 360;
}

function countryFeatureName(feature: Feature<Geometry, GeoJsonProperties>): string {
  return typeof feature.properties?.name === 'string' ? feature.properties.name : '';
}

function findCountryFeature(code: string): Feature<Geometry, GeoJsonProperties> | undefined {
  const aliases = COUNTRY_NAME_ALIASES[code] ?? [];
  return countries.features.find((feature) =>
    aliases.some((alias) => countryFeatureName(feature).toLowerCase() === alias.toLowerCase()));
}

function countryDetails(feature: Feature<Geometry, GeoJsonProperties>): Omit<HoveredCountry, 'name'> & { name: string } {
  const center = geoCentroid(feature);
  const numericCode = String(feature.id ?? '').replace(/^0+/, '') || '—';
  const coordinate = projection.invert?.(path.centroid(feature));
  return {
    name: countryFeatureName(feature),
    numericCode,
    latitude: coordinate?.[1] ?? center[1],
    longitude: coordinate?.[0] ?? center[0],
  };
}

function reconcileFlightUpdate(current: FlightRecord[], incoming: FlightRecord): FlightRecord[] {
  const index = current.findIndex((candidate) => candidate.id === incoming.id);
  if (index < 0) return hasValidFlightPosition(incoming) ? [...current, incoming] : current;
  const previous = current[index];
  const next = hasValidFlightPosition(incoming)
    ? incoming
    : {
        ...incoming,
        latitude: previous.latitude,
        longitude: previous.longitude,
        heading_deg: previous.heading_deg,
      };
  return current.map((candidate, candidateIndex) => candidateIndex === index ? next : candidate);
}

function reconcileFlightSnapshot(current: FlightRecord[], incoming: FlightRecord[]): FlightRecord[] {
  const incomingIds = new Set(incoming.map((flight) => flight.id));
  return incoming.reduce(
    (result, flight) => reconcileFlightUpdate(result, flight),
    current.filter((flight) => incomingIds.has(flight.id)),
  );
}

function routeDestinationLabel(flight: FlightRecord): string {
  if (flight.scope_type === 'WORLD') return 'the global patrol route';
  if (flight.scope_type === 'CONTINENT') {
    return CONTINENT_OPTIONS.find(([code]) => code === flight.scope_code)?.[1] ?? flight.scope_code;
  }
  return COUNTRY_OPTIONS.find(([code]) => code === flight.scope_code)?.[1] ?? flight.scope_code;
}

function markerLabelTransform([x, y]: [number, number], viewport: MapViewport): string {
  const labelX = x > viewport.x + viewport.width / 2
    ? -(MARKER_LABEL_WIDTH + MARKER_LABEL_GAP)
    : MARKER_LABEL_GAP;
  const labelY = y < viewport.y + 48 / viewport.zoom ? MARKER_LABEL_GAP : -14;
  return `translate(${labelX} ${labelY})`;
}

function telemetryValue(value: unknown): string {
  if (typeof value === 'number') return Number.isInteger(value) ? String(value) : value.toFixed(2);
  if (typeof value === 'string' || typeof value === 'boolean') return String(value);
  return JSON.stringify(value);
}

export function OperationsMap({ token, currentUser, aircraft, runs = [] }: OperationsMapProps) {
  const [flights, setFlights] = useState<FlightRecord[]>([]);
  const [permissions, setPermissions] = useState<OperationPermission[]>([]);
  const [selectedFlightId, setSelectedFlightId] = useState<number | null>(null);
  const [dataMode, setDataMode] = useState<DataMode>('loading');
  const [streamState, setStreamState] = useState<StreamState>('connecting');
  const [placement, setPlacement] = useState<PlacementDraft | null>(null);
  const [messages, setMessages] = useState<Record<number, ChatMessageRecord[]>>({});
  const [messageText, setMessageText] = useState('');
  const [shareType, setShareType] = useState<ChatShareType>('TEXT');
  const [scenarioChoice, setScenarioChoice] = useState<{ flightId: number; runId: string } | null>(null);
  const [scenarioMenuOpen, setScenarioMenuOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [viewport, setViewport] = useState<MapViewport>(INITIAL_MAP_VIEWPORT);
  const [hoveredCountry, setHoveredCountry] = useState<HoveredCountry | null>(null);
  const [countrySearch, setCountrySearch] = useState('');
  const mapRef = useRef<SVGSVGElement>(null);
  const mapStageRef = useRef<HTMLDivElement>(null);
  const eventSequencesRef = useRef(new Map<number, number>());
  const panRef = useRef<MapPan | null>(null);
  const suppressMapClickRef = useRef(false);
  const isAdmin = hasRole(currentUser, 'Admin');
  const selectedFlight = flights.find((flight) => flight.id === selectedFlightId) ?? flights[0];
  const scenarioRunId = selectedFlight && scenarioChoice?.flightId === selectedFlight.id
    ? scenarioChoice.runId
    : selectedFlight ? String(selectedFlight.run_id) : '';

  useEffect(() => {
    const stage = mapStageRef.current;
    if (!stage) return;
    const preventBrowserScroll = (event: globalThis.WheelEvent) => {
      event.preventDefault();
      event.stopPropagation();
    };
    stage.addEventListener('wheel', preventBrowserScroll, {
      passive: false,
      capture: true,
    });
    return () => stage.removeEventListener('wheel', preventBrowserScroll, true);
  }, []);

  const handleError = useCallback((requestError: unknown) => {
    setError(requestError instanceof Error ? requestError.message : 'The operation failed. Try again.');
  }, []);

  const handleMapOperationError = useCallback((requestError: unknown) => {
    if (requestError instanceof ApiError) {
      if (requestError.status === 401) {
        setError('The session could not authorize this map change. Sign in again only if the session has expired.');
      } else if (requestError.status === 403) {
        setError('You do not have permission to change this flight.');
      } else if (requestError.status === 409) {
        setError(requestError.message || 'The flight changed. Refresh and try again.');
      } else {
        setError(requestError.message);
      }
      return;
    }
    setError(requestError instanceof Error ? requestError.message : 'The map operation failed. Try again.');
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    async function load() {
      const [flightResult, permissionResult] = await Promise.allSettled([
        getOperationFlights(token, controller.signal),
        getOperationPermissions(token, controller.signal),
      ]);
      if (!active) return;

      if (flightResult.status === 'fulfilled') {
        const validFlights = reconcileFlightSnapshot([], flightResult.value);
        setFlights(validFlights);
        setSelectedFlightId((current) => current ?? validFlights[0]?.id ?? null);
        setStreamState('connecting');
        setDataMode('api');
      } else if (isApiUnavailable(flightResult.reason)) {
        const demoFlights = createDemoFlights(aircraft, currentUser);
        setFlights(demoFlights);
        setSelectedFlightId(demoFlights[0]?.id ?? null);
        setDataMode('demo');
        setStreamState('snapshot');
      } else {
        setDataMode('error');
        handleError(flightResult.reason);
      }

      if (permissionResult.status === 'fulfilled') {
        setPermissions(permissionResult.value);
      }
    }
    void load();
    return () => {
      active = false;
      controller.abort();
    };
  }, [aircraft, currentUser, handleError, token]);

  useEffect(() => {
    if (dataMode !== 'api') return;
    let active = true;
    let disconnect: (() => Promise<void>) | undefined;
    const acceptEvent = (flightId: number, sequence?: number) => {
      if (!sequence) return true;
      const previous = eventSequencesRef.current.get(flightId) ?? 0;
      if (sequence <= previous) return false;
      eventSequencesRef.current.set(flightId, sequence);
      return true;
    };
    connectOperationsHub(token, {
      onFlightUpdated: (flight) => {
        if (!acceptEvent(flight.id, flight.event_sequence)) return;
        setFlights((current) => reconcileFlightUpdate(current, flight));
      },
      onFlightRemoved: (flightId, eventSequence) => {
        if (!acceptEvent(flightId, eventSequence)) return;
        setFlights((current) => current.filter((flight) => flight.id !== flightId));
        setSelectedFlightId((current) => current === flightId ? null : current);
      },
      onMessagePosted: (message) => {
        setMessages((current) => {
          const existing = current[message.flight_id] ?? [];
          return existing.some((candidate) => candidate.id === message.id)
            ? current
            : { ...current, [message.flight_id]: [...existing, message] };
        });
      },
      onReconnected: async () => {
        try {
          const snapshot = await getOperationFlights(token);
          setFlights((current) => reconcileFlightSnapshot(current, snapshot));
          setSelectedFlightId((current) => snapshot.some((flight) => flight.id === current)
            ? current
            : snapshot.find(hasValidFlightPosition)?.id ?? null);
          setStreamState('connected');
        } catch (requestError) {
          handleError(requestError);
        }
      },
    }).then((stop) => {
      if (!active) {
        void stop();
        return;
      }
      disconnect = stop;
      setStreamState('connected');
    }).catch(() => {
      if (active) setStreamState('snapshot');
    });
    return () => {
      active = false;
      if (disconnect) void disconnect();
    };
  }, [dataMode, handleError, token]);

  useEffect(() => {
    if (dataMode !== 'api' || streamState !== 'snapshot') return;
    let active = true;
    const refresh = async () => {
      try {
        const snapshot = await getOperationFlights(token);
        if (!active) return;
        setFlights((current) => reconcileFlightSnapshot(current, snapshot));
        setSelectedFlightId((current) => snapshot.some((flight) => flight.id === current)
          ? current
          : snapshot.find(hasValidFlightPosition)?.id ?? null);
      } catch (requestError) {
        if (active) handleError(requestError);
      }
    };
    const interval = window.setInterval(() => void refresh(), 10_000);
    return () => {
      active = false;
      window.clearInterval(interval);
    };
  }, [dataMode, handleError, streamState, token]);

  useEffect(() => {
    if (!selectedFlight) return;
    if (messages[selectedFlight.id]) return;
    if (dataMode === 'demo') return;
    if (dataMode !== 'api') return;
    const controller = new AbortController();
    getOperationMessages(token, selectedFlight.id, controller.signal)
      .then((records) => setMessages((current) => ({ ...current, [selectedFlight.id]: records })))
      .catch((requestError: unknown) => {
        if (!(requestError instanceof DOMException && requestError.name === 'AbortError')) handleError(requestError);
      });
    return () => controller.abort();
  }, [currentUser, dataMode, handleError, messages, selectedFlight, token]);

  const availableAircraft = useMemo(() => {
    const activeAircraftIds = new Set(flights.map((flight) => flight.aircraft_id));
    return aircraft.filter((item) => item.status === 'ACTIVE'
      && !activeAircraftIds.has(item.id)
      && (isAdmin
        || item.current_user_access === 'OWNER'
        || item.current_user_access === 'MAINTAINER'));
  }, [aircraft, flights, isAdmin]);
  const availableRuns = useMemo(
    () => runs.filter((run): run is RunArchiveEntry & { id: number } => run.source === 'api' && typeof run.id === 'number'),
    [runs],
  );
  const canPlace = isAdmin || permissions.includes('FLIGHT_PLACE_OWN');
  const canSendChat = isAdmin || permissions.includes('CHAT_SEND');
  const canShareTelemetry = isAdmin || (
    permissions.includes('TELEMETRY_SHARE')
      && (selectedFlight?.current_user_access === 'OWNER'
        || selectedFlight?.current_user_access === 'MAINTAINER')
  );
  const flightMessages = selectedFlight
    ? messages[selectedFlight.id] ?? (dataMode === 'demo' ? createDemoMessages(selectedFlight, currentUser) : [])
    : [];
  const selectedCountryFeature = placement?.scopeType === 'COUNTRY'
    ? findCountryFeature(placement.scopeCode)
    : undefined;
  const selectedCountryDetails = selectedCountryFeature
    ? countryDetails(selectedCountryFeature)
    : undefined;

  function beginCreate() {
    const firstAircraft = availableAircraft[0];
    const firstRun = availableRuns[0];
    if (!firstAircraft || !firstRun) return;
    setPlacement({
      kind: 'create',
      aircraftId: String(firstAircraft.id),
      runId: String(firstRun.id),
      scopeType: 'WORLD',
      scopeCode: '',
      speedKph: '720',
      coordinates: '',
      latitude: '',
      longitude: '',
    });
    setCountrySearch('');
    setError('');
    setNotice('Set the aircraft start position. A selected continent is its destination, so the start may be elsewhere.');
  }

  function beginMove(flight: FlightRecord) {
    setPlacement({
      kind: 'move',
      flightId: flight.id,
      aircraftId: String(flight.aircraft_id),
      runId: flight.run_id ? String(flight.run_id) : '',
      scopeType: flight.scope_type,
      scopeCode: flight.scope_code ?? '',
      speedKph: String(flight.speed_kph),
      coordinates: formatCoordinatePair(flight.latitude, flight.longitude),
      latitude: formatInputCoordinate(flight.latitude),
      longitude: formatInputCoordinate(flight.longitude),
    });
    setCountrySearch(
      flight.scope_type === 'COUNTRY'
        ? COUNTRY_OPTIONS.find(([code]) => code === flight.scope_code)?.[1] ?? flight.scope_code
        : '',
    );
    setError('');
    setNotice('Set a new start position and destination. The aircraft will enter transit before patrolling that area.');
  }

  function changeCountrySearch(value: string) {
    setCountrySearch(value);
    const match = COUNTRY_OPTIONS.find(([code, label]) =>
      code === value.trim().toUpperCase()
      || label.toLowerCase() === value.trim().toLowerCase());
    setPlacement((current) => {
      if (!current) return current;
      if (!match) return { ...current, scopeCode: '' };
      const feature = findCountryFeature(match[0]);
      const details = feature ? countryDetails(feature) : null;
      return {
        ...current,
        scopeCode: match[0],
        coordinates: details
          ? formatCoordinatePair(details.latitude, details.longitude)
          : current.coordinates,
        latitude: details ? formatInputCoordinate(details.latitude) : current.latitude,
        longitude: details ? formatInputCoordinate(details.longitude) : current.longitude,
      };
    });
  }

  function changeZoom(multiplier: number) {
    setViewport((current) => zoomViewport(current, current.zoom * multiplier));
  }

  function resetMapView() {
    setViewport(INITIAL_MAP_VIEWPORT);
  }

  function focusSelectedFlight() {
    if (!selectedFlight) return;
    const point = projectMarkerCoordinate(selectedFlight.latitude, selectedFlight.longitude);
    if (!point) return;
    setViewport((current) => centerViewport(point, Math.max(3, current.zoom)));
  }

  function mapWheel(event: WheelEvent<SVGSVGElement>) {
    if (!mapRef.current) return;
    event.preventDefault();
    const bounds = mapRef.current.getBoundingClientRect();
    setViewport((current) => {
      const anchor = clientToMapPoint(event.clientX, event.clientY, bounds, current);
      if (!anchor) return current;
      const multiplier = event.deltaY < 0 ? MAP_ZOOM_STEP : 1 / MAP_ZOOM_STEP;
      return zoomViewport(current, current.zoom * multiplier, anchor[0], anchor[1]);
    });
  }

  function mapPointerDown(event: PointerEvent<SVGSVGElement>) {
    if (event.button !== 0 || (event.target as Element).closest?.('.aircraft-marker')) return;
    panRef.current = {
      pointerId: event.pointerId,
      clientX: event.clientX,
      clientY: event.clientY,
      viewport,
      moved: false,
    };
    event.currentTarget.setPointerCapture?.(event.pointerId);
  }

  function mapPointerMove(event: PointerEvent<SVGSVGElement>) {
    const pan = panRef.current;
    if (!pan || pan.pointerId !== event.pointerId || !mapRef.current) return;
    const bounds = mapRef.current.getBoundingClientRect();
    if (!bounds.width || !bounds.height) return;
    const deltaX = event.clientX - pan.clientX;
    const deltaY = event.clientY - pan.clientY;
    if (!pan.moved && Math.hypot(deltaX, deltaY) >= 4) pan.moved = true;
    setViewport(clampViewport({
      ...pan.viewport,
      x: pan.viewport.x - deltaX * pan.viewport.width / bounds.width,
      y: pan.viewport.y - deltaY * pan.viewport.height / bounds.height,
    }));
  }

  function finishMapPan(event: PointerEvent<SVGSVGElement>) {
    const pan = panRef.current;
    if (!pan || pan.pointerId !== event.pointerId) return;
    suppressMapClickRef.current = pan.moved;
    panRef.current = null;
    event.currentTarget.releasePointerCapture?.(event.pointerId);
  }

  function mapClicked(event: MouseEvent<SVGSVGElement>) {
    if (suppressMapClickRef.current) {
      suppressMapClickRef.current = false;
      return;
    }
    if (!placement || !mapRef.current) return;
    const bounds = mapRef.current.getBoundingClientRect();
    const point = clientToMapPoint(event.clientX, event.clientY, bounds, viewport);
    if (!point) return;
    const coordinate = projection.invert?.(point);
    if (!coordinate) return;
    setPlacement((current) => current ? {
      ...current,
      coordinates: formatCoordinatePair(coordinate[1], coordinate[0]),
      longitude: formatInputCoordinate(coordinate[0]),
      latitude: formatInputCoordinate(coordinate[1]),
    } : current);
  }

  function changeCoordinates(value: string) {
    setPlacement((current) => {
      if (!current) return current;
      const parsed = parseCoordinatePair(value);
      return {
        ...current,
        coordinates: value,
        latitude: parsed ? formatInputCoordinate(parsed[0]) : '',
        longitude: parsed ? formatInputCoordinate(parsed[1]) : '',
      };
    });
  }

  async function savePlacement(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!placement) return;
    const input = flightInputFromDraft(placement);
    if (!input) {
      setError('Enter a valid run ID, coordinates from 85° S to 85° N, and a speed from 400 to 1,100 km/h.');
      return;
    }
    if (placement.scopeType !== 'WORLD' && !placement.scopeCode.trim()) {
      setError('Enter a scope code for the selected area.');
      return;
    }
    setBusy(true);
    setError('');
    setNotice('');
    try {
      let saved: FlightRecord;
      if (dataMode === 'demo') {
        if (placement.kind === 'move') {
          const existing = flights.find((flight) => flight.id === placement.flightId);
          if (!existing) return;
          saved = { ...existing, ...input, version: existing.version + 1, updated_at_utc: new Date().toISOString() };
        } else {
          const source = aircraft.find((item) => item.id === input.aircraft_id);
          if (!source) return;
          const nextId = Math.min(0, ...flights.map((flight) => flight.id)) - 1;
          saved = {
            ...createDemoFlights([source], currentUser)[0],
            ...input,
            id: nextId,
            heading_deg: 0,
            can_manage: true,
            can_remove: true,
          };
        }
      } else if (placement.kind === 'move') {
        const existing = flights.find((flight) => flight.id === placement.flightId);
        if (!existing) return;
        saved = await updateOperationFlight(token, existing, input);
      } else {
        saved = await createOperationFlight(token, input);
      }
      setFlights((current) => current.some((flight) => flight.id === saved.id)
        ? current.map((flight) => flight.id === saved.id ? saved : flight)
        : [...current, saved]);
      setSelectedFlightId(saved.id);
      setPlacement(null);
      setNotice(saved.route_phase === 'TRANSIT'
        ? `Flight started in transit to ${routeDestinationLabel(saved)}.`
        : placement.kind === 'move' ? 'Simulated flight position updated.' : 'Simulated flight added to the map.');
    } catch (requestError) {
      handleMapOperationError(requestError);
    } finally {
      setBusy(false);
    }
  }

  async function assignScenario(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedFlight?.can_manage) return;
    const runId = Number(scenarioRunId);
    const run = availableRuns.find((candidate) => candidate.id === runId);
    if (!run || runId === selectedFlight.run_id) return;

    setBusy(true);
    setError('');
    setNotice('');
    try {
      const saved = dataMode === 'api'
        ? await updateOperationFlightScenario(token, selectedFlight, runId)
        : {
            ...selectedFlight,
            run_id: runId,
            scenario: run.scenario,
            scenario_label: run.label,
            scenario_elapsed_ms: 0,
            scenario_duration_ms: run.duration_ms,
            version: selectedFlight.version + 1,
            updated_at_utc: new Date().toISOString(),
          };
      setFlights((current) => current.map((flight) =>
        flight.id === saved.id ? saved : flight));
      setScenarioChoice({ flightId: saved.id, runId: String(saved.run_id) });
      setScenarioMenuOpen(false);
      setNotice(`${saved.scenario_label ?? titleCase(saved.scenario ?? 'Scenario')} assigned to ${saved.tail_number}.`);
    } catch (requestError) {
      handleMapOperationError(requestError);
      setScenarioChoice(null);
    } finally {
      setBusy(false);
    }
  }

  async function removeFlight(flight: FlightRecord) {
    if (!window.confirm(`Remove ${flight.tail_number} from the simulated operations map?`)) return;
    setBusy(true);
    setError('');
    try {
      if (dataMode === 'api') await deleteOperationFlight(token, flight.id);
      setFlights((current) => current.filter((candidate) => candidate.id !== flight.id));
      setSelectedFlightId(null);
      setPlacement(null);
      setNotice('Simulated flight removed from the map.');
    } catch (requestError) {
      handleMapOperationError(requestError);
    } finally {
      setBusy(false);
    }
  }

  async function postMessage(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedFlight || !messageText.trim() || !canSendChat || (shareType === 'TELEMETRY' && !canShareTelemetry)) return;
    setBusy(true);
    setError('');
    try {
      const posted = dataMode === 'api'
        ? await sendOperationMessage(token, selectedFlight.id, messageText.trim(), shareType)
        : {
            id: Date.now(),
            flight_id: selectedFlight.id,
            sender_user_id: currentUser.id,
            sender_username: currentUser.username,
            sender_display_name: currentUser.display_name,
            message: messageText.trim(),
            share_type: shareType,
            payload: shareType === 'LOCATION'
              ? { latitude: selectedFlight.latitude, longitude: selectedFlight.longitude }
              : shareType === 'TELEMETRY' ? selectedFlight.telemetry : null,
            sent_at_utc: new Date().toISOString(),
          } satisfies ChatMessageRecord;
      setMessages((current) => ({
        ...current,
        [selectedFlight.id]: [...(current[selectedFlight.id] ?? (dataMode === 'demo' ? createDemoMessages(selectedFlight, currentUser) : [])), posted],
      }));
      setMessageText('');
      setShareType('TEXT');
    } catch (requestError) {
      handleMapOperationError(requestError);
    } finally {
      setBusy(false);
    }
  }

  function selectMarker(flightId: number, event?: KeyboardEvent<SVGGElement>) {
    if (event && event.key !== 'Enter' && event.key !== ' ') return;
    event?.preventDefault();
    setSelectedFlightId(flightId);
  }

  if (dataMode === 'loading') {
    return <div className="operations-map-loading" role="status"><span className="loader" />Loading simulated flight positions...</div>;
  }

  if (dataMode === 'error') {
    return <div className="operations-map-error" role="alert"><strong>Operations map unavailable</strong><p>{error}</p></div>;
  }

  return (
    <div className="operations-map-workspace">
      <div className="operations-command-bar">
        <div>
          <span className="simulation-flag"><i aria-hidden="true" />Simulation only</span>
          <p>Positions come from recorded scenarios and user placement. AeroGuard doesn't connect to live aircraft.</p>
        </div>
        <div className="operations-command-actions">
          <span className={`stream-state stream-${streamState}`}><i aria-hidden="true" />{dataMode === 'demo' ? 'Session demo' : streamState === 'connected' ? 'SignalR connected' : streamState === 'connecting' ? 'Connecting stream' : 'REST polling'}</span>
          {canPlace && <button type="button" className="button primary-button" onClick={beginCreate} disabled={busy || availableAircraft.length === 0 || availableRuns.length === 0} title={!availableRuns.length ? 'An archived API run is required.' : !availableAircraft.length ? 'An owned aircraft with Active status is required.' : undefined}>Place aircraft</button>}
        </div>
      </div>

      {dataMode === 'demo' && <p className="workspace-message demo" role="status">The operations API is unavailable. This session uses temporary simulated flights and messages.</p>}
      {error && <p className="workspace-message error" role="alert">{error}</p>}
      {notice && <p className="workspace-message success" role="status">{notice}</p>}

      <div className="operations-grid">
        <aside className="flight-index" aria-labelledby="flight-index-title">
          <div className="flight-index-heading">
            <div><p className="section-kicker">Active scenarios</p><h3 id="flight-index-title">Flight board</h3></div>
            <span>{flights.length}</span>
          </div>
          <div className="flight-index-stats" aria-label="Simulated flight condition summary">
            <span><i className="healthy" />{flights.filter((flight) => flight.condition === 'HEALTHY').length} healthy</span>
            <span><i className="attention" />{flights.filter((flight) => flight.condition !== 'HEALTHY').length} attention</span>
          </div>
          <div className="flight-roster" role="list" aria-label="Simulated flights">
            {flights.map((flight) => (
              <div role="listitem" key={flight.id}>
                <button
                  type="button"
                  className={`flight-roster-row${flight.id === selectedFlight?.id ? ' active' : ''}`}
                  onClick={() => setSelectedFlightId(flight.id)}
                  aria-pressed={flight.id === selectedFlight?.id}
                >
                  <span className={`flight-condition condition-${flight.condition.toLowerCase()}`} aria-hidden="true" />
                  <span><strong>{flight.tail_number}</strong><small>{flight.display_name}</small></span>
                  <span><strong>{Math.round(flight.speed_kph)}</strong><small>km/h</small></span>
                </button>
              </div>
            ))}
            {!flights.length && <div className="empty-entity"><strong>No simulated flights</strong><p>Place an owned aircraft to start a scenario.</p></div>}
          </div>
          <div className="permission-summary">
            <span>Your operations access</span>
            <p>{isAdmin ? 'Admin role grants all map controls.' : permissions.length ? permissions.map(permissionLabel).join(', ') : 'View only'}</p>
          </div>
        </aside>

        <section className="world-map-panel" aria-labelledby="operations-map-title">
          <div className="world-map-heading">
            <div><p className="section-kicker">Global view</p><h3 id="operations-map-title">Simulated operations map</h3></div>
            {placement && <span className="placement-mode">Map placement active</span>}
          </div>

          {placement && <form className="placement-card" onSubmit={savePlacement}>
            <div className="placement-card-heading">
              <div><span>{placement.kind === 'create' ? 'New simulated flight' : 'Move simulated flight'}</span><strong>{placement.kind === 'create' ? 'Choose a start point' : selectedFlight?.tail_number}</strong></div>
              <button type="button" className="text-button" onClick={() => setPlacement(null)}>Cancel</button>
            </div>
            <p className="placement-help">Coordinates set the aircraft's start position. Continent selection sets its destination and patrol area. The recorded run drives only this aircraft's telemetry and map animation.</p>
            <div className="placement-fields">
              <label><span>Aircraft</span><select value={placement.aircraftId} disabled={placement.kind === 'move'} onChange={(event) => setPlacement({ ...placement, aircraftId: event.target.value })}>{placement.kind === 'move' && selectedFlight ? <option value={selectedFlight.aircraft_id}>{selectedFlight.tail_number}</option> : availableAircraft.map((item) => <option value={item.id} key={item.id}>{item.tail_number} - {item.display_name}</option>)}</select></label>
              <label><span>Aircraft scenario</span><select value={placement.runId} onChange={(event) => setPlacement({ ...placement, runId: event.target.value })} required><option value="">Select scenario</option>{availableRuns.map((run) => <option value={run.id} key={run.id}>{run.label} / run {run.id}</option>)}{placement.kind === 'move' && placement.runId && !availableRuns.some((run) => String(run.id) === placement.runId) && <option value={placement.runId}>Current run {placement.runId}</option>}</select></label>
              <label><span>Scope</span><select value={placement.scopeType} onChange={(event) => {
                const scopeType = event.target.value as FlightScopeType;
                setCountrySearch('');
                if (scopeType === 'CONTINENT') {
                  setPlacement({
                    ...placement,
                    scopeType,
                    scopeCode: '',
                  });
                } else {
                  setPlacement({
                    ...placement,
                    scopeType,
                    scopeCode: scopeType === 'WORLD' ? 'WORLD' : '',
                  });
                }
              }}>{FLIGHT_SCOPE_TYPES.map((scope) => <option value={scope} key={scope}>{titleCase(scope)}</option>)}</select></label>
              {placement.scopeType === 'CONTINENT' && <label><span>Continent</span><select value={placement.scopeCode} onChange={(event) => {
                const option = CONTINENT_OPTIONS.find(([code]) => code === event.target.value);
                setPlacement(option
                  ? {
                      ...placement,
                      scopeCode: option[0],
                      coordinates: formatCoordinatePair(option[2], option[3]),
                      latitude: formatInputCoordinate(option[2]),
                      longitude: formatInputCoordinate(option[3]),
                    }
                  : { ...placement, scopeCode: '' });
              }} required><option value="">Select continent</option>{CONTINENT_OPTIONS.map(([code, label]) => <option value={code} key={code}>{label}</option>)}</select></label>}
              {placement.scopeType === 'COUNTRY' && <label className="country-search-field"><span>Country</span><input list="country-search-options" value={countrySearch} onChange={(event) => changeCountrySearch(event.target.value)} placeholder="Search country..." autoComplete="off" required /><datalist id="country-search-options">{COUNTRY_OPTIONS.map(([code, label]) => <option value={label} key={code}>{code}</option>)}</datalist></label>}
              <label><span>Start coordinates</span><input type="text" value={placement.coordinates} onChange={(event) => changeCoordinates(event.target.value)} placeholder="55.868581 9.873817" aria-describedby="coordinate-format-help" required /></label>
              <label><span>Speed km/h</span><input type="number" min="400" max="1100" step="1" value={placement.speedKph} onChange={(event) => setPlacement({ ...placement, speedKph: event.target.value })} required /></label>
              <button className="button primary-button" type="submit" disabled={busy}>{busy ? 'Saving...' : placement.kind === 'create' ? 'Start simulated flight' : 'Save position'}</button>
            </div>
            {selectedCountryDetails && placement.scopeType === 'COUNTRY' && <div className="country-search-details" role="status" aria-label="Selected country details">
              <strong>{selectedCountryDetails.name}</strong>
              <span>ISO numeric: {selectedCountryDetails.numericCode}</span>
              <span>Center: {formatCoordinate(selectedCountryDetails.latitude, 'N', 'S')}, {formatCoordinate(selectedCountryDetails.longitude, 'E', 'W')}</span>
            </div>}
          </form>}

          <div
            ref={mapStageRef}
            className={`world-map-stage${placement ? ' placement-active' : ''}`}
            onWheel={(event) => event.preventDefault()}
          >
            {selectedFlight && <div className="selected-flight-map-card" aria-live="polite">
              <span>Selected aircraft</span>
              <strong>{selectedFlight.tail_number}</strong>
              <small>{selectedFlight.display_name}</small>
              <em>{selectedFlight.scenario_label ?? titleCase(selectedFlight.scenario ?? 'Session demo')} / {getMapScenarioEffectLabel(getFlightScenarioEffect(selectedFlight))}</em>
            </div>}
            <div className="map-zoom-controls" role="group" aria-label="Map zoom controls">
              <button type="button" aria-label="Zoom in" onClick={() => changeZoom(MAP_ZOOM_STEP)} disabled={viewport.zoom >= MAX_MAP_ZOOM}>+</button>
              <button type="button" aria-label="Zoom out" onClick={() => changeZoom(1 / MAP_ZOOM_STEP)} disabled={viewport.zoom <= MIN_MAP_ZOOM}>−</button>
              <button type="button" onClick={resetMapView} disabled={viewport.zoom === MIN_MAP_ZOOM}>Reset</button>
              <button type="button" onClick={focusSelectedFlight} disabled={!selectedFlight}>Focus selected</button>
              <output aria-label="Map zoom level">{Math.round(viewport.zoom * 100)}%</output>
            </div>
            <svg
              ref={mapRef}
              data-testid="operations-world-map"
              viewBox={`${viewport.x} ${viewport.y} ${viewport.width} ${viewport.height}`}
              role="group"
              aria-label="Interactive world map with simulated aircraft positions"
              onClick={mapClicked}
              onWheel={mapWheel}
              onPointerDown={mapPointerDown}
              onPointerMove={mapPointerMove}
              onPointerUp={finishMapPan}
              onPointerCancel={finishMapPan}
            >
              <title>Interactive world map with simulated aircraft positions</title>
              <defs>
                <radialGradient id="ocean-glow" cx="50%" cy="42%" r="68%"><stop offset="0%" stopColor="#102c3b" /><stop offset="100%" stopColor="#07131d" /></radialGradient>
                <filter id="marker-glow" x="-80%" y="-80%" width="260%" height="260%"><feGaussianBlur stdDeviation="5" result="blur" /><feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge></filter>
                <clipPath id="operations-map-sphere-clip" clipPathUnits="userSpaceOnUse"><path d={spherePath} /></clipPath>
              </defs>
              <path className="map-sphere" d={spherePath} />
              <path className="map-graticule" d={graticulePath} />
              <path className="map-countries" d={countriesPath} />
              <g className="map-country-hover-layer" aria-label="Countries">
                {countries.features.map((feature, index) => {
                  const name = countryFeatureName(feature);
                  const selected = placement?.scopeType === 'COUNTRY'
                    && (COUNTRY_NAME_ALIASES[placement.scopeCode] ?? []).some((alias) =>
                      alias.toLowerCase() === name.toLowerCase());
                  return <path
                    key={`${String(feature.id ?? 'country')}-${index}`}
                    className={`map-country-hitarea${selected ? ' selected' : ''}`}
                    d={path(feature) ?? ''}
                    onMouseEnter={() => setHoveredCountry(countryDetails(feature))}
                    onMouseLeave={() => setHoveredCountry(null)}
                  ><title>{name}</title></path>;
                })}
              </g>
              {hoveredCountry && <g className="country-hover-card" transform="translate(20 20)" aria-live="polite" pointerEvents="none">
                <rect width="238" height="72" rx="6" />
                <text x="12" y="20" className="country-hover-name">{hoveredCountry.name}</text>
                <text x="12" y="40" className="country-hover-meta">ISO numeric: {hoveredCountry.numericCode}</text>
                <text x="12" y="58" className="country-hover-meta">{formatCoordinate(hoveredCountry.latitude, 'N', 'S')} · {formatCoordinate(hoveredCountry.longitude, 'E', 'W')}</text>
              </g>}
              <g className="map-aircraft-layer" clipPath="url(#operations-map-sphere-clip)">
                {flights.map((flight) => {
                  const point = projectMarkerCoordinate(flight.latitude, flight.longitude);
                  if (!point) return null;
                  const active = flight.id === selectedFlight?.id;
                  const effect = getFlightScenarioEffect(flight);
                  const fallProgress = debrisProgress(flight.scenario_elapsed_ms);
                  return <g
                    key={flight.id}
                    className={`aircraft-marker marker-${flight.condition.toLowerCase()}${active ? ' active' : ''} scenario-effect-${effect}`}
                    transform={`translate(${point[0]} ${point[1]}) scale(${1 / viewport.zoom})`}
                    role="button"
                    tabIndex={0}
                    aria-label={`${flight.tail_number}, simulated position ${formatCoordinate(flight.latitude, 'N', 'S')}, ${formatCoordinate(flight.longitude, 'E', 'W')}, ${getMapScenarioEffectLabel(effect)}, scenario ${flight.scenario_label ?? flight.scenario ?? 'session demo'}`}
                    onClick={(event) => { event.stopPropagation(); setSelectedFlightId(flight.id); }}
                    onKeyDown={(event) => selectMarker(flight.id, event)}
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
                })}
                {placement && flightInputFromDraft(placement) && (() => {
                  const draft = flightInputFromDraft(placement);
                  const point = draft ? projectMarkerCoordinate(draft.latitude, draft.longitude) : null;
                  return point ? <g className="draft-marker" transform={`translate(${point[0]} ${point[1]}) scale(${1 / viewport.zoom})`} aria-hidden="true"><circle r="14" /><path d="M-8 0H8M0-8V8" /></g> : null;
                })()}
              </g>
            </svg>
            <div className="map-scale" aria-hidden="true"><span /><small>Simulation · {Math.round(viewport.zoom * 100)}%</small></div>
            <p className="map-attribution">Map geometry: <a href="https://www.naturalearthdata.com/" target="_blank" rel="noreferrer">Natural Earth</a> via <a href="https://github.com/topojson/world-atlas" target="_blank" rel="noreferrer">world-atlas</a></p>
          </div>
        </section>

        <aside className="flight-control-rail">
          {selectedFlight ? <>
            <section className="flight-detail" aria-labelledby="selected-flight-title">
              <div className="flight-detail-heading">
                <div><span className={`condition-tag condition-${selectedFlight.condition.toLowerCase()}`}>{titleCase(selectedFlight.condition)}</span><h3 id="selected-flight-title">{selectedFlight.tail_number}</h3><p>{selectedFlight.display_name}</p></div>
                <span className="heading-gauge" style={{ '--heading': `${selectedFlight.heading_deg}deg` } as React.CSSProperties}><i aria-hidden="true" /><small>{Math.round(selectedFlight.heading_deg)}°</small></span>
              </div>
              <dl className="flight-facts">
                <div><dt>Latitude</dt><dd>{formatCoordinate(selectedFlight.latitude, 'N', 'S')}</dd></div>
                <div><dt>Longitude</dt><dd>{formatCoordinate(selectedFlight.longitude, 'E', 'W')}</dd></div>
                <div><dt>Speed</dt><dd>{Math.round(selectedFlight.speed_kph)} km/h</dd></div>
                <div><dt>Scenario time</dt><dd>{formatScenarioTime(selectedFlight.scenario_elapsed_ms, selectedFlight.scenario_duration_ms)}</dd></div>
                <div><dt>Map effect</dt><dd>{getMapScenarioEffectLabel(getFlightScenarioEffect(selectedFlight))}</dd></div>
                <div><dt>Scope</dt><dd>{titleCase(selectedFlight.scope_type)}{selectedFlight.scope_type !== 'WORLD' ? ` / ${selectedFlight.scope_code}` : ''}</dd></div>
                <div><dt>Route</dt><dd>{selectedFlight.route_phase === 'TRANSIT' ? `Transit ${Math.round(Math.max(0, Math.min(1, selectedFlight.route_progress)) * 100)}%` : 'Patrol'}</dd></div>
                <div><dt>Access</dt><dd>{isAdmin ? 'Administrator' : titleCase(selectedFlight.current_user_access ?? 'VIEWER')}</dd></div>
              </dl>
              <div className={`route-progress route-${selectedFlight.route_phase.toLowerCase()}`}>
                <span>{selectedFlight.route_phase === 'TRANSIT' ? `En route to ${routeDestinationLabel(selectedFlight)}` : `Patrolling ${routeDestinationLabel(selectedFlight)}`}</span>
                <progress aria-label="Route progress" max="1" value={selectedFlight.route_phase === 'TRANSIT' ? Math.max(0, Math.min(1, selectedFlight.route_progress)) : 1} />
              </div>
              <div className="flight-scenario"><span>Assigned scenario</span><strong>{selectedFlight.scenario_label ?? titleCase(selectedFlight.scenario ?? 'Session demo')}</strong><small>{selectedFlight.run_id > 0 ? `Run #${selectedFlight.run_id} / ${selectedFlight.tail_number}` : 'Session data'}</small></div>
              {selectedFlight.can_manage && availableRuns.length > 0 && <form className="flight-scenario-assignment" onSubmit={assignScenario}>
                <div className="scenario-dropdown-field">
                  <span>Change scenario for {selectedFlight.tail_number}</span>
                  <div className={`scenario-dropdown${scenarioMenuOpen ? ' open' : ''}`} onBlur={(event) => {
                    if (!event.currentTarget.contains(event.relatedTarget)) setScenarioMenuOpen(false);
                  }}>
                    <button
                      type="button"
                      className="scenario-dropdown-trigger"
                      aria-label={`Scenario assigned to ${selectedFlight.tail_number}`}
                      aria-haspopup="listbox"
                      aria-expanded={scenarioMenuOpen}
                      aria-controls={`scenario-options-${selectedFlight.id}`}
                      onClick={() => setScenarioMenuOpen((open) => !open)}
                      onKeyDown={(event) => {
                        if (event.key === 'ArrowDown') {
                          event.preventDefault();
                          setScenarioMenuOpen(true);
                        }
                        if (event.key === 'Escape') setScenarioMenuOpen(false);
                      }}
                    >
                      <span>
                        <strong>{availableRuns.find((run) => String(run.id) === scenarioRunId)?.label ?? selectedFlight.scenario_label ?? 'Current scenario'}</strong>
                        <small>Run #{scenarioRunId}</small>
                      </span>
                      <i aria-hidden="true" />
                    </button>
                    {scenarioMenuOpen && <div className="scenario-dropdown-menu" id={`scenario-options-${selectedFlight.id}`} role="listbox" aria-label={`Available scenarios for ${selectedFlight.tail_number}`}>
                      {availableRuns.map((run) => <button
                        type="button"
                        role="option"
                        aria-selected={String(run.id) === scenarioRunId}
                        className={String(run.id) === scenarioRunId ? 'selected' : ''}
                        key={run.id}
                        onClick={() => {
                          setScenarioChoice({ flightId: selectedFlight.id, runId: String(run.id) });
                          setScenarioMenuOpen(false);
                        }}
                      >
                        <span className={`scenario-option-state state-${run.final_state.toLowerCase()}`} aria-hidden="true" />
                        <span><strong>{run.label}</strong><small>Run #{run.id} / {Math.round(run.duration_ms / 1000)} s</small></span>
                        {String(run.id) === scenarioRunId && <span className="scenario-option-check" aria-hidden="true">&#10003;</span>}
                      </button>)}
                      {!availableRuns.some((run) => run.id === selectedFlight.run_id) && <button
                        type="button"
                        role="option"
                        aria-selected={String(selectedFlight.run_id) === scenarioRunId}
                        className={String(selectedFlight.run_id) === scenarioRunId ? 'selected' : ''}
                        onClick={() => {
                          setScenarioChoice({ flightId: selectedFlight.id, runId: String(selectedFlight.run_id) });
                          setScenarioMenuOpen(false);
                        }}
                      ><span className="scenario-option-state state-normal" aria-hidden="true" /><span><strong>{selectedFlight.scenario_label ?? `Current run ${selectedFlight.run_id}`}</strong><small>Current aircraft run</small></span></button>}
                    </div>}
                  </div>
                </div>
                <button type="submit" className="button secondary-button" disabled={busy || Number(scenarioRunId) === selectedFlight.run_id}>Apply scenario</button>
                <small>This changes only the selected aircraft's replay and map effect. Its destination remains the same.</small>
              </form>}
              <div className="flight-owner"><span>Owner</span><strong>{selectedFlight.owner_display_name}</strong><small>@{selectedFlight.owner_username}</small></div>
              <div className="flight-control-actions">
                {selectedFlight.can_manage && <button type="button" className="button secondary-button" disabled={busy} onClick={() => beginMove(selectedFlight)}>Move aircraft</button>}
                {selectedFlight.can_remove && <button type="button" className="text-button danger-text" disabled={busy} onClick={() => void removeFlight(selectedFlight)}>Remove</button>}
              </div>
            </section>

            <section className="flight-telemetry" aria-labelledby="flight-telemetry-title">
              <div className="rail-section-heading"><div><p className="section-kicker">Scenario feed</p><h4 id="flight-telemetry-title">Telemetry</h4></div><span>{selectedFlight.telemetry.length}</span></div>
              <div className="telemetry-snapshot-list">
                {selectedFlight.telemetry.map((record, index) => <article key={`${record.subsystem}-${index}`}>
                  <div><strong>{titleCase(record.subsystem)}</strong><span className={record.valid ? 'valid' : 'invalid'}>{record.valid ? record.health : 'Invalid'}</span></div>
                  {record.reason && <p>{record.reason}</p>}
                  <dl>{Object.entries(record.data).slice(0, 3).map(([key, value]) => <div key={key}><dt>{titleCase(key)}</dt><dd>{telemetryValue(value)}</dd></div>)}</dl>
                </article>)}
                {!selectedFlight.telemetry.length && <p className="empty-copy">No telemetry snapshot is attached to this flight.</p>}
              </div>
            </section>

            <section className="flight-chat" aria-labelledby="flight-chat-title">
              <div className="rail-section-heading"><div><p className="section-kicker">Crew room</p><h4 id="flight-chat-title">Flight chat</h4></div><span>{flightMessages.length}</span></div>
              <div className="chat-message-list" aria-live="polite">
                {flightMessages.map((message) => <article key={message.id} className={message.sender_user_id === currentUser.id ? 'own' : ''}>
                  <div><strong>{message.sender_display_name}</strong><time dateTime={message.sent_at_utc}>{new Date(message.sent_at_utc).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}</time></div>
                  {message.share_type !== 'TEXT' && <span className="share-tag">{titleCase(message.share_type)} shared</span>}
                  <p>{message.message}</p>
                </article>)}
                {!flightMessages.length && <p className="empty-copy">No messages in this simulated flight room.</p>}
              </div>
              {canSendChat ? <form className="chat-composer" onSubmit={postMessage}>
                <label><span>Message</span><textarea rows={2} maxLength={500} value={messageText} onChange={(event) => setMessageText(event.target.value)} placeholder="Write to the flight room" required /></label>
                <div><label><span className="sr-only">Share type</span><select aria-label="Share type" value={shareType} onChange={(event) => setShareType(event.target.value as ChatShareType)}>{CHAT_SHARE_TYPES.filter((type) => type !== 'TELEMETRY' || canShareTelemetry).map((type) => <option value={type} key={type}>{titleCase(type)}</option>)}</select></label><button type="submit" className="button primary-button" disabled={busy || !messageText.trim()}>Send</button></div>
              </form> : <p className="chat-read-only">You have read-only access to this flight room.</p>}
            </section>
          </> : <div className="empty-entity"><strong>Select a simulated flight</strong><p>Telemetry, ownership and crew messages appear here.</p></div>}
        </aside>
      </div>
    </div>
  );
}

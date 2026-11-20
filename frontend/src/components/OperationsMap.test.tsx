import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { AircraftRecord, FlightRecord, UserAccount } from '../api/types';
import type { RunArchiveEntry } from '../types';
import { OperationsMap } from './OperationsMap';

const hubMocks = vi.hoisted(() => ({
  connect: vi.fn().mockResolvedValue(async () => undefined),
}));

vi.mock('../api/operationsHub', () => ({ connectOperationsHub: hubMocks.connect }));

function response(body: unknown, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: vi.fn().mockResolvedValue(body),
  } as unknown as Response;
}

const admin: UserAccount = {
  id: 1,
  username: 'admin',
  display_name: 'AeroGuard Administrator',
  is_active: true,
  roles: ['Admin'],
  created_at_utc: '2030-01-01T08:00:00Z',
  updated_at_utc: '2030-01-01T08:00:00Z',
};

const owner: UserAccount = {
  ...admin,
  id: 2,
  username: 'operator',
  display_name: 'Flight Operator',
  roles: ['Operator'],
};

const aircraft: AircraftRecord[] = [{
  id: 9,
  tail_number: 'N204AG',
  display_name: 'Test aircraft 01',
  model: 'Research platform',
  status: 'ACTIVE',
  notes: 'Assigned to the systems team.',
  version: 1,
  created_by: 1,
  created_at_utc: '2030-01-01T08:00:00Z',
  updated_at_utc: '2030-01-01T08:00:00Z',
  current_user_access: 'OWNER',
}];

const flight: FlightRecord = {
  id: 44,
  aircraft_id: 9,
  tail_number: 'N204AG',
  display_name: 'Test aircraft 01',
  owner_user_id: 2,
  owner_username: 'operator',
  owner_display_name: 'Flight Operator',
  current_user_access: 'OWNER',
  scope_type: 'WORLD',
  scope_code: 'WORLD',
  latitude: 52.23,
  longitude: 21.01,
  heading_deg: 87,
  speed_kph: 735,
  run_id: 11,
  scenario: 'normal-operation',
  scenario_label: 'Normal operation',
  scenario_elapsed_ms: 15_000,
  scenario_duration_ms: 60_000,
  condition: 'HEALTHY',
  telemetry: [{ subsystem: 'power', health: 'HEALTHY', valid: true, reason: null, data: { bus_voltage_v: 27.8 } }],
  version: 3,
  started_at_utc: '2030-01-01T08:00:00Z',
  updated_at_utc: '2030-01-01T08:00:15Z',
  can_manage: true,
  can_remove: true,
};

const runs: RunArchiveEntry[] = [{
  id: 11,
  scenario: 'normal-operation',
  label: 'Normal operation',
  description: 'Recorded test run',
  final_state: 'NORMAL',
  duration_ms: 60_000,
  tick_ms: 100,
  record_count: 600,
  replay_url: '/api/runs/11/replay',
  source: 'api',
}];

function apiMock({ flights = [flight], permissions = ['FLIGHT_PLACE_OWN', 'CHAT_SEND', 'TELEMETRY_SHARE'] as string[] } = {}) {
  return vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url === '/api/operations/flights' && init?.method === 'POST') {
      const body = JSON.parse(String(init.body));
      return Promise.resolve(response({ ...flight, id: 45, ...body, version: 1 }));
    }
    if (url === '/api/operations/flights/44' && init?.method === 'PUT') {
      const body = JSON.parse(String(init.body));
      return Promise.resolve(response({ ...flight, ...body, version: flight.version + 1 }));
    }
    if (url === '/api/operations/flights') return Promise.resolve(response(flights));
    if (url === '/api/operations/permissions') return Promise.resolve(response(permissions));
    if (/^\/api\/operations\/flights\/\d+\/messages\?limit=100$/.test(url)) return Promise.resolve(response([]));
    if (url === '/api/operations/flights/44/messages' && init?.method === 'POST') {
      const body = JSON.parse(String(init.body));
      return Promise.resolve(response({
        id: 91,
        flight_id: 44,
        sender_user_id: owner.id,
        sender_username: owner.username,
        sender_display_name: owner.display_name,
        payload: null,
        sent_at_utc: '2030-01-01T08:01:00Z',
        ...body,
      }));
    }
    return Promise.resolve(response({}, 404));
  });
}

describe('OperationsMap', () => {
  beforeEach(() => {
    hubMocks.connect.mockClear();
    hubMocks.connect.mockResolvedValue(async () => undefined);
  });

  it('renders published world geometry and exposes selectable simulated aircraft', async () => {
    const secondFlight = { ...flight, id: 45, tail_number: 'N305AG', display_name: 'Test aircraft 02', latitude: 35.68, longitude: 139.65 };
    vi.stubGlobal('fetch', apiMock({ flights: [flight, secondFlight] }));
    render(<OperationsMap token="session-token" currentUser={owner} aircraft={aircraft} runs={runs} onUnauthorized={vi.fn()} />);

    expect(await screen.findByRole('heading', { name: 'Simulated operations map' })).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'Interactive world map with simulated aircraft positions' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Natural Earth' })).toHaveAttribute('href', 'https://www.naturalearthdata.com/');
    expect(screen.getByRole('button', { name: /N204AG, simulated position/ })).toBeInTheDocument();
    expect(screen.getByText('735 km/h')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Telemetry' })).toBeInTheDocument();
    fireEvent.keyDown(screen.getByRole('button', { name: /N305AG, simulated position/ }), { key: 'Enter' });
    expect(screen.getByRole('heading', { name: 'N305AG' })).toBeInTheDocument();
    await waitFor(() => expect(hubMocks.connect).toHaveBeenCalledWith('session-token', expect.objectContaining({
      onFlightUpdated: expect.any(Function),
      onMessagePosted: expect.any(Function),
      onReconnected: expect.any(Function),
    })));
  });

  it('clips boundary markers to the globe and keeps their labels pointed inward', async () => {
    const boundaryFlight = { ...flight, latitude: 85, longitude: 180 };
    vi.stubGlobal('fetch', apiMock({ flights: [boundaryFlight] }));
    render(<OperationsMap token="session-token" currentUser={owner} aircraft={aircraft} runs={runs} onUnauthorized={vi.fn()} />);

    const marker = await screen.findByRole('button', { name: /N204AG, simulated position/ });
    const layer = marker.closest('.map-aircraft-layer');
    const label = marker.querySelector('.marker-label');

    expect(layer).toHaveAttribute('clip-path', 'url(#operations-map-sphere-clip)');
    expect(screen.getByTestId('operations-world-map').querySelector('#operations-map-sphere-clip path')).toHaveAttribute('d');
    expect(label?.getAttribute('transform')).toMatch(/^translate\(-88 12\)$/);
  });

  it('does not project an out-of-range position from a live update', async () => {
    vi.stubGlobal('fetch', apiMock());
    render(<OperationsMap token="session-token" currentUser={owner} aircraft={aircraft} runs={runs} onUnauthorized={vi.fn()} />);
    expect(await screen.findByRole('button', { name: /N204AG, simulated position/ })).toBeInTheDocument();
    await waitFor(() => expect(hubMocks.connect).toHaveBeenCalled());
    const handlers = hubMocks.connect.mock.calls[0][1];

    act(() => handlers.onFlightUpdated({ ...flight, latitude: 91, event_sequence: 201 }));

    expect(screen.queryByRole('button', { name: /N204AG, simulated position/ })).not.toBeInTheDocument();
  });

  it('uses a labelled session demo only when the operations API is unavailable', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input);
      if (url === '/api/operations/flights') return Promise.resolve(response({}, 503));
      if (url === '/api/operations/permissions') return Promise.resolve(response(['CHAT_SEND']));
      return Promise.resolve(response({}, 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<OperationsMap token="session-token" currentUser={owner} aircraft={aircraft} runs={runs} onUnauthorized={vi.fn()} />);

    expect(await screen.findByText('The operations API is unavailable. This session uses temporary simulated flights and messages.')).toBeInTheDocument();
    expect(screen.getAllByText('Session demo')).toHaveLength(2);
    expect(screen.getByRole('button', { name: /N204AG, simulated position/ })).toBeInTheDocument();
    expect(hubMocks.connect).not.toHaveBeenCalled();
  });

  it('ignores a delayed live update after a newer visibility removal', async () => {
    vi.stubGlobal('fetch', apiMock());
    render(<OperationsMap token="session-token" currentUser={owner} aircraft={aircraft} runs={runs} onUnauthorized={vi.fn()} />);
    expect(await screen.findByRole('button', { name: /N204AG, simulated position/ })).toBeInTheDocument();
    await waitFor(() => expect(hubMocks.connect).toHaveBeenCalled());
    const handlers = hubMocks.connect.mock.calls[0][1];

    act(() => handlers.onFlightRemoved(44, 200));
    expect(screen.queryByRole('button', { name: /N204AG, simulated position/ })).not.toBeInTheDocument();

    act(() => handlers.onFlightUpdated({ ...flight, event_sequence: 199 }));
    expect(screen.queryByRole('button', { name: /N204AG, simulated position/ })).not.toBeInTheDocument();

    act(() => handlers.onFlightUpdated({ ...flight, event_sequence: 201 }));
    expect(screen.getByRole('button', { name: /N204AG, simulated position/ })).toBeInTheDocument();
  });

  it('places an owned aircraft from a map click and sends the REST payload', async () => {
    const fetchMock = apiMock({ flights: [] });
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    render(<OperationsMap token="session-token" currentUser={owner} aircraft={aircraft} runs={runs} onUnauthorized={vi.fn()} />);

    await user.click(await screen.findByRole('button', { name: 'Place aircraft' }));
    expect(screen.getByLabelText('Recorded run')).toBeRequired();
    expect(screen.getByLabelText('Latitude')).toHaveAttribute('min', '-85');
    expect(screen.getByLabelText('Latitude')).toHaveAttribute('max', '85');
    expect(screen.getByLabelText('Speed km/h')).toHaveAttribute('min', '400');
    expect(screen.getByLabelText('Speed km/h')).toHaveAttribute('max', '1100');
    const map = screen.getByTestId('operations-world-map');
    vi.spyOn(map, 'getBoundingClientRect').mockReturnValue({
      x: 0, y: 0, top: 0, right: 1080, bottom: 570, left: 0, width: 1080, height: 570, toJSON: () => ({}),
    });
    fireEvent.click(map, { clientX: 540, clientY: 285 });

    expect((screen.getByLabelText('Latitude') as HTMLInputElement).value).not.toBe('');
    expect((screen.getByLabelText('Longitude') as HTMLInputElement).value).not.toBe('');
    await user.clear(screen.getByLabelText('Speed km/h'));
    await user.type(screen.getByLabelText('Speed km/h'), '680');
    await user.click(screen.getByRole('button', { name: 'Start simulated flight' }));

    await waitFor(() => {
      const createCall = fetchMock.mock.calls.find(([url, init]) => String(url) === '/api/operations/flights' && init?.method === 'POST');
      expect(createCall).toBeDefined();
      expect(JSON.parse(String(createCall?.[1]?.body))).toMatchObject({
        aircraft_id: 9,
        run_id: 11,
        scope_type: 'WORLD',
        scope_code: 'WORLD',
        speed_kph: 680,
      });
    });
    expect(await screen.findByText('Simulated flight added to the map.')).toBeInTheDocument();
  });

  it('updates the route area and position through the REST API', async () => {
    const fetchMock = apiMock();
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    render(<OperationsMap token="session-token" currentUser={owner} aircraft={aircraft} runs={runs} onUnauthorized={vi.fn()} />);

    await user.click(await screen.findByRole('button', { name: 'Move aircraft' }));
    await user.selectOptions(screen.getByLabelText('Scope'), 'CONTINENT');
    await user.selectOptions(screen.getByLabelText('Continent'), 'EU');
    await user.clear(screen.getByLabelText('Latitude'));
    await user.type(screen.getByLabelText('Latitude'), '54.352');
    await user.clear(screen.getByLabelText('Longitude'));
    await user.type(screen.getByLabelText('Longitude'), '18.646');
    await user.click(screen.getByRole('button', { name: 'Save position' }));

    await waitFor(() => {
      const updateCall = fetchMock.mock.calls.find(([url, init]) => String(url) === '/api/operations/flights/44' && init?.method === 'PUT');
      expect(updateCall).toBeDefined();
      expect(JSON.parse(String(updateCall?.[1]?.body))).toMatchObject({
        expected_version: 3,
        scope_type: 'CONTINENT',
        scope_code: 'EU',
        latitude: 54.352,
        longitude: 18.646,
      });
    });
    expect(await screen.findByText('Simulated flight position updated.')).toBeInTheDocument();
  });

  it('gates chat controls by permission and posts the selected share type', async () => {
    const fetchMock = apiMock({ permissions: ['CHAT_SEND', 'TELEMETRY_SHARE'] });
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    const view = render(<OperationsMap token="session-token" currentUser={owner} aircraft={aircraft} runs={runs} onUnauthorized={vi.fn()} />);

    await user.type(await screen.findByLabelText('Message'), 'Position checked.');
    await user.selectOptions(screen.getByLabelText('Share type'), 'LOCATION');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('/api/operations/flights/44/messages', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ message: 'Position checked.', share_type: 'LOCATION' }),
    })));
    expect(await screen.findByText('Position checked.')).toBeInTheDocument();

    view.unmount();
    vi.stubGlobal('fetch', apiMock({ permissions: [] }));
    render(<OperationsMap token="viewer-token" currentUser={{ ...owner, id: 3, username: 'viewer', roles: ['Viewer'] }} aircraft={aircraft} runs={runs} onUnauthorized={vi.fn()} />);
    expect(await screen.findByText('You have read-only access to this flight room.')).toBeInTheDocument();
  });

  it('requires owner access before offering telemetry shares', async () => {
    vi.stubGlobal('fetch', apiMock({
      flights: [{ ...flight, current_user_access: 'VIEWER', can_manage: false, can_remove: false }],
      permissions: ['CHAT_SEND', 'TELEMETRY_SHARE'],
    }));
    render(<OperationsMap token="viewer-token" currentUser={{ ...owner, id: 3, username: 'viewer', roles: ['Viewer'] }} aircraft={aircraft} runs={runs} onUnauthorized={vi.fn()} />);

    expect(await screen.findByLabelText('Share type')).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Text' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Location' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'Telemetry' })).not.toBeInTheDocument();
    expect(screen.getByLabelText('Message')).toHaveAttribute('maxlength', '500');
  });
});

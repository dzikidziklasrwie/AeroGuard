import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import App from './App';
import { jsonlFixture, manifestFixture } from './test/fixtures';

function response(body: unknown, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: vi.fn().mockResolvedValue(body),
    text: vi.fn().mockResolvedValue(String(body)),
  } as unknown as Response;
}

describe('AeroGuard dashboard', () => {
  it('loads recorded data with automatic replay and no playback controls', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, _init?: RequestInit) => {
      void _init;
      const url = String(input);
      if (url === '/data/manifest.json') return Promise.resolve(response(manifestFixture));
      if (url === '/data/normal-operation.jsonl') return Promise.resolve(response(jsonlFixture));
      return Promise.resolve(response('', 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<App />);

    expect(await screen.findByRole('heading', { name: 'System health' })).toBeInTheDocument();
    expect(screen.getByText('Normal', { selector: '.status-badge' })).toBeInTheDocument();
    for (const subsystem of [
      'Power Supply',
      'Cooling System',
      'Temperature Sensors',
      'Communication Module',
      'Onboard Computer',
      'Equipment Monitor',
    ]) {
      expect(screen.getByRole('heading', { name: subsystem })).toBeInTheDocument();
    }
    expect(screen.getByRole('heading', { name: 'Telemetry trend' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Recorded timeline' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Simulation statistics' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Architecture overview' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Download report' })).toBeEnabled();
    expect(screen.getByText('INITIALIZING → NORMAL')).toBeInTheDocument();
    expect(screen.getByText('60')).toBeInTheDocument();
    expect(screen.queryByRole('slider')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /play replay|pause replay|replay scenario/i })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Telemetry playback controls')).not.toBeInTheDocument();
    expect(screen.getByText('Continuous replay active')).toBeInTheDocument();

    expect(fetchMock).toHaveBeenCalledWith('/data/manifest.json', expect.any(Object));
    expect(fetchMock).toHaveBeenCalledWith('/data/normal-operation.jsonl', expect.any(Object));
  });

  it('filters the event timeline by severity and subsystem', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL) =>
      Promise.resolve(String(input).includes('manifest') ? response(manifestFixture) : response(jsonlFixture)),
    ));
    const user = userEvent.setup();
    render(<App />);

    expect(await screen.findByText('INITIALIZING → NORMAL')).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('Filter by severity'), 'CRITICAL');
    expect(screen.queryByText('INITIALIZING → NORMAL')).not.toBeInTheDocument();
    expect(screen.getByText('No recorded events match the current filters.')).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('Filter by severity'), 'ALL');
    expect(screen.getByText('INITIALIZING → NORMAL')).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('Filter by subsystem'), 'power');
    expect(screen.queryByText('INITIALIZING → NORMAL')).not.toBeInTheDocument();
    expect(screen.getByText('No recorded events match the current filters.')).toBeInTheDocument();
  });

  it('shows a useful manifest loading error and can retry', async () => {
    const fetchMock = vi.fn().mockResolvedValue(response('', 404));
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    render(<App />);

    expect(await screen.findByRole('heading', { name: "AeroGuard couldn't load the scenario manifest" })).toBeInTheDocument();
    expect(screen.getByText('frontend/public/data/manifest.json')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Try again' }));
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(4));
  });

  it('keeps the access token in memory and loads the admin fleet workspace', async () => {
    window.localStorage.clear();
    const admin = {
      id: 1,
      username: 'admin',
      display_name: 'AeroGuard Administrator',
      is_active: true,
      roles: ['Admin'],
      created_at_utc: '2030-01-01T08:00:00Z',
      updated_at_utc: '2030-01-01T08:00:00Z',
    };
    const aircraft = [{
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
    const apiManifest = {
      ...manifestFixture,
      generated_by: 'AeroGuard REST API',
      scenarios: [{ ...manifestFixture.scenarios[0], file: '/api/runs/11/replay' }],
    };
    const fetchMock = vi.fn((input: RequestInfo | URL, _init?: RequestInit) => {
      void _init;
      const url = String(input);
      if (url === '/api/scenarios') return Promise.resolve(response(apiManifest));
      if (url === '/api/runs?limit=12') return Promise.resolve(response([{
        id: 11,
        scenario: 'normal-operation',
        label: 'Normal operation',
        description: 'All simulated subsystems remain within configured limits.',
        final_state: 'NORMAL',
        duration_ms: 1000,
        tick_ms: 100,
        record_count: 74,
        replay_url: '/api/runs/11/replay',
      }]));
      if (url === '/api/runs/11/replay') return Promise.resolve(response(jsonlFixture));
      if (url === '/api/auth/login') return Promise.resolve(response({ access_token: 'opaque-session', token_type: 'Bearer', expires_at_utc: '2030-01-01T12:00:00Z', user: admin }));
      if (url === '/api/auth/me') return Promise.resolve(response(admin));
      if (url === '/api/aircraft') return Promise.resolve(response(aircraft));
      if (url === '/api/admin/users') return Promise.resolve(response([admin]));
      if (url === '/api/admin/roles') return Promise.resolve(response([{ id: 1, name: 'Admin', description: 'Full administration access', is_system: true, user_count: 1, created_at_utc: '2030-01-01T08:00:00Z' }]));
      if (url === '/api/admin/audit?limit=50' || url === '/api/aircraft/9/assignments') return Promise.resolve(response([]));
      return Promise.resolve(response('', 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();
    render(<App />);

    expect(await screen.findByRole('heading', { name: 'System health' })).toBeInTheDocument();
    await user.type(screen.getByLabelText('Password'), 'correct-password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('heading', { name: '1 visible aircraft' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Test aircraft 01' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'People and roles' }));
    expect(screen.getByRole('heading', { name: 'Users' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Roles' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Audit log' }));
    expect(screen.getByRole('heading', { name: 'Audit log' })).toBeInTheDocument();
    expect(window.localStorage.length).toBe(0);
    const protectedCall = fetchMock.mock.calls.find(([url]) => String(url) === '/api/aircraft');
    expect(new Headers((protectedCall?.[1] as RequestInit | undefined)?.headers).get('Authorization')).toBe('Bearer opaque-session');
  });
});

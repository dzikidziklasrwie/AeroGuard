import { describe, expect, it, vi } from 'vitest';
import {
  createOperationFlight,
  getAircraft,
  getOperationPermissions,
  login,
  replaceUserOperationPermissions,
} from './client';

function response(body: unknown, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: vi.fn().mockResolvedValue(body),
  } as unknown as Response;
}

describe('API client', () => {
  it('maps a login response into an in-memory session', async () => {
    const fetchMock = vi.fn().mockResolvedValue(response({
      access_token: 'opaque-token',
      token_type: 'Bearer',
      expires_at_utc: '2030-01-01T12:00:00Z',
      user: {
        id: 1,
        username: 'admin',
        display_name: 'Administrator',
        is_active: true,
        roles: ['Admin'],
        created_at_utc: '2030-01-01T08:00:00Z',
        updated_at_utc: '2030-01-01T08:00:00Z',
      },
    }));
    vi.stubGlobal('fetch', fetchMock);

    const session = await login('admin', 'correct-password');

    expect(session).toMatchObject({ token: 'opaque-token', user: { username: 'admin' } });
    const [, options] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(options.method).toBe('POST');
    expect(options.body).toBe(JSON.stringify({ username: 'admin', password: 'correct-password' }));
  });

  it('adds the Bearer token to protected requests', async () => {
    const fetchMock = vi.fn().mockResolvedValue(response([]));
    vi.stubGlobal('fetch', fetchMock);

    await getAircraft('session-token');

    const [, options] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(new Headers(options.headers).get('Authorization')).toBe('Bearer session-token');
  });

  it('returns server problem details without exposing the response body', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({
      title: 'Invalid credentials',
      detail: 'The username or password is incorrect.',
    }, 401)));

    await expect(login('admin', 'wrong-password')).rejects.toEqual(expect.objectContaining({
      status: 401,
      message: 'The username or password is incorrect.',
    }));
  });

  it('normalizes wrapped operation permissions from the API', async () => {
    const fetchMock = vi.fn().mockResolvedValue(response({ permissions: ['FLIGHT_PLACE_OWN', 'CHAT_SEND'] }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(getOperationPermissions('session-token')).resolves.toEqual(['FLIGHT_PLACE_OWN', 'CHAT_SEND']);
    const [, options] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(new Headers(options.headers).get('Authorization')).toBe('Bearer session-token');
  });

  it('sends canonical world placement and admin permission payloads', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response({ id: 44 }))
      .mockResolvedValueOnce(response({ permissions: ['TELEMETRY_SHARE'] }));
    vi.stubGlobal('fetch', fetchMock);

    await createOperationFlight('session-token', {
      aircraft_id: 9,
      run_id: 11,
      scope_type: 'WORLD',
      scope_code: 'WORLD',
      latitude: 52.23,
      longitude: 21.01,
      speed_kph: 720,
    });
    await expect(replaceUserOperationPermissions('session-token', 2, ['TELEMETRY_SHARE']))
      .resolves.toEqual(['TELEMETRY_SHARE']);

    expect(fetchMock).toHaveBeenNthCalledWith(1, '/api/operations/flights', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({
        aircraft_id: 9,
        run_id: 11,
        scope_type: 'WORLD',
        scope_code: 'WORLD',
        latitude: 52.23,
        longitude: 21.01,
        speed_kph: 720,
      }),
    }));
    expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/admin/users/2/permissions', expect.objectContaining({
      method: 'PUT',
      body: JSON.stringify({ permissions: ['TELEMETRY_SHARE'] }),
    }));
  });
});

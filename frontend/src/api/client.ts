import type {
  AccessSession,
  AircraftAccessLevel,
  AircraftAssignment,
  AircraftInput,
  AircraftRecord,
  AuditRecord,
  ChatMessageRecord,
  ChatShareType,
  CreateUserInput,
  FlightInput,
  FlightRecord,
  OperationPermission,
  LoginResponse,
  RoleRecord,
  UserAccount,
} from './types';

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

interface RequestOptions extends Omit<RequestInit, 'body'> {
  token?: string;
  body?: unknown;
}

export class ApiError extends Error {
  readonly status: number;
  readonly fieldErrors: Record<string, string[]>;

  constructor(message: string, status: number, fieldErrors: Record<string, string[]> = {}) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

async function readProblem(response: Response): Promise<ProblemDetails> {
  try {
    return await response.json() as ProblemDetails;
  } catch {
    return {};
  }
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set('Accept', 'application/json');
  if (options.body !== undefined) headers.set('Content-Type', 'application/json');
  if (options.token) headers.set('Authorization', `Bearer ${options.token}`);

  const response = await fetch(path, {
    ...options,
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  });

  if (!response.ok) {
    const problem = await readProblem(response);
    const fieldMessage = Object.values(problem.errors ?? {}).flat()[0];
    throw new ApiError(
      problem.detail ?? fieldMessage ?? problem.title ?? `Request failed (HTTP ${response.status}).`,
      response.status,
      problem.errors,
    );
  }

  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}

export async function login(username: string, password: string): Promise<AccessSession> {
  const result = await request<LoginResponse>('/api/auth/login', {
    method: 'POST',
    body: { username, password },
  });
  return {
    token: result.access_token,
    expiresAtUtc: result.expires_at_utc,
    user: result.user,
  };
}

export const getCurrentUser = (token: string, signal?: AbortSignal) =>
  request<UserAccount>('/api/auth/me', { token, signal });

export const logout = (token: string) =>
  request<void>('/api/auth/logout', { method: 'POST', token });

export const getAircraft = (token: string, signal?: AbortSignal) =>
  request<AircraftRecord[]>('/api/aircraft', { token, signal });

export const createAircraft = (token: string, input: AircraftInput) =>
  request<AircraftRecord>('/api/aircraft', { method: 'POST', token, body: input });

export const updateAircraft = (token: string, aircraft: AircraftRecord, input: AircraftInput) =>
  request<AircraftRecord>(`/api/aircraft/${aircraft.id}`, {
    method: 'PUT',
    token,
    body: { ...input, expected_version: aircraft.version },
  });

export const deleteAircraft = (token: string, aircraftId: number) =>
  request<void>(`/api/aircraft/${aircraftId}`, { method: 'DELETE', token });

export const getAircraftAssignments = (token: string, aircraftId: number, signal?: AbortSignal) =>
  request<AircraftAssignment[]>(`/api/aircraft/${aircraftId}/assignments`, { token, signal });

export const setAircraftAssignment = (
  token: string,
  aircraftId: number,
  userId: number,
  accessLevel: AircraftAccessLevel,
) => request<AircraftAssignment>(`/api/aircraft/${aircraftId}/assignments/${userId}`, {
  method: 'PUT',
  token,
  body: { access_level: accessLevel },
});

export const removeAircraftAssignment = (token: string, aircraftId: number, userId: number) =>
  request<void>(`/api/aircraft/${aircraftId}/assignments/${userId}`, { method: 'DELETE', token });

export const getUsers = (token: string, signal?: AbortSignal) =>
  request<UserAccount[]>('/api/admin/users', { token, signal });

export const createUser = (token: string, input: CreateUserInput) =>
  request<UserAccount>('/api/admin/users', { method: 'POST', token, body: input });

export const replaceUserRoles = (token: string, userId: number, roles: string[]) =>
  request<UserAccount>(`/api/admin/users/${userId}/roles`, {
    method: 'PUT',
    token,
    body: { roles },
  });

export const setUserStatus = (token: string, userId: number, isActive: boolean) =>
  request<UserAccount>(`/api/admin/users/${userId}/status`, {
    method: 'PUT',
    token,
    body: { is_active: isActive },
  });

export const resetUserPassword = (token: string, userId: number, password: string) =>
  request<void>(`/api/admin/users/${userId}/password`, {
    method: 'PUT',
    token,
    body: { password },
  });

export const getRoles = (token: string, signal?: AbortSignal) =>
  request<RoleRecord[]>('/api/admin/roles', { token, signal });

export const createRole = (token: string, name: string, description: string) =>
  request<RoleRecord>('/api/admin/roles', { method: 'POST', token, body: { name, description } });

export const deleteRole = (token: string, roleId: number) =>
  request<void>(`/api/admin/roles/${roleId}`, { method: 'DELETE', token });

export const getAudit = (token: string, signal?: AbortSignal) =>
  request<AuditRecord[]>('/api/admin/audit?limit=50', { token, signal });

export const getOperationFlights = (token: string, signal?: AbortSignal) =>
  request<FlightRecord[]>('/api/operations/flights', { token, signal });

export const createOperationFlight = (token: string, input: FlightInput) =>
  request<FlightRecord>('/api/operations/flights', { method: 'POST', token, body: input });

export const updateOperationFlight = (token: string, flight: FlightRecord, input: FlightInput) =>
  request<FlightRecord>(`/api/operations/flights/${flight.id}`, {
    method: 'PUT',
    token,
    body: { ...input, expected_version: flight.version },
  });

export const deleteOperationFlight = (token: string, flightId: number) =>
  request<void>(`/api/operations/flights/${flightId}`, { method: 'DELETE', token });

export const getOperationMessages = (token: string, flightId: number, signal?: AbortSignal) =>
  request<ChatMessageRecord[]>(`/api/operations/flights/${flightId}/messages?limit=100`, { token, signal });

export const sendOperationMessage = (
  token: string,
  flightId: number,
  message: string,
  shareType: ChatShareType,
) => request<ChatMessageRecord>(`/api/operations/flights/${flightId}/messages`, {
  method: 'POST',
  token,
  body: { message, share_type: shareType },
});

type PermissionResponse = OperationPermission[] | { permissions: OperationPermission[] };

function permissionList(response: PermissionResponse): OperationPermission[] {
  return Array.isArray(response) ? response : response.permissions;
}

export async function getOperationPermissions(token: string, signal?: AbortSignal) {
  return permissionList(await request<PermissionResponse>('/api/operations/permissions', { token, signal }));
}

export async function getUserOperationPermissions(token: string, userId: number, signal?: AbortSignal) {
  return permissionList(await request<PermissionResponse>(`/api/admin/users/${userId}/permissions`, { token, signal }));
}

export async function replaceUserOperationPermissions(
  token: string,
  userId: number,
  permissions: OperationPermission[],
) {
  return permissionList(await request<PermissionResponse>(`/api/admin/users/${userId}/permissions`, {
    method: 'PUT',
    token,
    body: { permissions },
  }));
}

export interface UserAccount {
  id: number;
  username: string;
  display_name: string;
  is_active: boolean;
  roles: string[];
  created_at_utc: string;
  updated_at_utc: string;
}

export interface LoginResponse {
  access_token: string;
  token_type: string;
  expires_at_utc: string;
  user: UserAccount;
}

export interface AccessSession {
  token: string;
  expiresAtUtc: string;
  user: UserAccount;
}

export interface RoleRecord {
  id: number;
  name: string;
  description: string;
  is_system: boolean;
  user_count: number;
  created_at_utc: string;
}

export const AIRCRAFT_STATUSES = ['ACTIVE', 'MAINTENANCE', 'GROUNDED'] as const;
export type AircraftStatus = (typeof AIRCRAFT_STATUSES)[number];

export const ACCESS_LEVELS = ['OWNER', 'MAINTAINER', 'VIEWER'] as const;
export type AircraftAccessLevel = (typeof ACCESS_LEVELS)[number];

export interface AircraftRecord {
  id: number;
  tail_number: string;
  display_name: string;
  model: string;
  status: AircraftStatus;
  notes: string;
  version: number;
  created_by: number;
  created_at_utc: string;
  updated_at_utc: string;
  current_user_access: AircraftAccessLevel | null;
}

export interface AircraftAssignment {
  aircraft_id: number;
  user_id: number;
  username: string;
  display_name: string;
  access_level: AircraftAccessLevel;
  assigned_by: number;
  assigned_at_utc: string;
}

export interface AuditRecord {
  id: number;
  actor_user_id: number | null;
  actor_username: string | null;
  action: string;
  entity_type: string;
  entity_id: string;
  details: unknown;
  occurred_at_utc: string;
}

export interface CreateUserInput {
  username: string;
  display_name: string;
  password: string;
  roles: string[];
}

export interface AircraftInput {
  tail_number: string;
  display_name: string;
  model: string;
  status: AircraftStatus;
  notes: string;
}

export const FLIGHT_SCOPE_TYPES = ['WORLD', 'CONTINENT', 'COUNTRY'] as const;
export type FlightScopeType = (typeof FLIGHT_SCOPE_TYPES)[number];

export const FLIGHT_CONDITIONS = ['HEALTHY', 'DEGRADED', 'CRITICAL', 'UNKNOWN'] as const;
export type FlightCondition = (typeof FLIGHT_CONDITIONS)[number];

export const OPERATION_PERMISSIONS = [
  'FLIGHT_PLACE_OWN',
  'FLIGHT_REMOVE_OWN',
  'FLIGHT_RECONFIGURE_OWN',
  'CHAT_SEND',
  'TELEMETRY_SHARE',
] as const;
export type OperationPermission = (typeof OPERATION_PERMISSIONS)[number];

export const OPERATION_PERMISSION_LABELS: Record<OperationPermission, string> = {
  FLIGHT_PLACE_OWN: 'Place owned aircraft',
  FLIGHT_REMOVE_OWN: 'Remove owned aircraft',
  FLIGHT_RECONFIGURE_OWN: 'Move owned aircraft',
  CHAT_SEND: 'Send flight messages',
  TELEMETRY_SHARE: 'Share telemetry',
};

export const CHAT_SHARE_TYPES = ['TEXT', 'LOCATION', 'TELEMETRY'] as const;
export type ChatShareType = (typeof CHAT_SHARE_TYPES)[number];

export interface FlightTelemetry {
  subsystem: string;
  health: string;
  valid: boolean;
  reason: string | null;
  data: Record<string, unknown>;
}

export interface FlightRecord {
  id: number;
  aircraft_id: number;
  tail_number: string;
  display_name: string;
  owner_user_id: number;
  owner_username: string;
  owner_display_name: string;
  current_user_access: AircraftAccessLevel | null;
  scope_type: FlightScopeType;
  scope_code: string;
  latitude: number;
  longitude: number;
  heading_deg: number;
  speed_kph: number;
  run_id: number;
  scenario: string | null;
  scenario_label: string | null;
  scenario_elapsed_ms: number;
  scenario_duration_ms: number;
  condition: FlightCondition;
  telemetry: FlightTelemetry[];
  version: number;
  started_at_utc: string;
  updated_at_utc: string;
  can_manage: boolean;
  can_remove: boolean;
  event_sequence?: number;
}

export interface FlightInput {
  aircraft_id: number;
  run_id: number;
  scope_type: FlightScopeType;
  scope_code: string;
  latitude: number;
  longitude: number;
  speed_kph: number;
}

export interface ChatMessageRecord {
  id: number;
  flight_id: number;
  sender_user_id: number;
  sender_username: string;
  sender_display_name: string;
  message: string;
  share_type: ChatShareType;
  payload: unknown;
  sent_at_utc: string;
}

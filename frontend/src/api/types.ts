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

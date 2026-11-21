import { lazy, memo, Suspense, type FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import {
  createAircraft,
  createRole,
  createUser,
  deleteAircraft,
  deleteRole,
  getAircraft,
  getAircraftAssignments,
  getAudit,
  getCurrentUser,
  getRoles,
  getUserOperationPermissions,
  getUsers,
  login,
  logout,
  removeAircraftAssignment,
  replaceUserOperationPermissions,
  replaceUserRoles,
  resetUserPassword,
  setAircraftAssignment,
  setUserStatus,
  updateAircraft,
  ApiError,
} from '../api/client';
import {
  AIRCRAFT_STATUSES,
  type AccessSession,
  type AircraftAccessLevel,
  type AircraftInput,
  type AircraftRecord,
  type AuditRecord,
  OPERATION_PERMISSION_LABELS,
  OPERATION_PERMISSIONS,
  type OperationPermission,
  type RoleRecord,
  type UserAccount,
} from '../api/types';
import { titleCase } from '../utils/format';
import type { RunArchiveEntry } from '../types';

const OperationsMap = lazy(async () => {
  const module = await import('./OperationsMap');
  return { default: module.OperationsMap };
});

type WorkspaceView = 'map' | 'fleet' | 'accounts' | 'audit';

function hasRole(user: UserAccount, role: string): boolean {
  return user.roles.some((candidate) => candidate.toLowerCase() === role.toLowerCase());
}

const fleetRoleOptions: ReadonlyArray<{ value: AircraftAccessLevel; label: string }> = [
  { value: 'OWNER', label: 'Operator' },
  { value: 'MAINTAINER', label: 'Maintainer' },
  { value: 'VIEWER', label: 'Viewer' },
];

function suggestedFleetAccess(user: UserAccount): AircraftAccessLevel {
  if (hasRole(user, 'Operator')) return 'OWNER';
  if (hasRole(user, 'Maintainer')) return 'MAINTAINER';
  return 'VIEWER';
}

function fleetRoleLabel(access: AircraftAccessLevel): string {
  return fleetRoleOptions.find((option) => option.value === access)?.label ?? titleCase(access);
}

function readableError(error: unknown): string {
  if (error instanceof ApiError || error instanceof Error) return error.message;
  return 'The request failed. Try again.';
}

function validateNewAccount(username: string, displayName: string, password: string): string | null {
  const normalizedUsername = username.trim();
  if (normalizedUsername.length < 3 || normalizedUsername.length > 64
    || !/^[A-Za-z0-9]/.test(normalizedUsername)
    || !/^[A-Za-z0-9._-]+$/.test(normalizedUsername)) {
    return 'Username: use 3–64 ASCII letters, digits, dots, underscores or hyphens; start with a letter or digit.';
  }
  if (displayName.trim().length < 1 || displayName.trim().length > 100) {
    return 'Display name: use 1–100 characters.';
  }
  if (password.length < 12 || password.length > 128) {
    return 'Password: use 12–128 characters.';
  }
  const categories = [
    /[a-z]/.test(password),
    /[A-Z]/.test(password),
    /\d/.test(password),
    /[^A-Za-z0-9]/.test(password),
  ].filter(Boolean).length;
  if (categories < 3) {
    return 'Password: use characters from at least three groups: lowercase, uppercase, digits and symbols.';
  }
  return null;
}

function LoginPanel({ onAuthenticated }: { onAuthenticated: (session: AccessSession) => void }) {
  const [username, setUsername] = useState('admin');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError('');
    try {
      const issued = await login(username, password);
      const currentUser = await getCurrentUser(issued.token);
      onAuthenticated({ ...issued, user: currentUser });
    } catch (requestError) {
      setError(readableError(requestError));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="workspace-login-grid">
      <div className="workspace-intro">
        <p className="section-kicker">Controlled access</p>
        <h3>Sign in to manage the fleet</h3>
        <p>Accounts, roles, aircraft assignments and audit records come from the ASP.NET Core API.</p>
        <ul>
          <li>Bearer sessions can be revoked by the server.</li>
          <li>The browser keeps the token in memory only.</li>
          <li>Admin actions create an audit record.</li>
        </ul>
      </div>
      <form className="workspace-login" onSubmit={submit}>
        <div>
          <p className="section-kicker">Account session</p>
          <h3>Access the operations workspace</h3>
        </div>
        <label>
          <span>Username</span>
          <input
            name="username"
            autoComplete="username"
            value={username}
            onChange={(event) => setUsername(event.target.value)}
            required
          />
        </label>
        <label>
          <span>Password</span>
          <input
            name="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            required
          />
        </label>
        {error && <p className="form-error" role="alert">{error}</p>}
        <button className="button primary-button" type="submit" disabled={busy}>
          {busy ? 'Signing in...' : 'Sign in'}
        </button>
        <p className="session-note">The token clears when this page closes or reloads.</p>
      </form>
    </div>
  );
}

interface AircraftFormProps {
  heading: string;
  initial?: AircraftRecord;
  submitLabel: string;
  busy: boolean;
  onSubmit: (input: AircraftInput) => Promise<void>;
  onCancel?: () => void;
}

function AircraftForm({ heading, initial, submitLabel, busy, onSubmit, onCancel }: AircraftFormProps) {
  const [tailNumber, setTailNumber] = useState(initial?.tail_number ?? '');
  const [displayName, setDisplayName] = useState(initial?.display_name ?? '');
  const [model, setModel] = useState(initial?.model ?? '');
  const [status, setStatus] = useState<AircraftInput['status']>(initial?.status ?? 'ACTIVE');
  const [notes, setNotes] = useState(initial?.notes ?? '');

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await onSubmit({ tail_number: tailNumber, display_name: displayName, model, status, notes });
    if (!initial) {
      setTailNumber('');
      setDisplayName('');
      setModel('');
      setStatus('ACTIVE');
      setNotes('');
    }
  }

  return (
    <form className="entity-form aircraft-form" onSubmit={submit}>
      <div className="entity-form-heading"><h4>{heading}</h4>{onCancel && <button type="button" className="text-button" onClick={onCancel}>Close</button>}</div>
      <div className="form-grid">
        <label><span>Tail number</span><input value={tailNumber} onChange={(event) => setTailNumber(event.target.value.toUpperCase())} placeholder="N204AG" required /></label>
        <label><span>Display name</span><input value={displayName} onChange={(event) => setDisplayName(event.target.value)} placeholder="Test aircraft 01" required /></label>
        <label><span>Model</span><input value={model} onChange={(event) => setModel(event.target.value)} placeholder="Research platform" required /></label>
        <label><span>Status</span><select value={status} onChange={(event) => setStatus(event.target.value as AircraftInput['status'])}>{AIRCRAFT_STATUSES.map((value) => <option value={value} key={value}>{titleCase(value)}</option>)}</select></label>
      </div>
      <label><span>Notes</span><textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} placeholder="Maintenance or assignment notes" /></label>
      <button className="button primary-button" type="submit" disabled={busy}>{submitLabel}</button>
    </form>
  );
}

interface FleetPanelProps {
  token: string;
  currentUser: UserAccount;
  aircraft: AircraftRecord[];
  users: UserAccount[];
  busy: boolean;
  runAction: (successMessage: string, action: () => Promise<unknown>) => Promise<boolean>;
  refreshFleet: () => Promise<void>;
}

function FleetPanel({ token, currentUser, aircraft, users, busy, runAction, refreshFleet }: FleetPanelProps) {
  const isAdmin = hasRole(currentUser, 'Admin');
  const canCreate = isAdmin || hasRole(currentUser, 'Operator');
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [assignments, setAssignments] = useState<Awaited<ReturnType<typeof getAircraftAssignments>>>([]);
  const [assignmentUserId, setAssignmentUserId] = useState('');
  const [assignmentAccess, setAssignmentAccess] = useState<AircraftAccessLevel>('VIEWER');
  const selected = aircraft.find((item) => item.id === selectedId) ?? aircraft[0];

  const loadAssignments = useCallback(async (signal?: AbortSignal) => {
    if (!selected || !isAdmin) {
      return;
    }
    try {
      setAssignments(await getAircraftAssignments(token, selected.id, signal));
    } catch (error) {
      if (!(error instanceof DOMException && error.name === 'AbortError')) setAssignments([]);
    }
  }, [isAdmin, selected, token]);

  useEffect(() => {
    if (!selected || !isAdmin) return;
    const controller = new AbortController();
    getAircraftAssignments(token, selected.id, controller.signal)
      .then(setAssignments)
      .catch((error: unknown) => {
        if (!(error instanceof DOMException && error.name === 'AbortError')) setAssignments([]);
      });
    return () => controller.abort();
  }, [isAdmin, selected, token]);

  const canEdit = selected && (isAdmin || selected.current_user_access === 'OWNER' || selected.current_user_access === 'MAINTAINER');

  async function mutateWithResult(successMessage: string, action: () => Promise<unknown>) {
    const succeeded = await runAction(successMessage, action);
    if (succeeded) {
      await refreshFleet();
      await loadAssignments();
    }
    return succeeded;
  }

  async function mutate(successMessage: string, action: () => Promise<unknown>) {
    await mutateWithResult(successMessage, action);
  }

  return (
    <div className="fleet-layout">
      <div className="fleet-list-column">
        <div className="workspace-section-heading">
          <div><p className="section-kicker">Aircraft registry</p><h3>{aircraft.length} visible aircraft</h3></div>
          {canCreate && <button className="button secondary-button" type="button" onClick={() => setShowCreate((value) => !value)}>{showCreate ? 'Close form' : 'Add aircraft'}</button>}
        </div>
        {showCreate && canCreate && <AircraftForm
          heading="Register aircraft"
          submitLabel="Create aircraft"
          busy={busy}
          onCancel={() => setShowCreate(false)}
          onSubmit={async (input) => mutate('Aircraft added to the registry.', () => createAircraft(token, input))}
        />}
        <div className="aircraft-list" role="list" aria-label="Aircraft registry">
          {aircraft.length ? aircraft.map((item) => (
            <button
              type="button"
              role="listitem"
              key={item.id}
              className={`aircraft-row${selected?.id === item.id ? ' active' : ''}`}
              onClick={() => setSelectedId(item.id)}
            >
              <span className="tail-mark">{item.tail_number}</span>
              <span><strong>{item.display_name}</strong><small>{item.model}</small></span>
              <span className={`aircraft-status status-${item.status.toLowerCase()}`}>{titleCase(item.status)}</span>
            </button>
          )) : <div className="empty-entity"><strong>No aircraft assigned</strong><p>An administrator or operator can create the first registry entry.</p></div>}
        </div>
      </div>

      <div className="fleet-detail-column">
        {selected ? (
          <>
            <div className="aircraft-detail-card">
              <div className="aircraft-detail-head">
                <div><span>{selected.tail_number}</span><h3>{selected.display_name}</h3><p>{selected.model}</p></div>
                <span className={`aircraft-status status-${selected.status.toLowerCase()}`}>{titleCase(selected.status)}</span>
              </div>
              <dl className="detail-facts">
                <div><dt>Your access</dt><dd>{isAdmin ? 'Administrator' : titleCase(selected.current_user_access ?? 'VIEWER')}</dd></div>
                <div><dt>Revision</dt><dd>{selected.version}</dd></div>
                <div><dt>Updated</dt><dd>{new Date(selected.updated_at_utc).toLocaleDateString('en-GB')}</dd></div>
              </dl>
              {selected.notes && <p className="aircraft-notes">{selected.notes}</p>}
            </div>
            {canEdit && <AircraftForm
              key={`${selected.id}-${selected.version}`}
              heading="Edit aircraft"
              initial={selected}
              submitLabel="Save aircraft"
              busy={busy}
              onSubmit={async (input) => mutate('Aircraft details saved.', () => updateAircraft(token, selected, input))}
            />}
            {isAdmin && <div className="danger-row"><p>Deleting an aircraft also removes its account assignments.</p><button className="button danger-button" type="button" disabled={busy} onClick={async () => {
              if (window.confirm(`Delete ${selected.tail_number} from the registry?`)) {
                await mutate('Aircraft removed from the registry.', () => deleteAircraft(token, selected.id));
              }
            }}>Delete aircraft</button></div>}

            {isAdmin && <section className="assignment-panel" aria-labelledby="assignment-title">
              <div className="workspace-section-heading"><div><p className="section-kicker">Access list</p><h3 id="assignment-title">Aircraft assignments</h3></div><span>{assignments.length} accounts</span></div>
              <form className="assignment-form" onSubmit={async (event) => {
                event.preventDefault();
                const userId = Number(assignmentUserId);
                if (!userId) return;
                if (await mutateWithResult('Aircraft assignment saved.', () => setAircraftAssignment(token, selected.id, userId, assignmentAccess))) {
                  setAssignmentUserId('');
                }
              }}>
                <label><span>Account</span><select value={assignmentUserId} onChange={(event) => {
                  const userId = Number(event.target.value);
                  setAssignmentUserId(event.target.value);
                  const account = users.find((user) => user.id === userId);
                  if (account) setAssignmentAccess(suggestedFleetAccess(account));
                }} required><option value="">Select account</option>{users.filter((user) => user.is_active).map((user) => <option key={user.id} value={user.id}>{user.display_name} ({user.username}) - {user.roles.join(', ') || 'No account role'}</option>)}</select></label>
                <label><span>Fleet role</span><select value={assignmentAccess} onChange={(event) => setAssignmentAccess(event.target.value as AircraftAccessLevel)} aria-describedby="aircraft-access-help">{fleetRoleOptions.map((option) => <option value={option.value} key={option.value}>{option.label}</option>)}</select></label>
                <button className="button secondary-button" type="submit" disabled={busy || !assignmentUserId}>Assign aircraft</button>
              </form>
              <p className="assignment-access-help" id="aircraft-access-help">
                The Fleet role follows People and roles: Operator manages and operates the aircraft,
                Maintainer can manage it, and Viewer has read-only access. Selecting an account proposes
                the matching role automatically; an administrator can change it before assignment.
              </p>
              <div className="assignment-list">
                {assignments.map((assignment) => <div key={assignment.user_id}><span><strong>{assignment.display_name}</strong><small>@{assignment.username}</small></span><span>{fleetRoleLabel(assignment.access_level)}</span><button className="text-button danger-text" type="button" disabled={busy} onClick={async () => mutate('Aircraft assignment removed.', () => removeAircraftAssignment(token, selected.id, assignment.user_id))}>Remove</button></div>)}
                {!assignments.length && <p className="empty-copy">No accounts are assigned to this aircraft.</p>}
              </div>
            </section>}
          </>
        ) : <div className="empty-entity"><strong>Select an aircraft</strong><p>Aircraft details and access assignments appear here.</p></div>}
      </div>
    </div>
  );
}

interface UserCardProps {
  user: UserAccount;
  roles: RoleRecord[];
  currentUserId: number;
  busy: boolean;
  operationPermissions: OperationPermission[];
  onRoles: (user: UserAccount, roles: string[]) => Promise<void>;
  onPermissions: (user: UserAccount, permissions: OperationPermission[]) => Promise<void>;
  onStatus: (user: UserAccount) => Promise<void>;
  onPassword: (user: UserAccount, password: string) => Promise<void>;
}

function UserCard({ user, roles, currentUserId, busy, operationPermissions, onRoles, onPermissions, onStatus, onPassword }: UserCardProps) {
  const [selectedRoles, setSelectedRoles] = useState(user.roles);
  const [selectedPermissions, setSelectedPermissions] = useState(operationPermissions);
  const [password, setPassword] = useState('');

  return (
    <article className={`user-card${user.is_active ? '' : ' inactive'}`}>
      <div className="user-card-heading"><span className="user-avatar">{user.display_name.slice(0, 2).toUpperCase()}</span><div><h4>{user.display_name}</h4><p>@{user.username}{user.id === currentUserId ? ' - current session' : ''}</p></div><span className={`account-state ${user.is_active ? 'active' : ''}`}>{user.is_active ? 'Active' : 'Inactive'}</span></div>
      <fieldset className="role-checks"><legend>Assigned roles</legend>{roles.map((role) => <label key={role.id}><input type="checkbox" checked={selectedRoles.includes(role.name)} onChange={(event) => setSelectedRoles((current) => event.target.checked ? [...current, role.name] : current.filter((name) => name !== role.name))} /><span>{role.name}</span></label>)}</fieldset>
      <fieldset className="role-checks permission-checks"><legend>Operations permissions</legend>{OPERATION_PERMISSIONS.map((permission) => <label key={permission}><input type="checkbox" checked={selectedPermissions.includes(permission)} onChange={(event) => setSelectedPermissions((current) => event.target.checked ? [...current, permission] : current.filter((name) => name !== permission))} /><span>{OPERATION_PERMISSION_LABELS[permission]}</span></label>)}</fieldset>
      {user.roles.some((role) => role.toLowerCase() === 'admin') && <p className="permission-note">The Admin role grants every operations permission.</p>}
      <div className="user-actions"><button className="button secondary-button" type="button" disabled={busy || selectedRoles.length === 0} onClick={() => onRoles(user, selectedRoles)}>Save roles</button><button className="button secondary-button" type="button" disabled={busy} onClick={() => onPermissions(user, selectedPermissions)}>Save map access</button><button className="text-button" type="button" disabled={busy || user.id === currentUserId} onClick={() => onStatus(user)}>{user.is_active ? 'Deactivate' : 'Activate'}</button></div>
      <form className="password-reset" onSubmit={async (event) => { event.preventDefault(); await onPassword(user, password); setPassword(''); }}><label><span>New password</span><input type="password" autoComplete="new-password" value={password} onChange={(event) => setPassword(event.target.value)} minLength={12} placeholder="12 or more characters" required /></label><button className="text-button" type="submit" disabled={busy}>Reset password</button></form>
    </article>
  );
}

interface AccountsPanelProps {
  token: string;
  currentUser: UserAccount;
  users: UserAccount[];
  roles: RoleRecord[];
  busy: boolean;
  runAction: (message: string, action: () => Promise<unknown>) => Promise<boolean>;
  refreshAccounts: () => Promise<void>;
}

function AccountsPanel({ token, currentUser, users, roles, busy, runAction, refreshAccounts }: AccountsPanelProps) {
  const [username, setUsername] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [createUserError, setCreateUserError] = useState('');
  const [newUserRoles, setNewUserRoles] = useState<string[]>(['Viewer']);
  const [roleName, setRoleName] = useState('');
  const [roleDescription, setRoleDescription] = useState('');
  const [operationPermissions, setOperationPermissions] = useState<Record<number, OperationPermission[]>>({});
  const [permissionLoadFailed, setPermissionLoadFailed] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    let failed = false;
    Promise.all(users.map(async (user) => {
      try {
        return [user.id, await getUserOperationPermissions(token, user.id, controller.signal)] as const;
      } catch (error) {
        if (!(error instanceof DOMException && error.name === 'AbortError')) failed = true;
        return [user.id, [] as OperationPermission[]] as const;
      }
    })).then((records) => {
      if (!active) return;
      setPermissionLoadFailed(failed);
      setOperationPermissions(Object.fromEntries(records));
    });
    return () => {
      active = false;
      controller.abort();
    };
  }, [token, users]);

  async function mutate(message: string, action: () => Promise<unknown>) {
    if (await runAction(message, action)) await refreshAccounts();
  }

  return (
    <div className="accounts-layout">
      <section className="account-column" aria-labelledby="users-title">
        <div className="workspace-section-heading"><div><p className="section-kicker">Account directory</p><h3 id="users-title">Users</h3></div><span>{users.length} accounts</span></div>
        <form className="entity-form create-user-form" onSubmit={async (event) => {
          event.preventDefault();
          const validationError = validateNewAccount(username, displayName, password);
          if (validationError) {
            setCreateUserError(validationError);
            return;
          }
          setCreateUserError('');
          const saved = await runAction('User account created.', () => createUser(token, { username, display_name: displayName, password, roles: newUserRoles }));
          if (saved) {
            setUsername(''); setDisplayName(''); setPassword(''); setNewUserRoles(['Viewer']);
            await refreshAccounts();
          }
        }}>
          <h4>Create account</h4>
          {createUserError && <p className="workspace-message error" role="alert">{createUserError}</p>}
          <div className="form-grid"><label><span>Username</span><input value={username} onChange={(event) => { setUsername(event.target.value); setCreateUserError(''); }} minLength={3} maxLength={64} pattern="[A-Za-z0-9][A-Za-z0-9._-]*" placeholder="operator1" autoComplete="username" aria-describedby="account-username-help" required /><small id="account-username-help">3–64 characters; ASCII letters, digits, dots, underscores or hyphens.</small></label><label><span>Display name</span><input value={displayName} onChange={(event) => { setDisplayName(event.target.value); setCreateUserError(''); }} maxLength={100} placeholder="Flight Operator" autoComplete="name" required /></label><label><span>Initial password</span><input type="password" autoComplete="new-password" minLength={12} maxLength={128} value={password} onChange={(event) => { setPassword(event.target.value); setCreateUserError(''); }} aria-describedby="account-password-help" required /><small id="account-password-help">12–128 characters; include at least 3 of: lowercase, uppercase, digits, symbols.</small></label></div>
          <fieldset className="role-checks"><legend>Initial account roles</legend>{roles.map((role) => <label key={role.id} title={role.description}><input type="checkbox" checked={newUserRoles.includes(role.name)} onChange={(event) => setNewUserRoles((current) => event.target.checked ? [...current, role.name] : current.filter((name) => name !== role.name))} /><span>{role.name}</span></label>)}</fieldset>
          <button className="button primary-button" type="submit" disabled={busy || newUserRoles.length === 0}>Create account</button>
        </form>
        {permissionLoadFailed && <p className="workspace-message error" role="status">Some map permissions couldn't be loaded.</p>}
        <div className="user-grid">{users.map((user) => <UserCard
          key={`${user.id}-${user.updated_at_utc}-${(operationPermissions[user.id] ?? []).join('.')}`}
          user={user}
          roles={roles}
          currentUserId={currentUser.id}
          busy={busy}
          operationPermissions={operationPermissions[user.id] ?? []}
          onRoles={async (target, selectedRoles) => mutate('User roles saved.', () => replaceUserRoles(token, target.id, selectedRoles))}
          onPermissions={async (target, selectedPermissions) => {
            const saved = await runAction('Map permissions saved.', () => replaceUserOperationPermissions(token, target.id, selectedPermissions));
            if (saved) setOperationPermissions((current) => ({ ...current, [target.id]: selectedPermissions }));
          }}
          onStatus={async (target) => mutate(`Account ${target.is_active ? 'deactivated' : 'activated'}.`, () => setUserStatus(token, target.id, !target.is_active))}
          onPassword={async (target, value) => mutate('Password reset and existing sessions revoked.', () => resetUserPassword(token, target.id, value))}
        />)}</div>
      </section>

      <aside className="role-column" aria-labelledby="roles-title">
        <div className="workspace-section-heading"><div><p className="section-kicker">Policy catalogue</p><h3 id="roles-title">Roles</h3></div></div>
        <form className="entity-form role-form" onSubmit={async (event) => {
          event.preventDefault();
          const saved = await runAction('Role created.', () => createRole(token, roleName, roleDescription));
          if (saved) { setRoleName(''); setRoleDescription(''); await refreshAccounts(); }
        }}><label><span>Role name</span><input value={roleName} onChange={(event) => setRoleName(event.target.value)} required /></label><label><span>Description</span><textarea rows={2} value={roleDescription} onChange={(event) => setRoleDescription(event.target.value)} /></label><button className="button secondary-button" type="submit" disabled={busy}>Add role</button></form>
        <div className="role-list">{roles.map((role) => <article key={role.id}><div><strong>{role.name}</strong><span>{role.is_system ? 'Built-in' : 'Custom'}</span></div><p>{role.description || 'No description supplied.'}</p><footer><span>{role.user_count} users</span>{!role.is_system && <button type="button" className="text-button danger-text" disabled={busy || role.user_count > 0} onClick={async () => mutate('Role deleted.', () => deleteRole(token, role.id))}>Delete</button>}</footer></article>)}</div>
      </aside>
    </div>
  );
}

function AuditPanel({ records }: { records: AuditRecord[] }) {
  return (
    <section className="audit-panel" aria-labelledby="audit-title">
      <div className="workspace-section-heading"><div><p className="section-kicker">Recorded changes</p><h3 id="audit-title">Audit log</h3></div><span>Latest {records.length}</span></div>
      <div className="audit-table-wrap"><table><thead><tr><th>Time</th><th>Actor</th><th>Action</th><th>Entity</th></tr></thead><tbody>{records.map((record) => <tr key={record.id}><td><time dateTime={record.occurred_at_utc}>{new Date(record.occurred_at_utc).toLocaleString('en-GB')}</time></td><td>{record.actor_username ?? 'System'}</td><td><code>{record.action}</code></td><td>{record.entity_type} #{record.entity_id}</td></tr>)}</tbody></table>{!records.length && <p className="empty-copy">No audit records are available.</p>}</div>
    </section>
  );
}

function AuthenticatedWorkspace({ session, runs, onSignedOut }: { session: AccessSession; runs: RunArchiveEntry[]; onSignedOut: () => void }) {
  const [currentUser, setCurrentUser] = useState(session.user);
  const [aircraft, setAircraft] = useState<AircraftRecord[]>([]);
  const [users, setUsers] = useState<UserAccount[]>([]);
  const [roles, setRoles] = useState<RoleRecord[]>([]);
  const [audit, setAudit] = useState<AuditRecord[]>([]);
  const [view, setView] = useState<WorkspaceView>('map');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const isAdmin = hasRole(currentUser, 'Admin');

  const handleRequestError = useCallback((requestError: unknown) => {
    setError(readableError(requestError));
  }, []);

  const refreshFleet = useCallback(async (signal?: AbortSignal) => {
    setAircraft(await getAircraft(session.token, signal));
  }, [session.token]);

  const refreshAccounts = useCallback(async (signal?: AbortSignal) => {
    if (!isAdmin) return;
    const [nextUsers, nextRoles] = await Promise.all([
      getUsers(session.token, signal),
      getRoles(session.token, signal),
    ]);
    setUsers(nextUsers);
    setRoles(nextRoles);
  }, [isAdmin, session.token]);

  const refreshAudit = useCallback(async (signal?: AbortSignal) => {
    if (isAdmin) setAudit(await getAudit(session.token, signal));
  }, [isAdmin, session.token]);

  useEffect(() => {
    const controller = new AbortController();
    Promise.all([
      getCurrentUser(session.token, controller.signal),
      getAircraft(session.token, controller.signal),
      isAdmin ? getUsers(session.token, controller.signal) : Promise.resolve([]),
      isAdmin ? getRoles(session.token, controller.signal) : Promise.resolve([]),
      isAdmin ? getAudit(session.token, controller.signal) : Promise.resolve([]),
    ]).then(([nextUser, nextAircraft, nextUsers, nextRoles, nextAudit]) => {
      setCurrentUser(nextUser);
      setAircraft(nextAircraft);
      setUsers(nextUsers);
      setRoles(nextRoles);
      setAudit(nextAudit);
    }).catch((requestError: unknown) => {
      if (requestError instanceof DOMException && requestError.name === 'AbortError') return;
      if (requestError instanceof ApiError && requestError.status === 401) {
        onSignedOut();
        return;
      }
      handleRequestError(requestError);
    }).finally(() => setLoading(false));
    return () => controller.abort();
  }, [handleRequestError, isAdmin, onSignedOut, session.token]);

  const runAction = useCallback(async (successMessage: string, action: () => Promise<unknown>) => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await action();
      setNotice(successMessage);
      return true;
    } catch (requestError) {
      handleRequestError(requestError);
      return false;
    } finally {
      setBusy(false);
    }
  }, [handleRequestError]);

  return (
    <div className="authenticated-workspace">
      <header className="workspace-session-bar">
        <div className="session-identity"><span className="user-avatar">{currentUser.display_name.slice(0, 2).toUpperCase()}</span><div><strong>{currentUser.display_name}</strong><span>@{currentUser.username} - {currentUser.roles.join(', ')}</span></div></div>
        <div><span className="session-expiry">Session ends {new Date(session.expiresAtUtc).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}</span><button className="button secondary-button" type="button" onClick={async () => { try { await logout(session.token); } finally { onSignedOut(); } }}>Sign out</button></div>
      </header>
      <nav className="workspace-tabs" aria-label="Operations workspace views">
        <button type="button" className={view === 'map' ? 'active' : ''} aria-pressed={view === 'map'} onClick={() => setView('map')}>Operations map</button>
        <button type="button" className={view === 'fleet' ? 'active' : ''} aria-pressed={view === 'fleet'} onClick={() => setView('fleet')}>Fleet</button>
        {isAdmin && <button type="button" className={view === 'accounts' ? 'active' : ''} aria-pressed={view === 'accounts'} onClick={() => setView('accounts')}>People and roles</button>}
        {isAdmin && <button type="button" className={view === 'audit' ? 'active' : ''} aria-pressed={view === 'audit'} onClick={async () => { setView('audit'); try { await refreshAudit(); } catch (requestError) { handleRequestError(requestError); } }}>Audit log</button>}
      </nav>
      {error && <p className="workspace-message error" role="alert">{error}</p>}
      {notice && <p className="workspace-message success" role="status">{notice}</p>}
      {loading ? <div className="workspace-loading" role="status"><span className="loader" />Loading account and fleet records...</div> : (
        <>
          {view === 'map' && <Suspense fallback={<div className="operations-map-loading" role="status"><span className="loader" />Loading map module...</div>}><OperationsMap token={session.token} currentUser={currentUser} aircraft={aircraft} runs={runs} onUnauthorized={onSignedOut} /></Suspense>}
          {view === 'fleet' && <FleetPanel token={session.token} currentUser={currentUser} aircraft={aircraft} users={users} busy={busy} runAction={runAction} refreshFleet={async () => { try { await refreshFleet(); } catch (requestError) { handleRequestError(requestError); } }} />}
          {view === 'accounts' && isAdmin && <AccountsPanel token={session.token} currentUser={currentUser} users={users} roles={roles} busy={busy} runAction={runAction} refreshAccounts={async () => { try { await refreshAccounts(); } catch (requestError) { handleRequestError(requestError); } }} />}
          {view === 'audit' && isAdmin && <AuditPanel records={audit} />}
        </>
      )}
    </div>
  );
}

export const OperationsWorkspace = memo(function OperationsWorkspace({ runs = [] }: { runs?: RunArchiveEntry[] }) {
  const [session, setSession] = useState<AccessSession | null>(null);
  const signedOut = useCallback(() => setSession(null), []);
  const status = useMemo(() => session ? `Signed in as ${session.user.username}` : 'Sign-in required', [session]);

  return (
    <section className="workspace-section" id="workspace" aria-labelledby="workspace-title">
      <div className="section-heading-row workspace-heading">
        <div><p className="section-kicker">Identity and fleet API</p><h2 id="workspace-title">Operations workspace</h2></div>
        <p>{status}. Public telemetry replay remains available without an account.</p>
      </div>
      {session ? <AuthenticatedWorkspace session={session} runs={runs} onSignedOut={signedOut} /> : <LoginPanel onAuthenticated={setSession} />}
    </section>
  );
});

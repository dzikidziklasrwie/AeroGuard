import { memo, useCallback, useEffect, useMemo, useState } from 'react';
import { Icon } from './components/Icons';
import { OperationsWorkspace } from './components/OperationsWorkspace';
import { RunArchive } from './components/RunArchive';
import { TelemetryChart } from './components/TelemetryChart';
import { fetchDataset, fetchManifest, fetchRunArchive, numericTelemetryValues } from './data/jsonl';
import { usePlayback } from './hooks/usePlayback';
import {
  SEVERITIES,
  SUBSYSTEM_IDS,
  SUBSYSTEM_LABELS,
  type EventRecord,
  type HealthState,
  type RunArchiveEntry,
  type ScenarioManifest,
  type Severity,
  type SimulationDataset,
  type StateTransitionRecord,
  type SystemState,
  type TelemetryRecord,
} from './types';
import { formatDuration, formatValue, metricLabel, statusTone, titleCase } from './utils/format';
import { downloadSimulationReport } from './utils/report';

type LoadState<T> =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; value: T };

type TimelineRecord = EventRecord | StateTransitionRecord;

interface ReplayView {
  telemetry: TelemetryRecord[];
  transitions: StateTransitionRecord[];
  timeline: TimelineRecord[];
  latest: Map<string, TelemetryRecord>;
  telemetryBySubsystem: Map<string, TelemetryRecord[]>;
}

const SUBSYSTEM_ICONS = {
  power: 'power',
  cooling: 'cooling',
  temperature: 'temperature',
  communication: 'communication',
  computer: 'computer',
  equipment: 'equipment',
} as const;

const EMPTY_TELEMETRY: TelemetryRecord[] = [];
const EMPTY_RUNS: RunArchiveEntry[] = [];

function dataFilePath(file: string): string {
  if (/^(?:https?:)?\//.test(file)) return file;
  return `/data/${file.replace(/^\.\//, '')}`;
}

function currentRecordedState(records: StateTransitionRecord[]): SystemState {
  return records.at(-1)?.to ?? 'INITIALIZING';
}

function visibleRecordCount(dataset: SimulationDataset, elapsedMs: number): number {
  let lower = 0;
  let upper = dataset.records.length;
  while (lower < upper) {
    const middle = lower + Math.floor((upper - lower) / 2);
    if (dataset.records[middle].timestamp_ms <= elapsedMs) lower = middle + 1;
    else upper = middle;
  }
  return lower;
}

function buildReplayView(dataset: SimulationDataset | undefined, elapsedMs: number): ReplayView {
  const telemetry: TelemetryRecord[] = [];
  const transitions: StateTransitionRecord[] = [];
  const timeline: TimelineRecord[] = [];
  const latest = new Map<string, TelemetryRecord>();
  const telemetryBySubsystem = new Map<string, TelemetryRecord[]>();
  if (!dataset) return { telemetry, transitions, timeline, latest, telemetryBySubsystem };

  const limit = visibleRecordCount(dataset, elapsedMs);
  for (let index = 0; index < limit; index += 1) {
    const record = dataset.records[index];
    if (record.record_type === 'telemetry') {
      telemetry.push(record);
      latest.set(record.subsystem, record);
      const history = telemetryBySubsystem.get(record.subsystem);
      if (history) history.push(record);
      else telemetryBySubsystem.set(record.subsystem, [record]);
    } else if (record.record_type === 'state_transition') {
      transitions.push(record);
      timeline.push(record);
    } else if (record.record_type === 'event') {
      timeline.push(record);
    }
  }

  return { telemetry, transitions, timeline, latest, telemetryBySubsystem };
}

function MiniSparkline({ records }: { records: TelemetryRecord[] }) {
  const metric = records.flatMap((record) => numericTelemetryValues(record.data).map(([key]) => key))[0];
  const values = metric
    ? records.map((record) => record.data[metric]).filter((value): value is number => typeof value === 'number' && Number.isFinite(value))
    : [];
  const min = values.length ? Math.min(...values) : 0;
  const max = values.length ? Math.max(...values) : 1;
  const points = values.map((value, index) => {
    const x = values.length === 1 ? 50 : (index / (values.length - 1)) * 100;
    const y = 25 - ((value - min) / Math.max(max - min, 1)) * 20;
    return `${x},${y}`;
  }).join(' ');

  return (
    <svg className="mini-chart" viewBox="0 0 100 30" preserveAspectRatio="none" aria-hidden="true">
      <line x1="0" x2="100" y1="25" y2="25" />
      {values.length > 1 && <polyline points={points} />}
    </svg>
  );
}

function StatusBadge({ status }: { status: HealthState | SystemState | Severity }) {
  return <span className={`status-badge tone-${statusTone(status)}`}><span aria-hidden="true" />{titleCase(status)}</span>;
}

interface SubsystemCardProps {
  subsystem: string;
  latest?: TelemetryRecord;
  history: TelemetryRecord[];
}

function SubsystemCard({ subsystem, latest, history }: SubsystemCardProps) {
  const metrics = latest ? Object.entries(latest.data).slice(0, 3) : [];
  const status = latest?.health ?? 'UNKNOWN';
  const knownIcon = SUBSYSTEM_ICONS[subsystem as keyof typeof SUBSYSTEM_ICONS] ?? 'equipment';

  return (
    <article className={`subsystem-card tone-border-${statusTone(status)}`} aria-label={`${SUBSYSTEM_LABELS[subsystem] ?? titleCase(subsystem)} status ${titleCase(status)}`}>
      <div className="subsystem-topline">
        <span className={`subsystem-icon tone-${statusTone(status)}`}><Icon name={knownIcon} /></span>
        <StatusBadge status={status} />
      </div>
      <div className="subsystem-title">
        <h3>{SUBSYSTEM_LABELS[subsystem] ?? titleCase(subsystem)}</h3>
        <p>{latest ? `Packet ${latest.sequence.toLocaleString('en-US')} · ${latest.valid ? 'validated' : 'invalid'}` : 'Awaiting first packet'}</p>
      </div>
      <dl className="metric-list">
        {metrics.length ? metrics.map(([key, value]) => (
          <div key={key}>
            <dt>{metricLabel(key)}</dt>
            <dd>{formatValue(value)}</dd>
          </div>
        )) : (
          <div className="metric-placeholder"><dt>Telemetry</dt><dd>No data</dd></div>
        )}
      </dl>
      <div className="subsystem-footer">
        <MiniSparkline records={history.slice(-16)} />
        <span>{latest ? `T+${formatDuration(latest.timestamp_ms)}` : 'No samples'}</span>
      </div>
      {latest && !latest.valid && <p className="invalid-reason">{latest.reason || 'Simulator rejected this telemetry'}</p>}
    </article>
  );
}

function Timeline({ records, subsystems }: { records: TimelineRecord[]; subsystems: string[] }) {
  const [severity, setSeverity] = useState<'ALL' | Severity>('ALL');
  const [subsystem, setSubsystem] = useState('ALL');
  const filtered = records.filter((record) => {
    const severityMatch = severity === 'ALL' || record.severity === severity;
    const recordSubsystem = record.record_type === 'event' ? record.subsystem ?? 'system' : 'system';
    return severityMatch && (subsystem === 'ALL' || recordSubsystem === subsystem);
  }).slice(-40).reverse();

  return (
    <section className="panel timeline-panel" aria-labelledby="timeline-title">
      <div className="panel-heading timeline-heading">
        <div>
          <p className="section-kicker">Event stream</p>
          <h2 id="timeline-title">Recorded timeline</h2>
        </div>
        <span className="record-count">{filtered.length} visible</span>
      </div>
      <div className="filter-row" aria-label="Timeline filters">
        <Icon name="filter" />
        <label className="sr-only" htmlFor="severity-filter">Filter by severity</label>
        <span className="select-wrap">
          <select id="severity-filter" value={severity} onChange={(event) => setSeverity(event.target.value as 'ALL' | Severity)}>
            <option value="ALL">All severities</option>
            {SEVERITIES.map((value) => <option value={value} key={value}>{titleCase(value)}</option>)}
          </select>
        </span>
        <label className="sr-only" htmlFor="subsystem-filter">Filter by subsystem</label>
        <span className="select-wrap">
          <select id="subsystem-filter" value={subsystem} onChange={(event) => setSubsystem(event.target.value)}>
            <option value="ALL">All subsystems</option>
            <option value="system">System</option>
            {subsystems.map((value) => <option value={value} key={value}>{SUBSYSTEM_LABELS[value] ?? titleCase(value)}</option>)}
          </select>
        </span>
      </div>
      <ol className="timeline-list">
        {filtered.length ? filtered.map((record, index) => {
          const isEvent = record.record_type === 'event';
          const code = isEvent ? record.event_code : `${record.from} → ${record.to}`;
          const message = isEvent ? record.message : record.reason;
          const itemSubsystem = isEvent ? record.subsystem : null;
          return (
            <li key={`${record.timestamp_ms}-${record.record_type}-${index}`}>
              <span className={`timeline-dot tone-${statusTone(record.severity)}`} aria-hidden="true" />
              <div className="timeline-content">
                <div className="timeline-meta">
                  <time>T+{formatDuration(record.timestamp_ms)}</time>
                  <StatusBadge status={record.severity} />
                </div>
                <strong>{code}</strong>
                <p>{message}</p>
                <span>{itemSubsystem ? SUBSYSTEM_LABELS[itemSubsystem] ?? titleCase(itemSubsystem) : 'System'}</span>
              </div>
            </li>
          );
        }) : <li className="timeline-empty">No recorded events match the current filters.</li>}
      </ol>
    </section>
  );
}

const Statistics = memo(function Statistics({ dataset }: { dataset: SimulationDataset }) {
  const summary = dataset.summary;
  const telemetryCount = dataset.records.filter((record) => record.record_type === 'telemetry').length;
  const transitionCount = dataset.records.filter((record) => record.record_type === 'state_transition').length;
  const stats = [
    ['Ticks', summary?.ticks ?? 'N/A'],
    ['Telemetry packets', summary?.telemetry_count ?? telemetryCount],
    ['Invalid packets', summary?.invalid_count ?? 'N/A'],
    ['Missing heartbeats', summary?.missing_heartbeat_count ?? 'N/A'],
    ['Dropped messages', summary?.dropped_message_count ?? 'N/A'],
    ['State transitions', summary?.state_transition_count ?? transitionCount],
  ];
  return (
    <section className="stats-section" id="statistics" aria-labelledby="statistics-title">
      <div className="section-heading-row">
        <div><p className="section-kicker">Recorded totals</p><h2 id="statistics-title">Simulation statistics</h2></div>
        <p>{summary ? 'The final C++ summary record supplies these values.' : 'The artifact has no summary record, so these values are partial.'}</p>
      </div>
      <div className="stats-grid">
        {stats.map(([label, value]) => <article key={label}><span>{label}</span><strong>{typeof value === 'number' ? value.toLocaleString('en-US') : value}</strong></article>)}
      </div>
    </section>
  );
});

const ArchitectureOverview = memo(function ArchitectureOverview() {
  const stages = [
    { icon: 'database' as const, eyebrow: 'Input', title: 'Scenario config', text: 'Seeded, repeatable run parameters' },
    { icon: 'computer' as const, eyebrow: 'C++20', title: 'Health monitor', text: 'HAL, queues, validation and state machine' },
    { icon: 'database' as const, eyebrow: 'Artifact', title: 'JSONL log', text: 'Typed telemetry, events and transitions' },
    { icon: 'database' as const, eyebrow: 'ASP.NET Core', title: 'SQLite archive', text: 'REST queries, identity and fleet records' },
    { icon: 'aircraft' as const, eyebrow: 'React', title: 'Engineering console', text: 'Replay, reports and account-based fleet work' },
  ];
  return (
    <section className="architecture-section" id="architecture" aria-labelledby="architecture-title">
      <div className="section-heading-row">
        <div><p className="section-kicker">Data flow</p><h2 id="architecture-title">Architecture overview</h2></div>
        <p>Simulation decisions stay in the C++ domain. The API stores artifacts and business records without recalculating health.</p>
      </div>
      <div className="architecture-flow">
        {stages.map((stage, index) => <div className="architecture-step" key={stage.title}>
          <span className="architecture-icon"><Icon name={stage.icon} /></span>
          <div><span>{stage.eyebrow}</span><strong>{stage.title}</strong><p>{stage.text}</p></div>
          {index < stages.length - 1 && <span className="flow-link" aria-hidden="true"><i /><i /><i /></span>}
        </div>)}
      </div>
      <div className="boundary-note"><Icon name="shield" /><p>AeroGuard uses fictional aircraft components and synthetic telemetry. It doesn't control aircraft or claim certification.</p></div>
    </section>
  );
});

function DashboardSkeleton() {
  return <div className="loading-state" role="status" aria-live="polite"><span className="loader" /><div><strong>Loading recorded telemetry</strong><p>Validating JSONL schema and preparing replay…</p></div></div>;
}

export default function App() {
  const [manifestLoad, setManifestLoad] = useState<LoadState<ScenarioManifest>>({ status: 'loading' });
  const [archiveLoad, setArchiveLoad] = useState<LoadState<RunArchiveEntry[]>>({ status: 'loading' });
  const [loadedReplay, setLoadedReplay] = useState<(LoadState<SimulationDataset> & { path: string })>();
  const [selectedScenario, setSelectedScenario] = useState('');
  const [selectedRun, setSelectedRun] = useState<RunArchiveEntry>();
  const [manifestRetry, setManifestRetry] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    fetchManifest(controller.signal)
      .then((manifest) => {
        setArchiveLoad({ status: 'loading' });
        setManifestLoad({ status: 'ready', value: manifest });
        setSelectedRun(undefined);
        setSelectedScenario((current) => manifest.scenarios.some(({ id }) => id === current) ? current : manifest.scenarios[0]?.id ?? '');
      })
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === 'AbortError') return;
        setManifestLoad({ status: 'error', message: error instanceof Error ? error.message : 'Unknown manifest error.' });
      });
    return () => controller.abort();
  }, [manifestRetry]);

  useEffect(() => {
    if (manifestLoad.status !== 'ready') return;
    const controller = new AbortController();
    fetchRunArchive(manifestLoad.value, controller.signal)
      .then((runs) => setArchiveLoad({ status: 'ready', value: runs }))
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === 'AbortError') return;
        setArchiveLoad({ status: 'error', message: error instanceof Error ? error.message : 'Unknown archive error.' });
      });
    return () => controller.abort();
  }, [manifestLoad]);

  const manifestScenario = manifestLoad.status === 'ready'
    ? manifestLoad.value.scenarios.find(({ id }) => id === selectedScenario)
    : undefined;
  const scenario = useMemo(() => selectedRun ? {
      id: `archive-${selectedRun.id}`,
      label: selectedRun.label,
      description: selectedRun.description,
      file: selectedRun.replay_url,
      duration_ms: selectedRun.duration_ms,
      tick_ms: selectedRun.tick_ms,
      final_state: selectedRun.final_state,
    } : manifestScenario,
  [manifestScenario, selectedRun]);
  const replayPath = scenario ? dataFilePath(scenario.file) : undefined;

  useEffect(() => {
    if (!replayPath) return;
    const controller = new AbortController();
    const { signal } = controller;
    fetchDataset(replayPath, signal)
      .then((dataset) => {
        if (!signal.aborted) setLoadedReplay({ path: replayPath, status: 'ready', value: dataset });
      }, (error: unknown) => {
        if (!signal.aborted) {
          setLoadedReplay({ path: replayPath, status: 'error', message: error instanceof Error ? error.message : 'Unknown dataset error.' });
        }
      });
    return () => controller.abort();
  }, [replayPath]);

  const datasetLoad = loadedReplay && loadedReplay.path === replayPath
    ? loadedReplay
    : { status: 'loading' as const };
  const dataset = datasetLoad.status === 'ready' ? datasetLoad.value : undefined;
  const durationMs = dataset?.metadata.duration_ms ?? 0;
  const playback = usePlayback(durationMs, scenario?.file ?? selectedScenario);

  const replayView = useMemo(
    () => buildReplayView(dataset, playback.elapsedMs),
    [dataset, playback.elapsedMs],
  );
  const { telemetry, transitions, timeline: timelineRecords, latest, telemetryBySubsystem } = replayView;
  const systemState = currentRecordedState(transitions);
  const subsystems = useMemo(
    () => dataset?.metadata.subsystems.length ? dataset.metadata.subsystems : [...SUBSYSTEM_IDS],
    [dataset],
  );
  const [chartSubsystem, setChartSubsystem] = useState<string>('power');
  const displayedChartSubsystem = subsystems.includes(chartSubsystem)
    ? chartSubsystem
    : subsystems[0] ?? 'power';

  const downloadReport = useCallback(() => {
    if (dataset) downloadSimulationReport(dataset);
  }, [dataset]);

  const openRecordedRun = useCallback((run: RunArchiveEntry) => {
    setSelectedScenario(run.scenario);
    setSelectedRun(run);
  }, []);

  const selectedDescription = scenario?.description ?? dataset?.metadata.description;
  const artifactStatus = datasetLoad.status === 'ready'
    ? 'Artifact loaded'
    : datasetLoad.status === 'error'
      ? 'Artifact error'
      : 'Loading artifact';

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">Skip to dashboard</a>
      <aside className="sidebar">
        <a className="brand" href="#overview" aria-label="AeroGuard dashboard home">
          <span className="brand-mark"><Icon name="aircraft" /></span>
          <span><strong>AeroGuard</strong><small>Health monitor</small></span>
        </a>
        <nav aria-label="Dashboard sections">
          <a href="#overview" className="active"><span>01</span>Overview</a>
          <a href="#subsystems"><span>02</span>Subsystems</a>
          <a href="#statistics"><span>03</span>Run statistics</a>
          <a href="#archive"><span>04</span>Run archive</a>
          <a href="#workspace"><span>05</span>Workspace</a>
          <a href="#architecture"><span>06</span>Architecture</a>
        </nav>
        <div className="source-card">
            <span><Icon name="database" />Data source</span>
          <strong>{manifestLoad.status === 'ready' && manifestLoad.value.source === 'api' ? 'SQLite REST archive' : 'C++ simulator files'}</strong>
          <p>JSON Lines - Schema v1</p>
        </div>
        <p className="sidebar-foot">Independent educational project<br />Not for aircraft use</p>
      </aside>

      <main id="main-content">
        <header className="topbar">
          <div>
            <p className="breadcrumb">Engineering console <span>/</span> deterministic replay</p>
            <h1>Avionics health monitoring</h1>
          </div>
          <div className="topbar-actions">
            <span className={`connection-badge load-${datasetLoad.status}`}><i aria-hidden="true" />{artifactStatus}</span>
            <button className="button secondary-button" type="button" onClick={downloadReport} disabled={!dataset}>
              <Icon name="download" />Download report
            </button>
          </div>
        </header>

        {manifestLoad.status === 'error' ? (
          <section className="error-state" role="alert">
            <span><Icon name="database" /></span>
            <div><p className="section-kicker">Dataset unavailable</p><h2>AeroGuard couldn't load the scenario manifest</h2><p>{manifestLoad.message}</p><code>frontend/public/data/manifest.json</code></div>
            <button type="button" className="button primary-button" onClick={() => {
              setManifestLoad({ status: 'loading' });
              setLoadedReplay(undefined);
              setManifestRetry((value) => value + 1);
            }}>Try again</button>
          </section>
        ) : manifestLoad.status === 'loading' || !scenario ? <DashboardSkeleton /> : (
          <>
            <section className="scenario-bar" aria-label="Scenario selection">
              <div className="scenario-copy">
                <span className="scenario-index">SIM</span>
                <div><p>Active scenario</p><strong>{scenario.label}</strong><span>{selectedDescription}</span></div>
              </div>
              <label className="field scenario-select">
                <span>Public replay scenario</span>
                <span className="select-wrap">
                  <select value={selectedScenario} onChange={(event) => {
                    setSelectedRun(undefined);
                    setSelectedScenario(event.target.value);
                  }}>
                    {manifestLoad.value.scenarios.map((option) => <option value={option.id} key={option.id}>{option.label}</option>)}
                  </select>
                </span>
              </label>
            </section>

            {archiveLoad.status === 'ready' && <RunArchive
              runs={archiveLoad.value}
              activeReplayUrl={replayPath}
              onOpen={openRecordedRun}
            />}
            {archiveLoad.status === 'error' && <p className="workspace-message error" role="status">Run archive unavailable: {archiveLoad.message}</p>}

            {datasetLoad.status === 'loading' ? <DashboardSkeleton /> : datasetLoad.status === 'error' ? (
              <section className="error-state" role="alert">
                <span><Icon name="database" /></span>
                <div><p className="section-kicker">Replay unavailable</p><h2>AeroGuard couldn't load the JSONL artifact</h2><p>{datasetLoad.message}</p><code>{dataFilePath(scenario.file)}</code></div>
              </section>
            ) : dataset && (
              <>
                {dataset.warnings.length > 0 && <details className="warning-banner"><summary>{dataset.warnings.length} import warning{dataset.warnings.length === 1 ? '' : 's'}</summary><ul>{dataset.warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul></details>}
                <section className="health-hero" id="overview" aria-labelledby="overall-health-title">
                  <div className="health-copy">
                    <p className="section-kicker">Overall recorded state</p>
                    <div className="health-title-row"><h2 id="overall-health-title">System health</h2><StatusBadge status={systemState} /></div>
                    <p>This state matches the C++ health monitor's output at the current replay position.</p>
                    <dl className="run-facts">
                      <div><dt>Seed</dt><dd>{dataset.metadata.seed}</dd></div>
                      <div><dt>Tick</dt><dd>{dataset.metadata.tick_ms} ms</dd></div>
                      <div><dt>Frame</dt><dd>{telemetry.length.toLocaleString('en-US')}</dd></div>
                      <div><dt>Schema</dt><dd>v{dataset.metadata.schema_version}</dd></div>
                    </dl>
                  </div>
                  <div className={`health-orbit tone-${statusTone(systemState)}`} aria-label={`Recorded system state ${titleCase(systemState)}`}>
                    <svg viewBox="0 0 180 180" aria-hidden="true"><circle className="orbit-track" cx="90" cy="90" r="72" /><circle className="orbit-value" cx="90" cy="90" r="72" /><path d="M47 91h20l8-19 13 39 10-26 8 13h28" /></svg>
                    <div><Icon name="shield" /><span>State</span><strong>{titleCase(systemState)}</strong></div>
                  </div>
                  <div className="hero-flightline" aria-hidden="true"><span /><i /><span /></div>
                </section>

                <section className="subsystems-section" id="subsystems" aria-labelledby="subsystems-title">
                  <div className="section-heading-row">
                    <div><p className="section-kicker">Hardware abstraction</p><h2 id="subsystems-title">Subsystem telemetry</h2></div>
                    <p>{latest.size} of {subsystems.length} subsystems reporting at T+{formatDuration(playback.elapsedMs)}</p>
                  </div>
                  <div className="subsystem-grid">
                    {subsystems.map((subsystem) => <SubsystemCard
                      key={subsystem}
                      subsystem={subsystem}
                      latest={latest.get(subsystem)}
                      history={telemetryBySubsystem.get(subsystem) ?? EMPTY_TELEMETRY}
                    />)}
                  </div>
                </section>

                <div className="diagnostics-grid">
                  <div className="chart-stack">
                    <div className="subsystem-switcher" role="group" aria-label="Chart subsystem">
                      {subsystems.map((subsystem) => <button
                        type="button"
                        key={subsystem}
                        className={displayedChartSubsystem === subsystem ? 'active' : ''}
                        aria-pressed={displayedChartSubsystem === subsystem}
                        onClick={() => setChartSubsystem(subsystem)}
                      >{SUBSYSTEM_LABELS[subsystem]?.split(' ')[0] ?? titleCase(subsystem)}</button>)}
                    </div>
                    <TelemetryChart
                      records={telemetryBySubsystem.get(displayedChartSubsystem) ?? EMPTY_TELEMETRY}
                      subsystem={displayedChartSubsystem}
                      durationMs={durationMs}
                    />
                  </div>
                  <Timeline records={timelineRecords} subsystems={subsystems} />
                </div>

                <Statistics dataset={dataset} />
                <ArchitectureOverview />
              </>
            )}
            <OperationsWorkspace runs={archiveLoad.status === 'ready' ? archiveLoad.value : EMPTY_RUNS} />
            <footer><span>AeroGuard</span><p>Avionics Health Monitoring Simulator - C++20 + ASP.NET Core + React</p><p>Portfolio engineering project</p></footer>
          </>
        )}
      </main>
    </div>
  );
}

import { memo } from 'react';
import type { RunArchiveEntry } from '../types';
import { formatDuration, titleCase } from '../utils/format';

interface RunArchiveProps {
  runs: RunArchiveEntry[];
  activeReplayUrl?: string;
  onOpen: (run: RunArchiveEntry) => void;
}

function sourceLabel(run: RunArchiveEntry): string {
  return run.source === 'api' ? 'SQLite archive' : 'Bundled artifact';
}

function importedLabel(value?: string): string {
  if (!value) return 'Repository file';
  const date = new Date(value);
  return Number.isNaN(date.valueOf())
    ? 'Stored run'
    : date.toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' });
}

export const RunArchive = memo(function RunArchive({ runs, activeReplayUrl, onOpen }: RunArchiveProps) {
  const selected = runs.find((run) => run.replay_url === activeReplayUrl);
  const apiCount = runs.filter((run) => run.source === 'api').length;

  return (
    <section className="archive-section" id="archive" aria-labelledby="archive-title">
      <div className="section-heading-row archive-heading">
        <div>
          <p className="section-kicker">Recorded runs</p>
          <h2 id="archive-title">Simulation archive</h2>
        </div>
        <div className="archive-summary" aria-label={`${runs.length} recorded runs available`}>
          <span><i aria-hidden="true" />{runs.length} runs</span>
          <span>{apiCount > 0 ? 'SQLite REST source' : 'Repository source'}</span>
        </div>
      </div>

      {runs.length > 0 ? (
        <div className="archive-console">
          <div className="archive-index">
            <div className="archive-index-heading">
              <span>Run selector</span>
              <small>Selection starts from frame zero</small>
            </div>
            <ol className="archive-run-list" aria-label="Recorded simulation runs">
              {runs.map((run, index) => {
                const isActive = run.replay_url === activeReplayUrl;
                return (
                  <li key={run.id}>
                    <button
                      type="button"
                      className={`archive-run-option${isActive ? ' active' : ''}`}
                      onClick={() => onOpen(run)}
                      aria-pressed={isActive}
                    >
                      <span className="archive-run-number">{String(index + 1).padStart(2, '0')}</span>
                      <span className="archive-run-copy">
                        <span className="archive-run-label">{run.label}</span>
                        <span className="archive-run-meta">{sourceLabel(run)} - {formatDuration(run.duration_ms)} - {run.record_count?.toLocaleString('en-US') ?? 'local'} records</span>
                      </span>
                      <span className={`archive-state state-${run.final_state.toLowerCase()}`}>{titleCase(run.final_state)}</span>
                      <span className="archive-selection-mark" aria-hidden="true" />
                    </button>
                  </li>
                );
              })}
            </ol>
          </div>

          {selected ? (
            <article className="archive-detail" aria-live="polite">
              <div className="archive-detail-topline">
                <span className="archive-source-tag">{sourceLabel(selected)}</span>
                <span className="archive-loop-state"><i aria-hidden="true" />Continuous replay active</span>
              </div>
              <div className="archive-detail-title">
                <div>
                  <span>{selected.source === 'api' ? `RUN ${selected.id}` : 'BUNDLED RUN'}</span>
                  <h3>{selected.label}</h3>
                </div>
                <span className={`archive-state state-${selected.final_state.toLowerCase()}`}>{titleCase(selected.final_state)}</span>
              </div>
              <p className="archive-description">{selected.description}</p>
              <div className="archive-signal" aria-hidden="true"><span /><i /><i /><i /><span /></div>
              <dl className="archive-facts">
                <div><dt>Final state</dt><dd>{titleCase(selected.final_state)}</dd></div>
                <div><dt>Duration</dt><dd>{formatDuration(selected.duration_ms)}</dd></div>
                <div><dt>Simulation tick</dt><dd>{selected.tick_ms} ms</dd></div>
                <div><dt>Record count</dt><dd>{selected.record_count?.toLocaleString('en-US') ?? 'Bundled'}</dd></div>
                <div><dt>Stored</dt><dd>{importedLabel(selected.imported_at_utc)}</dd></div>
                <div><dt>Source</dt><dd>{selected.source === 'api' ? 'REST endpoint' : 'Static JSONL'}</dd></div>
              </dl>
              <div className="archive-integrity">
                <span>Artifact reference</span>
                <code>{selected.source_sha256 ? `sha256:${selected.source_sha256.slice(0, 18)}...` : selected.replay_url}</code>
              </div>
            </article>
          ) : (
            <article className="archive-detail archive-detail-empty" aria-live="polite">
              <span className="archive-source-tag">Archive ready</span>
              <div>
                <p className="section-kicker">No archived run selected</p>
                <h3>Choose a recorded run</h3>
                <p>The selected artifact will start from frame zero and continue in a loop.</p>
              </div>
            </article>
          )}
        </div>
      ) : <p className="archive-empty">No recorded runs are available.</p>}
    </section>
  );
});

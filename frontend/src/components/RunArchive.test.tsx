import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { RunArchiveEntry } from '../types';
import { RunArchive } from './RunArchive';

const runs: RunArchiveEntry[] = [
  {
    id: 42,
    scenario: 'normal-operation',
    label: 'Normal operation',
    description: 'All simulated subsystems remain within configured limits.',
    final_state: 'NORMAL',
    duration_ms: 60000,
    tick_ms: 100,
    record_count: 4210,
    source_sha256: '1234567890abcdef1234567890abcdef',
    imported_at_utc: '2030-01-02T10:30:00Z',
    replay_url: '/api/runs/42/replay',
    source: 'api',
  },
  {
    id: 43,
    scenario: 'power-fluctuations',
    label: 'Power fluctuations',
    description: 'Supply voltage moves outside the nominal range.',
    final_state: 'DEGRADED',
    duration_ms: 45000,
    tick_ms: 100,
    record_count: 3180,
    replay_url: '/api/runs/43/replay',
    source: 'api',
  },
];

describe('RunArchive', () => {
  it('shows one selected run with its source, state and recorded metrics', () => {
    render(<RunArchive runs={runs} activeReplayUrl={runs[0].replay_url} onOpen={vi.fn()} />);

    expect(screen.getByRole('button', { name: /Normal operation/ })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /Power fluctuations/ })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('heading', { name: 'Normal operation' })).toBeInTheDocument();
    expect(screen.getByText('Continuous replay active')).toBeInTheDocument();
    expect(screen.getByText('4,210')).toBeInTheDocument();
    expect(screen.getByText(/sha256:1234567890abcdef12/)).toBeInTheDocument();
  });

  it('uses the run option as an accessible selector', async () => {
    const onOpen = vi.fn();
    const user = userEvent.setup();
    render(<RunArchive runs={runs} activeReplayUrl="/data/another-run.jsonl" onOpen={onOpen} />);

    expect(screen.getByRole('heading', { name: 'Choose a recorded run' })).toBeInTheDocument();
    expect(screen.queryByText('Continuous replay active')).not.toBeInTheDocument();
    expect(screen.getAllByRole('button').every((button) => button.getAttribute('aria-pressed') === 'false')).toBe(true);

    await user.click(screen.getByRole('button', { name: /Power fluctuations/ }));

    expect(onOpen).toHaveBeenCalledWith(runs[1]);
  });
});

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
  it('loads the manifest and JSONL artifact, then exposes replay controls and recorded data', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input);
      if (url === '/data/manifest.json') return Promise.resolve(response(manifestFixture));
      if (url === '/data/normal-operation.jsonl') return Promise.resolve(response(jsonlFixture));
      return Promise.resolve(response('', 404));
    });
    vi.stubGlobal('fetch', fetchMock);
    const user = userEvent.setup();

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
    await user.click(screen.getByRole('button', { name: 'Play replay' }));
    expect(screen.getByRole('button', { name: 'Pause replay' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Pause replay' }));
    expect(screen.getByRole('button', { name: 'Play replay' })).toBeInTheDocument();

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
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
  });
});

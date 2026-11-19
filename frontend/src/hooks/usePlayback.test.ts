import { act, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { usePlayback } from './usePlayback';

afterEach(() => {
  vi.useRealTimers();
});

describe('usePlayback', () => {
  it('supports play, pause, replay and dataset reset', () => {
    vi.useFakeTimers();
    const { result, rerender } = renderHook(
      ({ duration, resetKey }) => usePlayback(duration, resetKey),
      { initialProps: { duration: 1000, resetKey: 'run-a' } },
    );

    expect(result.current.isPlaying).toBe(false);
    expect(result.current.elapsedMs).toBe(0);

    act(() => result.current.play());
    act(() => vi.advanceTimersByTime(300));
    const afterPlayback = result.current.elapsedMs;
    expect(afterPlayback).toBeGreaterThanOrEqual(200);

    act(() => result.current.pause());
    act(() => vi.advanceTimersByTime(300));
    expect(result.current.elapsedMs).toBe(afterPlayback);

    act(() => result.current.play());
    act(() => vi.advanceTimersByTime(1000));
    expect(result.current.elapsedMs).toBe(1000);
    expect(result.current.isPlaying).toBe(false);

    act(() => result.current.play());
    expect(result.current.elapsedMs).toBe(0);
    expect(result.current.isPlaying).toBe(true);

    rerender({ duration: 1000, resetKey: 'run-b' });
    expect(result.current.elapsedMs).toBe(0);
    expect(result.current.isPlaying).toBe(false);
  });
});

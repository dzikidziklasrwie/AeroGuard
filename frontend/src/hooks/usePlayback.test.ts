import { act, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { usePlayback } from './usePlayback';

afterEach(() => {
  vi.useRealTimers();
});

describe('usePlayback', () => {
  it('starts automatically and advances without controls', () => {
    vi.useFakeTimers();
    const { result } = renderHook(() => usePlayback(1000, 'run-a'));

    expect(result.current.elapsedMs).toBe(0);
    act(() => vi.advanceTimersByTime(300));
    expect(result.current.elapsedMs).toBeGreaterThanOrEqual(200);
  });

  it('loops at the recorded duration', () => {
    vi.useFakeTimers();
    const { result } = renderHook(() => usePlayback(1000, 'run-a'));

    act(() => vi.advanceTimersByTime(900));
    expect(result.current.elapsedMs).toBeGreaterThanOrEqual(800);
    act(() => vi.advanceTimersByTime(100));
    expect(result.current.elapsedMs).toBe(1000);
    act(() => vi.advanceTimersByTime(100));
    expect(result.current.elapsedMs).toBeLessThan(200);
  });

  it('resets immediately and keeps running when the selected replay changes', () => {
    vi.useFakeTimers();
    const { result, rerender } = renderHook(
      ({ resetKey }) => usePlayback(1000, resetKey),
      { initialProps: { resetKey: 'run-a' } },
    );

    act(() => vi.advanceTimersByTime(400));
    expect(result.current.elapsedMs).toBeGreaterThan(0);

    rerender({ resetKey: 'run-b' });
    expect(result.current.elapsedMs).toBe(0);
    act(() => vi.advanceTimersByTime(200));
    expect(result.current.elapsedMs).toBeGreaterThan(0);
  });

  it('waits at frame zero until a positive duration is available', () => {
    vi.useFakeTimers();
    const { result, rerender } = renderHook(
      ({ duration }) => usePlayback(duration, 'run-a'),
      { initialProps: { duration: 0 } },
    );

    act(() => vi.advanceTimersByTime(500));
    expect(result.current.elapsedMs).toBe(0);
    rerender({ duration: 1000 });
    act(() => vi.advanceTimersByTime(200));
    expect(result.current.elapsedMs).toBeGreaterThan(0);
  });
});

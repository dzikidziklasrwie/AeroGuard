import { useCallback, useEffect, useRef, useState } from 'react';

const FRAME_MS = 100;

export function usePlayback(durationMs: number, resetKey: string) {
  const [elapsedMs, setElapsedMs] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const lastTick = useRef<number | null>(null);

  /* eslint-disable react-hooks/set-state-in-effect -- A newly selected replay needs an independent playback cursor. */
  useEffect(() => {
    setElapsedMs(0);
    setIsPlaying(false);
    lastTick.current = null;
  }, [resetKey]);
  /* eslint-enable react-hooks/set-state-in-effect */

  useEffect(() => {
    if (!isPlaying || durationMs <= 0) return;

    const interval = window.setInterval(() => {
      const now = performance.now();
      const delta = lastTick.current === null ? FRAME_MS : Math.min(now - lastTick.current, 500);
      lastTick.current = now;
      setElapsedMs((current) => {
        const next = Math.min(current + delta, durationMs);
        if (next >= durationMs) setIsPlaying(false);
        return next;
      });
    }, FRAME_MS);

    return () => window.clearInterval(interval);
  }, [durationMs, isPlaying]);

  useEffect(() => {
    if (!isPlaying) lastTick.current = null;
  }, [isPlaying]);

  const pause = useCallback(() => setIsPlaying(false), []);
  const play = useCallback(() => {
    setElapsedMs((current) => (current >= durationMs ? 0 : current));
    setIsPlaying(true);
  }, [durationMs]);

  return { elapsedMs, isPlaying, pause, play };
}

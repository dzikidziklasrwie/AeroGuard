import { useEffect, useRef, useState } from 'react';

const FRAME_MS = 100;

interface PlaybackCursor {
  key: string;
  elapsedMs: number;
}

export function usePlayback(durationMs: number, resetKey: string, renderFrames = true) {
  const [cursor, setCursor] = useState<PlaybackCursor>({ key: resetKey, elapsedMs: 0 });
  const clock = useRef<{ key: string; durationMs: number; startedAt: number } | null>(null);
  const elapsedMs = cursor.key === resetKey
    ? Math.min(cursor.elapsedMs, Math.max(durationMs, 0))
    : 0;

  useEffect(() => {
    if (clock.current?.key !== resetKey || clock.current.durationMs !== durationMs) {
      clock.current = { key: resetKey, durationMs, startedAt: performance.now() };
    }
    const { startedAt } = clock.current;
    if (durationMs <= 0 || !renderFrames) return;

    const interval = window.setInterval(() => {
      const phase = (performance.now() - startedAt) % (durationMs + FRAME_MS);
      setCursor({ key: resetKey, elapsedMs: Math.min(phase, durationMs) });
    }, FRAME_MS);

    return () => window.clearInterval(interval);
  }, [durationMs, resetKey, renderFrames]);

  return { elapsedMs };
}

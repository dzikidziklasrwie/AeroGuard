import { useEffect, useRef, useState } from 'react';

const FRAME_MS = 100;

interface PlaybackCursor {
  key: string;
  elapsedMs: number;
}

export function usePlayback(durationMs: number, resetKey: string) {
  const [cursor, setCursor] = useState<PlaybackCursor>({ key: resetKey, elapsedMs: 0 });
  const lastTick = useRef<number | null>(null);
  const elapsedMs = cursor.key === resetKey
    ? Math.min(cursor.elapsedMs, Math.max(durationMs, 0))
    : 0;

  useEffect(() => {
    lastTick.current = performance.now();
    if (durationMs <= 0) return;

    const interval = window.setInterval(() => {
      const now = performance.now();
      const delta = lastTick.current === null ? FRAME_MS : Math.max(now - lastTick.current, 0);
      lastTick.current = now;
      setCursor((current) => {
        const currentElapsed = current.key === resetKey ? current.elapsedMs : 0;
        const next = currentElapsed >= durationMs
          ? 0
          : Math.min(currentElapsed + delta, durationMs);
        return {
          key: resetKey,
          elapsedMs: next,
        };
      });
    }, FRAME_MS);

    return () => window.clearInterval(interval);
  }, [durationMs, resetKey]);

  return { elapsedMs };
}

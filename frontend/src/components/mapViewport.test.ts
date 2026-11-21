import { describe, expect, it } from 'vitest';
import {
  centerViewport,
  clientToMapPoint,
  INITIAL_MAP_VIEWPORT,
  MAX_MAP_ZOOM,
  zoomViewport,
} from './mapViewport';

describe('map viewport', () => {
  it('zooms around the requested map coordinate and retains its screen ratio', () => {
    const next = zoomViewport(INITIAL_MAP_VIEWPORT, 1.5, 270, 285);

    expect(next).toEqual({ x: 90, y: 95, width: 720, height: 380, zoom: 1.5 });
  });

  it('clamps zoom and centered views to the map bounds', () => {
    const next = centerViewport([0, 0], 100);

    expect(next.zoom).toBe(MAX_MAP_ZOOM);
    expect(next.x).toBe(0);
    expect(next.y).toBe(0);
  });

  it('converts a browser point through the current view box', () => {
    const viewport = zoomViewport(INITIAL_MAP_VIEWPORT, 1.5, 270, 285);

    expect(clientToMapPoint(540, 285, { left: 0, top: 0, width: 1080, height: 570 }, viewport))
      .toEqual([450, 285]);
    expect(clientToMapPoint(10, 10, { left: 0, top: 0, width: 0, height: 570 }, viewport))
      .toBeNull();
  });
});

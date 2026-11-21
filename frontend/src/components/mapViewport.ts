export const MAP_WIDTH = 1_080;
export const MAP_HEIGHT = 570;
export const MIN_MAP_ZOOM = 1;
export const MAX_MAP_ZOOM = 6;
export const MAP_ZOOM_STEP = 1.5;

export interface MapViewport {
  x: number;
  y: number;
  width: number;
  height: number;
  zoom: number;
}

interface ViewportBounds {
  left: number;
  top: number;
  width: number;
  height: number;
}

export const INITIAL_MAP_VIEWPORT: MapViewport = {
  x: 0,
  y: 0,
  width: MAP_WIDTH,
  height: MAP_HEIGHT,
  zoom: MIN_MAP_ZOOM,
};

export function clampViewport(viewport: MapViewport): MapViewport {
  const zoom = Math.max(MIN_MAP_ZOOM, Math.min(MAX_MAP_ZOOM, viewport.zoom));
  const width = MAP_WIDTH / zoom;
  const height = MAP_HEIGHT / zoom;
  return {
    x: Math.max(0, Math.min(MAP_WIDTH - width, viewport.x)),
    y: Math.max(0, Math.min(MAP_HEIGHT - height, viewport.y)),
    width,
    height,
    zoom,
  };
}

export function zoomViewport(
  current: MapViewport,
  requestedZoom: number,
  anchorX = current.x + current.width / 2,
  anchorY = current.y + current.height / 2,
): MapViewport {
  const zoom = Math.max(MIN_MAP_ZOOM, Math.min(MAX_MAP_ZOOM, requestedZoom));
  const width = MAP_WIDTH / zoom;
  const height = MAP_HEIGHT / zoom;
  const relativeX = (anchorX - current.x) / current.width;
  const relativeY = (anchorY - current.y) / current.height;
  return clampViewport({
    x: anchorX - relativeX * width,
    y: anchorY - relativeY * height,
    width,
    height,
    zoom,
  });
}

export function centerViewport(point: [number, number], requestedZoom: number): MapViewport {
  const zoom = Math.max(MIN_MAP_ZOOM, Math.min(MAX_MAP_ZOOM, requestedZoom));
  const width = MAP_WIDTH / zoom;
  const height = MAP_HEIGHT / zoom;
  return clampViewport({
    x: point[0] - width / 2,
    y: point[1] - height / 2,
    width,
    height,
    zoom,
  });
}

export function clientToMapPoint(
  clientX: number,
  clientY: number,
  bounds: ViewportBounds,
  viewport: MapViewport,
): [number, number] | null {
  if (!bounds.width || !bounds.height) return null;
  return [
    viewport.x + (clientX - bounds.left) * viewport.width / bounds.width,
    viewport.y + (clientY - bounds.top) * viewport.height / bounds.height,
  ];
}

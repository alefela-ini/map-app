/**
 * Pure geometry and mapping helpers (no React, no Leaflet) so they are easy to unit test.
 */
import type {
  CreateMapObjectRequest,
  CreatePolygonRequest,
  LatLng,
  MapDataRow,
  MapObject,
  MapObjectDto,
  MapPolygon,
  PolygonDto,
  Position,
} from './types';

// ---- Coordinate conversion -----------------------------------------------

/** Leaflet { lat, lng } -> GeoJSON [lng, lat]. */
export const toPosition = ({ lat, lng }: LatLng): Position => [lng, lat];

/** GeoJSON [lng, lat] -> Leaflet { lat, lng }. */
export const fromPosition = ([lng, lat]: Position): LatLng => ({ lat, lng });

/**
 * Brings a clicked point back into the valid range. Leaflet reports longitudes beyond ±180
 * when the user clicks on a repeated copy of the world, which the server would reject.
 */
export function normalizeLatLng({ lat, lng }: LatLng): LatLng {
  return {
    lat: Math.max(-90, Math.min(90, lat)),
    // Only wrap when needed: the modulo arithmetic would add floating-point noise to valid values.
    lng: lng >= -180 && lng <= 180 ? lng : ((((lng + 180) % 360) + 360) % 360) - 180,
  };
}

const samePoint = (a: LatLng, b: LatLng) => a.lat === b.lat && a.lng === b.lng;

// ---- DTO <-> view model --------------------------------------------------

export function polygonFromDto(dto: PolygonDto): MapPolygon {
  const ring = (dto.geometry.coordinates[0] ?? []).map(fromPosition);
  // GeoJSON rings are closed; the UI works with open rings.
  if (ring.length > 1 && samePoint(ring[0], ring[ring.length - 1])) {
    ring.pop();
  }
  return { id: dto.id, name: dto.name, vertices: ring, saved: true };
}

export function polygonToRequest(polygon: MapPolygon): CreatePolygonRequest {
  const ring = polygon.vertices.map(toPosition);
  return {
    name: polygon.name,
    clientId: polygon.id,
    geometry: { type: 'Polygon', coordinates: [[...ring, ring[0]]] },
  };
}

export function objectFromDto(dto: MapObjectDto): MapObject {
  return {
    id: dto.id,
    name: dto.name,
    symbol: dto.symbol,
    position: fromPosition(dto.geometry.coordinates),
    saved: true,
  };
}

export function objectToRequest(object: MapObject): CreateMapObjectRequest {
  return {
    name: object.name,
    clientId: object.id,
    symbol: object.symbol,
    geometry: { type: 'Point', coordinates: toPosition(object.position) },
  };
}

// ---- Polygon validity (mirrors the server rules) -------------------------

const EPSILON = 1e-12;

const cross = (a: LatLng, b: LatLng, c: LatLng) =>
  (b.lng - a.lng) * (c.lat - a.lat) - (b.lat - a.lat) * (c.lng - a.lng);

const sign = (v: number) => (Math.abs(v) < EPSILON ? 0 : Math.sign(v));

const onSegment = (a: LatLng, b: LatLng, p: LatLng) =>
  p.lng >= Math.min(a.lng, b.lng) - EPSILON &&
  p.lng <= Math.max(a.lng, b.lng) + EPSILON &&
  p.lat >= Math.min(a.lat, b.lat) - EPSILON &&
  p.lat <= Math.max(a.lat, b.lat) + EPSILON;

function segmentsIntersect(p1: LatLng, p2: LatLng, q1: LatLng, q2: LatLng): boolean {
  const d1 = sign(cross(q1, q2, p1));
  const d2 = sign(cross(q1, q2, p2));
  const d3 = sign(cross(p1, p2, q1));
  const d4 = sign(cross(p1, p2, q2));

  if (d1 * d2 < 0 && d3 * d4 < 0) return true;

  return (
    (d1 === 0 && onSegment(q1, q2, p1)) ||
    (d2 === 0 && onSegment(q1, q2, p2)) ||
    (d3 === 0 && onSegment(p1, p2, q1)) ||
    (d4 === 0 && onSegment(p1, p2, q2))
  );
}

/**
 * Returns a reason why the open ring cannot be saved as a polygon, or `null` if it is valid.
 * Same rules as the server: at least 3 vertices, no crossing/touching edges, non-zero area.
 */
export function polygonProblem(vertices: LatLng[]): string | null {
  const ring = vertices.filter((v, i) => i === 0 || !samePoint(v, vertices[i - 1]));
  if (ring.length > 1 && samePoint(ring[0], ring[ring.length - 1])) ring.pop();

  const n = ring.length;
  if (n < 3) return 'A polygon needs at least 3 different vertices.';

  for (let i = 0; i < n; i++) {
    const a1 = ring[i];
    const a2 = ring[(i + 1) % n];
    const a3 = ring[(i + 2) % n];

    // An edge folding back over the previous one.
    const dot = (a2.lng - a1.lng) * (a3.lng - a2.lng) + (a2.lat - a1.lat) * (a3.lat - a2.lat);
    if (sign(cross(a1, a2, a3)) === 0 && dot < 0) return 'Polygon edges must not overlap.';

    for (let j = i + 2; j < n; j++) {
      if (i === 0 && j === n - 1) continue; // adjacent through the closing edge
      if (segmentsIntersect(a1, a2, ring[j], ring[(j + 1) % n])) {
        return 'Polygon edges must not cross or touch each other.';
      }
    }
  }

  let area = 0;
  for (let i = 0; i < n; i++) {
    const a = ring[i];
    const b = ring[(i + 1) % n];
    area += a.lng * b.lat - b.lng * a.lat;
  }
  if (Math.abs(area / 2) < EPSILON) return 'A polygon must have a non-zero area.';

  return null;
}

// ---- Misc helpers --------------------------------------------------------

/**
 * Returns "<prefix> N" where N is one more than the largest number already used with that prefix,
 * so names stay unique after deletions.
 */
export function nextName(prefix: string, existingNames: readonly string[]): string {
  const pattern = new RegExp(`^${prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')} (\\d+)$`);
  const max = existingNames.reduce((acc, name) => {
    const match = pattern.exec(name);
    return match ? Math.max(acc, Number(match[1])) : acc;
  }, 0);
  return `${prefix} ${max + 1}`;
}

/** Builds the "Map Data" rows: one row per object and one row per polygon vertex. */
export function buildMapDataRows(polygons: readonly MapPolygon[], objects: readonly MapObject[]): MapDataRow[] {
  const polygonRows = polygons.flatMap((polygon) =>
    polygon.vertices.map((vertex, index) => ({
      key: `p:${polygon.id}:${index}`,
      kind: 'polygon' as const,
      itemId: polygon.id,
      label: `${polygon.name} · vertex ${index + 1}`,
      lat: vertex.lat,
      lng: vertex.lng,
    })),
  );

  const objectRows = objects.map((object) => ({
    key: `o:${object.id}`,
    kind: 'object' as const,
    itemId: object.id,
    label: object.name,
    lat: object.position.lat,
    lng: object.position.lng,
  }));

  return [...polygonRows, ...objectRows];
}

/** Formats a coordinate with 6 decimals (~0.1 m precision). */
export const formatCoordinate = (value: number) => value.toFixed(6);

import { describe, expect, it } from 'vitest';
import {
  buildMapDataRows,
  nextName,
  normalizeLatLng,
  objectFromDto,
  objectToRequest,
  polygonFromDto,
  polygonProblem,
  polygonToRequest,
} from './geo';
import type { LatLng, MapPolygon } from './types';

const ll = (lat: number, lng: number): LatLng => ({ lat, lng });

describe('coordinate order', () => {
  it('sends polygons as closed GeoJSON rings in [lng, lat] order', () => {
    const polygon: MapPolygon = { id: 'd1', name: 'A', saved: false, vertices: [ll(32, 34), ll(32, 35), ll(33, 35)] };

    expect(polygonToRequest(polygon).geometry.coordinates).toEqual([
      [
        [34, 32],
        [35, 32],
        [35, 33],
        [34, 32],
      ],
    ]);
  });

  it('reads closed GeoJSON rings back as open rings', () => {
    const polygon = polygonFromDto({
      id: '1',
      name: 'A',
      createdAt: '',
      geometry: { type: 'Polygon', coordinates: [[[34, 32], [35, 32], [35, 33], [34, 32]]] },
    });

    expect(polygon.vertices).toEqual([ll(32, 34), ll(32, 35), ll(33, 35)]);
    expect(polygon.saved).toBe(true);
  });

  it('round-trips objects', () => {
    const dto = { id: '1', name: 'J', symbol: 'jeep' as const, createdAt: '', geometry: { type: 'Point' as const, coordinates: [34.7, 32.1] as [number, number] } };
    const object = objectFromDto(dto);

    expect(object.position).toEqual(ll(32.1, 34.7));
    expect(objectToRequest(object).geometry.coordinates).toEqual([34.7, 32.1]);
  });
});

describe('idempotency key', () => {
  it('sends the draft id as clientId, so a retried Save cannot create duplicates', () => {
    const polygon: MapPolygon = { id: 'draft-abc', name: 'A', saved: false, vertices: [ll(32, 34), ll(32, 35), ll(33, 35)] };

    expect(polygonToRequest(polygon).clientId).toBe('draft-abc');
    expect(objectToRequest({ id: 'draft-xyz', name: 'J', symbol: 'jeep', saved: false, position: ll(32, 34) }).clientId).toBe(
      'draft-xyz',
    );
  });
});

describe('normalizeLatLng', () => {
  it('wraps longitudes from repeated world copies', () => {
    expect(normalizeLatLng(ll(10, 200)).lng).toBeCloseTo(-160);
    expect(normalizeLatLng(ll(10, -190)).lng).toBeCloseTo(170);
    expect(normalizeLatLng(ll(10, 540)).lng).toBeCloseTo(180 - 360);
  });

  it('keeps valid values unchanged', () => {
    expect(normalizeLatLng(ll(32.08, 34.78))).toEqual(ll(32.08, 34.78));
    expect(normalizeLatLng(ll(0, 180))).toEqual(ll(0, 180));
  });
});

describe('polygonProblem', () => {
  it('accepts a simple polygon', () => {
    expect(polygonProblem([ll(0, 0), ll(0, 1), ll(1, 1), ll(1, 0)])).toBeNull();
  });

  it('rejects a bow-tie', () => {
    expect(polygonProblem([ll(0, 0), ll(1, 1), ll(0, 1), ll(1, 0)])).toMatch(/cross/);
  });

  it('rejects too few distinct vertices', () => {
    expect(polygonProblem([ll(0, 0), ll(0, 0), ll(1, 1)])).toMatch(/at least 3/);
  });

  it('rejects collinear vertices', () => {
    expect(polygonProblem([ll(0, 0), ll(1, 1), ll(2, 2)])).not.toBeNull();
  });
});

describe('nextName', () => {
  it('continues after the largest existing number', () => {
    expect(nextName('Polygon', ['Polygon 1', 'Polygon 3', 'Jeep 7', 'Polygon x'])).toBe('Polygon 4');
    expect(nextName('Jeep', [])).toBe('Jeep 1');
  });
});

describe('buildMapDataRows', () => {
  it('creates one row per polygon vertex and per object', () => {
    const rows = buildMapDataRows(
      [{ id: 'p', name: 'Zone', saved: true, vertices: [ll(1, 2), ll(3, 4), ll(5, 6)] }],
      [{ id: 'o', name: 'Jeep 1', saved: true, symbol: 'jeep', position: ll(7, 8) }],
    );

    expect(rows.map((r) => [r.label, r.lat, r.lng])).toEqual([
      ['Zone · vertex 1', 1, 2],
      ['Zone · vertex 2', 3, 4],
      ['Zone · vertex 3', 5, 6],
      ['Jeep 1', 7, 8],
    ]);
  });
});

/**
 * Shared types. API contracts mirror the ASP.NET Core DTOs; GeoJSON positions are [longitude, latitude].
 * View models use Leaflet's { lat, lng } to avoid mixing up the axis order in UI code.
 */

export interface LatLng {
  lat: number;
  lng: number;
}

/** GeoJSON position: [longitude, latitude]. */
export type Position = [number, number];

export type ObjectSymbol = 'marker' | 'jeep';

export const OBJECT_SYMBOLS: readonly { value: ObjectSymbol; label: string }[] = [
  { value: 'marker', label: 'Marker' },
  { value: 'jeep', label: 'Jeep' },
];

// ---- API contracts -------------------------------------------------------

export interface PolygonGeometry {
  type: 'Polygon';
  coordinates: Position[][];
}

export interface PointGeometry {
  type: 'Point';
  coordinates: Position;
}

export interface PolygonDto {
  id: string;
  name: string;
  geometry: PolygonGeometry;
  createdAt: string;
}

export interface CreatePolygonRequest {
  name: string;
  /** Idempotency key (the draft id): saving the same key again does not create a second item. */
  clientId: string;
  geometry: PolygonGeometry;
}

export interface MapObjectDto {
  id: string;
  name: string;
  symbol: ObjectSymbol;
  geometry: PointGeometry;
  createdAt: string;
}

export interface CreateMapObjectRequest {
  name: string;
  /** Idempotency key (the draft id): saving the same key again does not create a second item. */
  clientId: string;
  symbol: ObjectSymbol;
  geometry: PointGeometry;
}

// ---- View models ---------------------------------------------------------

/** Common shape of everything that can be drawn, saved and deleted. */
export interface EditableItem {
  id: string;
  name: string;
  /** `false` for drafts that exist only in the browser until "Save". */
  saved: boolean;
}

export interface MapPolygon extends EditableItem {
  /** Open ring: the first vertex is NOT repeated at the end. */
  vertices: LatLng[];
}

export interface MapObject extends EditableItem {
  symbol: ObjectSymbol;
  position: LatLng;
}

/** What a click on the map currently does. */
export type InteractionMode =
  | { kind: 'idle' }
  | { kind: 'drawPolygon' }
  | { kind: 'placeObject'; symbol: ObjectSymbol };

/** One row of the "Map Data" table. */
export interface MapDataRow {
  key: string;
  kind: 'polygon' | 'object';
  itemId: string;
  label: string;
  lat: number;
  lng: number;
}

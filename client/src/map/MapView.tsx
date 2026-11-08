import { useEffect, useRef } from 'react';
import { MapContainer, Marker, Polygon, TileLayer, Tooltip, useMap, useMapEvents } from 'react-leaflet';
import L, { type LeafletMouseEvent } from 'leaflet';
import { normalizeLatLng } from '../geo';
import type { InteractionMode, LatLng, MapObject, MapPolygon } from '../types';
import { DrawingLayer } from './DrawingLayer';
import { getIcon } from './icons';

// Tel Aviv area, as in the task mockup. Used only until saved data is loaded (InitialFit then fits to it).
const DEFAULT_CENTER: [number, number] = [32.03, 34.8];
const DEFAULT_ZOOM = 11;

const polygonStyle = (polygon: MapPolygon, selected: boolean): L.PathOptions => ({
  color: selected ? '#7c3aed' : polygon.saved ? '#2563eb' : '#ea580c',
  weight: selected ? 4 : 2,
  dashArray: polygon.saved ? undefined : '6 4',
  fillOpacity: selected ? 0.35 : 0.2,
});

/** A request to move the map to one or more points; `seq` makes repeated requests distinct. */
export interface FocusRequest {
  points: LatLng[];
  seq: number;
}

interface MapViewProps {
  mode: InteractionMode;
  polygons: MapPolygon[];
  objects: MapObject[];
  selectedPolygonId: string | null;
  selectedObjectId: string | null;
  drawingVertices: LatLng[];
  focus: FocusRequest | null;
  /** True once the saved data has been loaded; the map then fits to it once. */
  dataLoaded: boolean;
  onSelectPolygon: (id: string) => void;
  onSelectObject: (id: string) => void;
  /** A click on empty map outside drawing / placing. */
  onClearSelection: () => void;
  onAddVertex: (point: LatLng) => void;
  onClosePolygon: () => void;
  onPlaceObject: (point: LatLng) => void;
  onMoveDraftObject: (id: string, point: LatLng) => void;
}

/** The interactive map: saved and draft items, polygon drawing and object placement. */
export function MapView(props: MapViewProps) {
  const { mode, polygons, objects, selectedPolygonId, selectedObjectId, drawingVertices, focus } = props;

  // In drawing/placing modes, existing shapes must not swallow clicks meant for the map.
  // `interactive` cannot be changed after a Leaflet layer is created, so it is part of the React key.
  const interactive = mode.kind === 'idle';

  return (
    <MapContainer
      center={DEFAULT_CENTER}
      zoom={DEFAULT_ZOOM}
      minZoom={2}
      worldCopyJump
      className="map"
    >
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />

      {polygons.map((polygon) => (
        <Polygon
          key={`${polygon.id}:${interactive}`}
          positions={polygon.vertices.map((v) => [v.lat, v.lng] as [number, number])}
          pathOptions={polygonStyle(polygon, polygon.id === selectedPolygonId)}
          interactive={interactive}
          eventHandlers={{
            click: (e) => {
              L.DomEvent.stopPropagation(e);
              props.onSelectPolygon(polygon.id);
            },
          }}
        >
          <Tooltip sticky>{polygon.saved ? polygon.name : `${polygon.name} (unsaved)`}</Tooltip>
        </Polygon>
      ))}

      {objects.map((object) => (
        <Marker
          key={`${object.id}:${interactive}`}
          position={[object.position.lat, object.position.lng]}
          icon={getIcon(object.symbol, object.id === selectedObjectId ? 'selected' : object.saved ? 'saved' : 'draft')}
          interactive={interactive}
          draggable={interactive && !object.saved}
          keyboard={false}
          eventHandlers={{
            click: () => props.onSelectObject(object.id),
            dragend: (e) => {
              const marker = e.target as L.Marker;
              props.onMoveDraftObject(object.id, normalizeLatLng(marker.getLatLng()));
            },
          }}
        >
          <Tooltip direction="top">{object.saved ? object.name : `${object.name} (unsaved, drag to move)`}</Tooltip>
        </Marker>
      ))}

      {mode.kind === 'drawPolygon' && (
        <DrawingLayer vertices={drawingVertices} onAddVertex={props.onAddVertex} onClose={props.onClosePolygon} />
      )}

      {mode.kind === 'placeObject' && <PlacementLayer onPlace={props.onPlaceObject} />}

      {mode.kind === 'idle' && <EmptyClickLayer onClick={props.onClearSelection} />}

      <FocusController focus={focus} />
      <InitialFit ready={props.dataLoaded} polygons={polygons} objects={objects} />
    </MapContainer>
  );
}

/**
 * Outside drawing / placing: a click on empty map clears the selection. Clicks on shapes never get here:
 * polygons stop propagation and markers do not bubble to the map; dragging the map is not a click.
 */
function EmptyClickLayer({ onClick }: { onClick: () => void }) {
  useMapEvents({ click: () => onClick() });
  return null;
}

/** Object placement mode: the next click on the map places the object. */
function PlacementLayer({ onPlace }: { onPlace: (point: LatLng) => void }) {
  const map = useMap();

  useEffect(() => {
    map.stop(); // Place the object where the user aims, not on a map still moving after a flyTo.
    map.getContainer().classList.add('map--placing');
    return () => map.getContainer().classList.remove('map--placing');
  }, [map]);

  useMapEvents({
    click: (e: LeafletMouseEvent) => onPlace(normalizeLatLng(e.latlng)),
  });

  return null;
}

/** Fits the map to the stored data once, right after the first successful load. */
function InitialFit({ ready, polygons, objects }: { ready: boolean; polygons: MapPolygon[]; objects: MapObject[] }) {
  const map = useMap();
  const done = useRef(false);

  useEffect(() => {
    if (!ready || done.current) return;
    done.current = true;

    const points = [...polygons.flatMap((p) => p.vertices), ...objects.map((o) => o.position)];
    if (points.length > 0) {
      map.fitBounds(L.latLngBounds(points.map((p) => [p.lat, p.lng])), { padding: [40, 40], maxZoom: 15 });
    }
  }, [ready, polygons, objects, map]);

  return null;
}

/** Moves the map when a new focus request arrives (e.g. a row in the Map Data table is clicked). */
function FocusController({ focus }: { focus: FocusRequest | null }) {
  const map = useMap();
  const lastSeq = useRef<number | null>(null);

  useEffect(() => {
    if (!focus || focus.seq === lastSeq.current || focus.points.length === 0) return;
    lastSeq.current = focus.seq;

    if (focus.points.length === 1) {
      const [p] = focus.points;
      map.flyTo([p.lat, p.lng], Math.max(map.getZoom(), 14));
    } else {
      map.flyToBounds(L.latLngBounds(focus.points.map((p) => [p.lat, p.lng])), { padding: [40, 40], maxZoom: 16 });
    }
  }, [focus, map]);

  return null;
}

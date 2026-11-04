import { useEffect, useState } from 'react';
import { CircleMarker, Polyline, useMap, useMapEvents } from 'react-leaflet';
import type { LeafletMouseEvent } from 'leaflet';
import { normalizeLatLng } from '../geo';
import type { LatLng } from '../types';

/** How close (in screen pixels) a click must be to the first vertex to close the polygon. */
const CLOSE_TOLERANCE_PX = 12;

const DRAW_COLOR = '#ea580c';

interface DrawingLayerProps {
  vertices: LatLng[];
  onAddVertex: (point: LatLng) => void;
  onClose: () => void;
}

/**
 * Polygon drawing mode: every click on the map adds a vertex; clicking (near) the first vertex
 * closes the polygon. Shows the polyline drawn so far and a rubber-band line to the cursor.
 */
export function DrawingLayer({ vertices, onAddVertex, onClose }: DrawingLayerProps) {
  const map = useMap();
  const [cursor, setCursor] = useState<LatLng | null>(null);

  // Double-click would zoom and also add two vertices; disable it while drawing.
  useEffect(() => {
    // A running flyTo (after a Map Data row click) would move the first vertex away from under the cursor.
    map.stop();
    map.doubleClickZoom.disable();
    map.getContainer().classList.add('map--drawing');
    return () => {
      map.doubleClickZoom.enable();
      map.getContainer().classList.remove('map--drawing');
    };
  }, [map]);

  const pixelDistance = (a: LatLng, b: LatLng) =>
    map.latLngToContainerPoint([a.lat, a.lng]).distanceTo(map.latLngToContainerPoint([b.lat, b.lng]));

  useMapEvents({
    click(e: LeafletMouseEvent) {
      const point = normalizeLatLng(e.latlng);
      const first = vertices[0];
      const last = vertices[vertices.length - 1];

      if (first && pixelDistance(first, e.latlng) <= CLOSE_TOLERANCE_PX) {
        // Clicking the start point closes the polygon (needs at least 3 vertices).
        if (vertices.length >= 3) onClose();
        return;
      }

      // Ignore accidental double clicks on the same spot.
      if (last && pixelDistance(last, e.latlng) <= 3) return;

      onAddVertex(point);
    },
    mousemove(e: LeafletMouseEvent) {
      setCursor({ lat: e.latlng.lat, lng: e.latlng.lng });
    },
    mouseout() {
      setCursor(null);
    },
  });

  const path = vertices.map((v) => [v.lat, v.lng] as [number, number]);
  const last = vertices[vertices.length - 1];
  const canClose = vertices.length >= 3;

  return (
    <>
      {path.length > 1 && <Polyline positions={path} pathOptions={{ color: DRAW_COLOR, weight: 3 }} interactive={false} />}

      {last && cursor && (
        <Polyline
          positions={[[last.lat, last.lng], [cursor.lat, cursor.lng]]}
          pathOptions={{ color: DRAW_COLOR, weight: 2, dashArray: '6 6' }}
          interactive={false}
        />
      )}

      {vertices.map((v, index) => (
        <CircleMarker
          // Index keys are fine: vertices are only appended or removed from the end.
          key={index}
          center={[v.lat, v.lng]}
          radius={index === 0 ? (canClose ? 8 : 6) : 4}
          // Not interactive: the map's click handler decides whether a click closes the polygon.
          interactive={false}
          pathOptions={{
            color: DRAW_COLOR,
            weight: 2,
            fillColor: index === 0 && canClose ? '#fde68a' : '#ffffff',
            fillOpacity: 1,
          }}
        />
      ))}
    </>
  );
}

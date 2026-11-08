/**
 * Marker icons. The standard Leaflet pin is created explicitly because its default image URLs
 * break when Leaflet is bundled (Vite rewrites asset paths); the Jeep is an inline SVG.
 */
import L from 'leaflet';
import markerIconUrl from 'leaflet/dist/images/marker-icon.png';
import markerIcon2xUrl from 'leaflet/dist/images/marker-icon-2x.png';
import markerShadowUrl from 'leaflet/dist/images/marker-shadow.png';
import type { ObjectSymbol } from '../types';

export type IconState = 'saved' | 'draft' | 'selected';

/** Image of the standard pin, also used as a preview in the Objects panel's Add menu. */
export { markerIconUrl as MARKER_ICON_URL };

/** The Jeep symbol (static markup), drawn on the map and as a preview in the Add menu. */
export const JEEP_SVG = `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 32" width="44" height="30" aria-hidden="true">
  <path d="M4 20 L6 13 L14 13 L18 6 L32 6 L34 13 L43 14 L45 20 Z" fill="#4d6b2f" stroke="#26361a" stroke-width="1.5" stroke-linejoin="round"/>
  <path d="M19.5 8 L30.5 8 L31.8 13 L16.7 13 Z" fill="#cfe3f5" stroke="#26361a" stroke-width="1"/>
  <rect x="2" y="18" width="44" height="5" rx="1.5" fill="#3b5324" stroke="#26361a" stroke-width="1.2"/>
  <rect x="42" y="15" width="4" height="3" rx="1" fill="#ffd54a"/>
  <circle cx="13" cy="24" r="5.5" fill="#1f1f1f"/><circle cx="13" cy="24" r="2.2" fill="#9e9e9e"/>
  <circle cx="36" cy="24" r="5.5" fill="#1f1f1f"/><circle cx="36" cy="24" r="2.2" fill="#9e9e9e"/>
</svg>`;

function createIcon(symbol: ObjectSymbol, state: IconState): L.Icon | L.DivIcon {
  const className = `map-icon map-icon--${state}`;

  if (symbol === 'jeep') {
    return L.divIcon({
      html: JEEP_SVG,
      className: `${className} map-icon--jeep`,
      iconSize: [44, 30],
      iconAnchor: [22, 27], // bottom centre, between the wheels
      tooltipAnchor: [0, -26],
    });
  }

  return L.icon({
    iconUrl: markerIconUrl,
    iconRetinaUrl: markerIcon2xUrl,
    shadowUrl: markerShadowUrl,
    iconSize: [25, 41],
    iconAnchor: [12, 41],
    tooltipAnchor: [0, -38],
    shadowSize: [41, 41],
    className,
  });
}

const cache = new Map<string, L.Icon | L.DivIcon>();

/** Returns a cached icon so react-leaflet does not call `setIcon` on every render. */
export function getIcon(symbol: ObjectSymbol, state: IconState): L.Icon | L.DivIcon {
  const key = `${symbol}:${state}`;
  let icon = cache.get(key);
  if (!icon) {
    icon = createIcon(symbol, state);
    cache.set(key, icon);
  }
  return icon;
}

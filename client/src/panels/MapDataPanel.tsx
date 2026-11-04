import { formatCoordinate } from '../geo';
import type { MapDataRow } from '../types';
import { Panel } from './Panel';

interface MapDataPanelProps {
  rows: MapDataRow[];
  selectedPolygonId: string | null;
  selectedObjectId: string | null;
  unsavedCount: number;
  onRowClick: (row: MapDataRow) => void;
}

/**
 * "Map Data" table with Object / Lat / Lon. It shows what is stored on the server,
 * so it changes after Save / Delete, not while drawing.
 */
export function MapDataPanel({ rows, selectedPolygonId, selectedObjectId, unsavedCount, onRowClick }: MapDataPanelProps) {
  const isSelected = (row: MapDataRow) =>
    row.kind === 'polygon' ? row.itemId === selectedPolygonId : row.itemId === selectedObjectId;

  return (
    <Panel
      title="Map Data"
      status={unsavedCount > 0 ? `${unsavedCount} unsaved item(s) not shown` : undefined}
      className="panel--grow"
    >
      <div className="table-wrapper">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Object</th>
              <th scope="col">Lat</th>
              <th scope="col">Lon</th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr>
                <td colSpan={3} className="data-table__empty">
                  No saved data yet.
                </td>
              </tr>
            ) : (
              rows.map((row) => (
                <tr
                  key={row.key}
                  className={isSelected(row) ? 'is-selected' : undefined}
                  onClick={() => onRowClick(row)}
                  tabIndex={0}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      onRowClick(row);
                    }
                  }}
                >
                  <td>
                    <span className={`kind-dot kind-dot--${row.kind}`} aria-hidden="true" />
                    {row.label}
                  </td>
                  <td className="num">{formatCoordinate(row.lat)}</td>
                  <td className="num">{formatCoordinate(row.lng)}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </Panel>
  );
}

import { Fragment, useState, type KeyboardEvent } from 'react';
import { formatCoordinate } from '../geo';
import type { LatLng, MapDataRow } from '../types';
import { Panel } from './Panel';

interface MapDataPanelProps {
  rows: MapDataRow[];
  selectedPolygonId: string | null;
  selectedObjectId: string | null;
  unsavedCount: number;
  onRowClick: (row: MapDataRow) => void;
  onVertexClick: (polygonId: string, vertex: LatLng) => void;
}

/** Enter / Space on a focused row acts like a click (rows are focusable for keyboard users). */
const activateOnKey = (action: () => void) => (e: KeyboardEvent<HTMLTableRowElement>) => {
  if (e.target !== e.currentTarget) return; // keys on the expand button are handled by the button itself
  if (e.key === 'Enter' || e.key === ' ') {
    e.preventDefault();
    action();
  }
};

/**
 * "Map Data" table with Object / Lat / Lon. It shows what is stored on the server,
 * so it changes after Save / Delete, not while drawing.
 *
 * A polygon is one row (Lat / Lon = its center); the ▸ button expands its vertices underneath.
 */
export function MapDataPanel(props: MapDataPanelProps) {
  const { rows, selectedPolygonId, selectedObjectId, unsavedCount, onRowClick, onVertexClick } = props;
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(() => new Set());

  const toggle = (polygonId: string) =>
    setExpanded((current) => {
      const next = new Set(current);
      if (!next.delete(polygonId)) next.add(polygonId);
      return next;
    });

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
              rows.map((row) => {
                const open = row.kind === 'polygon' && expanded.has(row.itemId);
                const vertexRowsId = `vertices-${row.itemId}`;

                return (
                  <Fragment key={row.key}>
                    <tr
                      className={isSelected(row) ? 'is-selected' : undefined}
                      onClick={() => onRowClick(row)}
                      onKeyDown={activateOnKey(() => onRowClick(row))}
                      tabIndex={0}
                    >
                      <td>
                        {row.kind === 'polygon' ? (
                          <button
                            type="button"
                            className="data-table__toggle"
                            aria-expanded={open}
                            aria-controls={vertexRowsId}
                            aria-label={`${open ? 'Hide' : 'Show'} vertices of ${row.label}`}
                            onClick={(e) => {
                              e.stopPropagation(); // expanding is not selecting
                              toggle(row.itemId);
                            }}
                          >
                            {open ? '▾' : '▸'}
                          </button>
                        ) : (
                          <span className="data-table__toggle-spacer" aria-hidden="true" />
                        )}
                        <span className={`kind-dot kind-dot--${row.kind}`} aria-hidden="true" />
                        {row.label}
                        {row.kind === 'polygon' && (
                          <span className="data-table__meta"> · {row.vertices.length} vertices</span>
                        )}
                      </td>
                      <td className="num" title={row.kind === 'polygon' ? 'Center of the polygon' : undefined}>
                        {formatCoordinate(row.lat)}
                      </td>
                      <td className="num" title={row.kind === 'polygon' ? 'Center of the polygon' : undefined}>
                        {formatCoordinate(row.lng)}
                      </td>
                    </tr>

                    {open &&
                      row.vertices.map((vertex, index) => (
                        <tr
                          // Vertex rows belong to the polygon row above (aria-controls points at the first one).
                          id={index === 0 ? vertexRowsId : undefined}
                          key={`${row.key}:${index}`}
                          className={`data-table__vertex${isSelected(row) ? ' is-selected' : ''}`}
                          onClick={() => onVertexClick(row.itemId, vertex)}
                          onKeyDown={activateOnKey(() => onVertexClick(row.itemId, vertex))}
                          tabIndex={0}
                        >
                          <td>vertex {index + 1}</td>
                          <td className="num">{formatCoordinate(vertex.lat)}</td>
                          <td className="num">{formatCoordinate(vertex.lng)}</td>
                        </tr>
                      ))}
                  </Fragment>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </Panel>
  );
}

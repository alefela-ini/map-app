import { useCallback, useEffect, useMemo, useState } from 'react';
import { objectsApi, polygonsApi } from './api/mapApi';
import {
  buildMapDataRows,
  nextName,
  objectFromDto,
  objectToRequest,
  polygonFromDto,
  polygonProblem,
  polygonToRequest,
} from './geo';
import { useEditableCollection } from './hooks/useEditableCollection';
import { MapView, type FocusRequest } from './map/MapView';
import { MapDataPanel } from './panels/MapDataPanel';
import { ObjectsPanel } from './panels/ObjectsPanel';
import { PolygonPanel } from './panels/PolygonPanel';
import type { InteractionMode, LatLng, MapDataRow, ObjectSymbol } from './types';

let draftCounter = 0;
/** Local id for unsaved items. (`crypto.randomUUID` is unavailable on plain-HTTP non-localhost origins.) */
const newDraftId = () => `draft-${Date.now()}-${++draftCounter}`;

const IDLE: InteractionMode = { kind: 'idle' };

/**
 * Root component: owns the interaction mode and wires the map to the three side panels.
 */
export default function App() {
  const polygons = useEditableCollection(polygonsApi, polygonFromDto, polygonToRequest);
  const objects = useEditableCollection(objectsApi, objectFromDto, objectToRequest);

  const [mode, setMode] = useState<InteractionMode>(IDLE);
  const [drawingVertices, setDrawingVertices] = useState<LatLng[]>([]);
  const [drawingProblem, setDrawingProblem] = useState<string | null>(null);
  const [symbol, setSymbol] = useState<ObjectSymbol>('marker');
  const [focus, setFocus] = useState<FocusRequest | null>(null);

  const focusOn = useCallback((points: LatLng[]) => setFocus((f) => ({ points, seq: (f?.seq ?? 0) + 1 })), []);

  // ---- Mode handling -------------------------------------------------------

  const cancelMode = useCallback(() => {
    setMode(IDLE);
    setDrawingVertices([]);
    setDrawingProblem(null);
  }, []);

  const startDrawing = () => {
    cancelMode();
    setMode({ kind: 'drawPolygon' });
  };

  const startPlacing = () => {
    cancelMode();
    setMode({ kind: 'placeObject', symbol });
  };

  // Keyboard shortcuts while drawing / placing.
  useEffect(() => {
    if (mode.kind === 'idle') return;

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        cancelMode();
      } else if (mode.kind === 'drawPolygon' && (e.key === 'Backspace' || (e.key === 'z' && (e.ctrlKey || e.metaKey)))) {
        e.preventDefault();
        setDrawingVertices((v) => v.slice(0, -1));
        setDrawingProblem(null);
      }
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [mode.kind, cancelMode]);

  // ---- Polygon drawing -----------------------------------------------------

  const addVertex = useCallback((point: LatLng) => {
    setDrawingVertices((v) => [...v, point]);
    setDrawingProblem(null);
  }, []);

  const closePolygon = () => {
    const problem = polygonProblem(drawingVertices);
    if (problem) {
      setDrawingProblem(`${problem} Press Backspace to undo vertices.`);
      return;
    }

    polygons.addDraft({
      id: newDraftId(),
      name: nextName('Polygon', polygons.items.map((p) => p.name)),
      vertices: drawingVertices,
      saved: false,
    });
    cancelMode();
  };

  // ---- Object placement ----------------------------------------------------

  const placeObject = (position: LatLng) => {
    if (mode.kind !== 'placeObject') return;
    const prefix = mode.symbol === 'jeep' ? 'Jeep' : 'Marker';

    objects.addDraft({
      id: newDraftId(),
      name: nextName(prefix, objects.items.map((o) => o.name)),
      symbol: mode.symbol,
      position,
      saved: false,
    });
    cancelMode();
  };

  // ---- Delete with confirmation ------------------------------------------

  const deleteSelectedPolygon = () => {
    const selected = polygons.selected;
    if (selected?.saved && !window.confirm(`Delete polygon "${selected.name}" from the database?`)) return;
    void polygons.deleteSelected();
  };

  const deleteAllPolygons = () => {
    if (!window.confirm('Delete ALL polygons from the map and the database?')) return;
    cancelMode();
    void polygons.deleteAll();
  };

  const deleteSelectedObject = () => {
    const selected = objects.selected;
    if (selected?.saved && !window.confirm(`Delete object "${selected.name}" from the database?`)) return;
    void objects.deleteSelected();
  };

  // ---- Map Data table ------------------------------------------------------

  const rows = useMemo(() => buildMapDataRows(polygons.saved, objects.saved), [polygons.saved, objects.saved]);

  const onRowClick = (row: MapDataRow) => {
    if (row.kind === 'polygon') {
      polygons.select(row.itemId);
      const polygon = polygons.saved.find((p) => p.id === row.itemId);
      if (polygon) focusOn(polygon.vertices);
    } else {
      objects.select(row.itemId);
      focusOn([{ lat: row.lat, lng: row.lng }]);
    }
  };

  // Warn before leaving the page with unsaved drafts.
  const unsavedCount = polygons.drafts.length + objects.drafts.length;
  useEffect(() => {
    if (unsavedCount === 0) return;
    const onBeforeUnload = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [unsavedCount]);

  return (
    <div className="layout">
      <main className="layout__map">
        <MapView
          mode={mode}
          polygons={polygons.items}
          objects={objects.items}
          selectedPolygonId={polygons.selectedId}
          selectedObjectId={objects.selectedId}
          drawingVertices={drawingVertices}
          focus={focus}
          dataLoaded={polygons.loaded && objects.loaded}
          onSelectPolygon={polygons.select}
          onSelectObject={objects.select}
          onAddVertex={addVertex}
          onClosePolygon={closePolygon}
          onPlaceObject={placeObject}
          onMoveDraftObject={(id, position) => objects.updateDraft(id, { position })}
        />
      </main>

      <aside className="layout__side">
        <PolygonPanel
          drawing={mode.kind === 'drawPolygon'}
          drawingVertexCount={drawingVertices.length}
          drawingProblem={drawingProblem}
          savedCount={polygons.saved.length}
          draftCount={polygons.drafts.length}
          selected={polygons.selected}
          busy={polygons.busy}
          error={polygons.error}
          onAdd={startDrawing}
          onCancelDrawing={cancelMode}
          onSave={() => void polygons.saveDrafts()}
          onDeleteSelected={deleteSelectedPolygon}
          onDeleteAll={deleteAllPolygons}
          onDismissError={polygons.clearError}
        />

        <ObjectsPanel
          symbol={symbol}
          placing={mode.kind === 'placeObject'}
          savedCount={objects.saved.length}
          draftCount={objects.drafts.length}
          selected={objects.selected}
          busy={objects.busy}
          error={objects.error}
          onSymbolChange={setSymbol}
          onAdd={startPlacing}
          onCancelPlacing={cancelMode}
          onSave={() => void objects.saveDrafts()}
          onDeleteSelected={deleteSelectedObject}
          onDismissError={objects.clearError}
        />

        <MapDataPanel
          rows={rows}
          selectedPolygonId={polygons.selectedId}
          selectedObjectId={objects.selectedId}
          unsavedCount={unsavedCount}
          onRowClick={onRowClick}
        />
      </aside>
    </div>
  );
}

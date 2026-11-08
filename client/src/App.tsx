import { useCallback, useEffect, useMemo, useState } from 'react';
import { subscribeToChanges } from './api/changes';
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

/**
 * Random id for an unsaved item. It is also sent as `clientId`, the idempotency key that makes a retried Save
 * safe, so it must be unique across tabs and users: 128 random bits, not a timestamp + counter.
 * (`crypto.randomUUID` is unavailable on plain-HTTP non-localhost origins; `getRandomValues` works everywhere.)
 */
const newDraftId = () =>
  `draft-${Array.from(crypto.getRandomValues(new Uint8Array(16)), (b) => b.toString(16).padStart(2, '0')).join('')}`;

const IDLE: InteractionMode = { kind: 'idle' };

/**
 * Root component: owns the interaction mode and wires the map to the three side panels.
 */
export default function App() {
  const polygons = useEditableCollection(polygonsApi, polygonFromDto, polygonToRequest);
  const objects = useEditableCollection(objectsApi, objectFromDto, objectToRequest);

  // Live updates: reload a collection when anyone (another user or this tab) changes it on the server,
  // and both whenever the stream (re)connects, because changes made before that were not announced to us.
  const { refresh: refreshPolygons } = polygons;
  const { refresh: refreshObjects } = objects;
  useEffect(
    () =>
      subscribeToChanges(
        (topic) => (topic === 'polygons' ? refreshPolygons() : refreshObjects()),
        () => {
          refreshPolygons();
          refreshObjects();
        },
      ),
    [refreshPolygons, refreshObjects],
  );

  const [mode, setMode] = useState<InteractionMode>(IDLE);
  const [drawingVertices, setDrawingVertices] = useState<LatLng[]>([]);
  const [drawingProblem, setDrawingProblem] = useState<string | null>(null);
  const [focus, setFocus] = useState<FocusRequest | null>(null);

  const focusOn = useCallback((points: LatLng[]) => setFocus((f) => ({ points, seq: (f?.seq ?? 0) + 1 })), []);

  // ---- Selection: one item at a time, across polygons and objects ----------
  // Each panel's Delete acts on its own selection; allowing a polygon AND an object to be selected at once
  // made it unclear what would be deleted. A click on empty map or Esc clears the selection.

  const { select: setPolygonSelection } = polygons;
  const { select: setObjectSelection } = objects;

  // Only SAVED items can be selected (selection exists for Delete), and only while that panel has no unsaved
  // drafts: first Save or Cancel them. Otherwise "adding" and "deleting" states would mix in one panel.
  const polygonsPending = polygons.drafts.length > 0;
  const objectsPending = objects.drafts.length > 0;

  const selectPolygon = useCallback(
    (id: string) => {
      if (polygonsPending) return;
      setPolygonSelection(id);
      setObjectSelection(null);
    },
    [polygonsPending, setPolygonSelection, setObjectSelection],
  );

  const selectObject = useCallback(
    (id: string) => {
      if (objectsPending) return;
      setObjectSelection(id);
      setPolygonSelection(null);
    },
    [objectsPending, setPolygonSelection, setObjectSelection],
  );

  const clearSelection = useCallback(() => {
    setPolygonSelection(null);
    setObjectSelection(null);
  }, [setPolygonSelection, setObjectSelection]);

  // ---- Mode handling -------------------------------------------------------

  const cancelMode = useCallback(() => {
    setMode(IDLE);
    setDrawingVertices([]);
    setDrawingProblem(null);
  }, []);

  // Adding and selecting are separate states: starting to add clears any selection.
  const startDrawing = () => {
    cancelMode();
    clearSelection();
    setMode({ kind: 'drawPolygon' });
  };

  const startPlacing = (symbol: ObjectSymbol) => {
    cancelMode();
    clearSelection();
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

  // Esc outside drawing / placing clears the selection.
  useEffect(() => {
    if (mode.kind !== 'idle') return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') clearSelection();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [mode.kind, clearSelection]);

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
    cancelMode(); // one object per Add: further map clicks do nothing
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
      selectPolygon(row.itemId);
      const polygon = polygons.saved.find((p) => p.id === row.itemId);
      if (polygon) focusOn(polygon.vertices);
    } else {
      selectObject(row.itemId);
      focusOn([{ lat: row.lat, lng: row.lng }]);
    }
  };

  /** A vertex row of an expanded polygon: select the polygon and fly to that vertex. */
  const onVertexClick = (polygonId: string, vertex: LatLng) => {
    selectPolygon(polygonId);
    focusOn([vertex]);
  };

  // Warn before leaving the page with unsaved drafts or a polygon that is still being drawn.
  const unsavedCount = polygons.drafts.length + objects.drafts.length;
  const hasUnsavedWork = unsavedCount > 0 || drawingVertices.length > 0;
  useEffect(() => {
    if (!hasUnsavedWork) return;
    const onBeforeUnload = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [hasUnsavedWork]);

  // Buttons stay disabled until the first load finishes: draft names ("Polygon 3") are derived from the loaded list.
  // Correctness also relies on it: Save returns only the saved items, so a Save must never overtake the initial GET
  // (its late list would be ignored as stale by responseOrder and the older items would be missing until a reload).
  const polygonsBusy = polygons.busy || !polygons.loaded;
  const objectsBusy = objects.busy || !objects.loaded;

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
          onSelectPolygon={selectPolygon}
          onSelectObject={selectObject}
          onClearSelection={clearSelection}
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
          busy={polygonsBusy}
          error={polygons.error}
          onAdd={startDrawing}
          onCancelDrawing={cancelMode}
          onSave={() => void polygons.saveDrafts()}
          onDiscardDrafts={polygons.discardDrafts}
          onDeleteSelected={deleteSelectedPolygon}
          onDeleteAll={deleteAllPolygons}
          onDismissError={polygons.clearError}
        />

        <ObjectsPanel
          placingSymbol={mode.kind === 'placeObject' ? mode.symbol : null}
          savedCount={objects.saved.length}
          draftCount={objects.drafts.length}
          selected={objects.selected}
          busy={objectsBusy}
          error={objects.error}
          onAdd={startPlacing}
          onCancelPlacing={cancelMode}
          onSave={() => void objects.saveDrafts()}
          onDiscardDrafts={objects.discardDrafts}
          onDeleteSelected={deleteSelectedObject}
          onDismissError={objects.clearError}
        />

        <MapDataPanel
          rows={rows}
          selectedPolygonId={polygons.selectedId}
          selectedObjectId={objects.selectedId}
          unsavedCount={unsavedCount}
          onRowClick={onRowClick}
          onVertexClick={onVertexClick}
        />
      </aside>
    </div>
  );
}

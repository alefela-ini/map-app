import type { MapPolygon } from '../types';
import { Message, Panel } from './Panel';

interface PolygonPanelProps {
  drawing: boolean;
  drawingVertexCount: number;
  drawingProblem: string | null;
  savedCount: number;
  draftCount: number;
  selected: MapPolygon | null;
  busy: boolean;
  error: string | null;
  onAdd: () => void;
  onCancelDrawing: () => void;
  /** Throws away the unsaved polygons. */
  onDiscardDrafts: () => void;
  onSave: () => void;
  onDeleteSelected: () => void;
  onDeleteAll: () => void;
  onDismissError: () => void;
}

/** Polygon management: Add (drawing mode), Save, Delete selected / Delete all. */
export function PolygonPanel(props: PolygonPanelProps) {
  const { drawing, drawingVertexCount, savedCount, draftCount, selected, busy } = props;
  const hasAny = savedCount + draftCount > 0;

  return (
    <Panel title="Polygon" status={`${savedCount} saved · ${draftCount} unsaved`}>
      <div className="toolbar">
        {drawing ? (
          <button type="button" onClick={props.onCancelDrawing}>
            Cancel
          </button>
        ) : (
          <button
            type="button"
            className="primary"
            onClick={props.onAdd}
            disabled={busy || selected !== null}
            title={selected ? 'Clear the selection first: Esc or click on empty map' : undefined}
          >
            Add
          </button>
        )}
        {/* Deleting is its own state: hidden while drawing and while there are unsaved polygons (Save / Cancel). */}
        {!drawing && draftCount === 0 && (
          <>
            <button
              type="button"
              className="danger"
              onClick={props.onDeleteSelected}
              disabled={busy || !selected}
              title={selected ? `Delete ${selected.name}` : 'Select a polygon on the map or in the table first'}
            >
              Delete
            </button>
            <button type="button" className="danger" onClick={props.onDeleteAll} disabled={busy || !hasAny}>
              Delete all
            </button>
          </>
        )}
      </div>

      {/* Second row, as on the mockup: Save / Cancel, only when there is something unsaved.
          Cancel simply throws the unsaved items away. */}
      {!drawing && draftCount > 0 && (
        <div className="toolbar">
          <button type="button" onClick={props.onSave} disabled={busy}>
            Save ({draftCount})
          </button>
          <button type="button" onClick={props.onDiscardDrafts} disabled={busy}>
            Cancel
          </button>
        </div>
      )}

      {drawing && (
        <Message kind="info">
          {drawingVertexCount < 3
            ? `Click on the map to add vertices (${drawingVertexCount} so far).`
            : `${drawingVertexCount} vertices — click the first vertex to close the polygon.`}{' '}
          <kbd>Backspace</kbd> undo, <kbd>Esc</kbd> cancel.
        </Message>
      )}
      {props.drawingProblem && <Message kind="error">{props.drawingProblem}</Message>}
      {!drawing && selected && (
        <Message kind="info">
          Selected: <strong>{selected.name}</strong>
        </Message>
      )}
      {props.error && (
        <Message kind="error" onDismiss={props.onDismissError}>
          {props.error}
        </Message>
      )}
    </Panel>
  );
}

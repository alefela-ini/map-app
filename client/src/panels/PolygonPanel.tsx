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
          <button type="button" className="primary" onClick={props.onAdd} disabled={busy}>
            Add
          </button>
        )}
        <button type="button" onClick={props.onSave} disabled={busy || draftCount === 0}>
          Save{draftCount > 0 ? ` (${draftCount})` : ''}
        </button>
        <button type="button" className="danger" onClick={props.onDeleteSelected} disabled={busy || !selected}>
          Delete
        </button>
        <button type="button" className="danger" onClick={props.onDeleteAll} disabled={busy || !hasAny}>
          Delete all
        </button>
      </div>

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
          {selected.saved ? '' : ' (unsaved)'}
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

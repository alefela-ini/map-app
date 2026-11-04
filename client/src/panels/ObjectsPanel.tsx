import { OBJECT_SYMBOLS, type MapObject, type ObjectSymbol } from '../types';
import { Message, Panel } from './Panel';

interface ObjectsPanelProps {
  symbol: ObjectSymbol;
  placing: boolean;
  savedCount: number;
  draftCount: number;
  selected: MapObject | null;
  busy: boolean;
  error: string | null;
  onSymbolChange: (symbol: ObjectSymbol) => void;
  onAdd: () => void;
  onCancelPlacing: () => void;
  onSave: () => void;
  onDeleteSelected: () => void;
  onDismissError: () => void;
}

/** Object management: choose Marker / Jeep, Add (placement mode), Save, Delete selected. */
export function ObjectsPanel(props: ObjectsPanelProps) {
  const { symbol, placing, savedCount, draftCount, selected, busy } = props;

  return (
    <Panel title="Objects" status={`${savedCount} saved · ${draftCount} unsaved`}>
      <fieldset className="symbol-picker" disabled={placing}>
        <legend>Symbol</legend>
        {OBJECT_SYMBOLS.map((option) => (
          <label key={option.value}>
            <input
              type="radio"
              name="object-symbol"
              value={option.value}
              checked={symbol === option.value}
              onChange={() => props.onSymbolChange(option.value)}
            />
            {option.label}
          </label>
        ))}
      </fieldset>

      <div className="toolbar">
        {placing ? (
          <button type="button" onClick={props.onCancelPlacing}>
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
      </div>

      {placing && (
        <Message kind="info">
          Click on the map to place the {symbol === 'jeep' ? 'Jeep' : 'Marker'}. <kbd>Esc</kbd> cancel.
        </Message>
      )}
      {!placing && selected && (
        <Message kind="info">
          Selected: <strong>{selected.name}</strong>
          {selected.saved ? '' : ' (unsaved — drag to move)'}
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

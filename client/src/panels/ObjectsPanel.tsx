import { useEffect, useRef, useState, type ReactNode } from 'react';
import { JEEP_SVG, MARKER_ICON_URL } from '../map/icons';
import { OBJECT_SYMBOLS, type MapObject, type ObjectSymbol } from '../types';
import { Message, Panel } from './Panel';

interface ObjectsPanelProps {
  /** The symbol being placed, or `null` when not in placement mode. */
  placingSymbol: ObjectSymbol | null;
  savedCount: number;
  draftCount: number;
  selected: MapObject | null;
  busy: boolean;
  error: string | null;
  onAdd: (symbol: ObjectSymbol) => void;
  onCancelPlacing: () => void;
  onSave: () => void;
  /** Throws away the unsaved objects. */
  onDiscardDrafts: () => void;
  onDeleteSelected: () => void;
  onDismissError: () => void;
}

const SYMBOL_PREVIEW: Record<ObjectSymbol, ReactNode> = {
  marker: <img src={MARKER_ICON_URL} alt="" width={14} height={23} />,
  // Static markup from icons.ts (no user input), the same drawing as on the map.
  jeep: <span className="add-menu__jeep" dangerouslySetInnerHTML={{ __html: JEEP_SVG }} />,
};

/**
 * Object management. Add opens a small menu (Marker / Jeep); choosing one switches the map to placement mode,
 * the next click places ONE object and ends it. Unsaved objects then offer Save or Cancel (throw them away).
 * Save sends the unsaved objects, Delete removes the selected object.
 */
export function ObjectsPanel(props: ObjectsPanelProps) {
  const { placingSymbol, savedCount, draftCount, selected, busy } = props;
  const placing = placingSymbol !== null;
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  // Close the menu on Esc or on a click anywhere outside it; focus the first choice when it opens.
  useEffect(() => {
    if (!menuOpen) return;
    menuRef.current?.querySelector<HTMLButtonElement>('[role="menuitem"]')?.focus();

    const onPointerDown = (e: PointerEvent) => {
      if (!menuRef.current?.contains(e.target as Node)) setMenuOpen(false);
    };
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setMenuOpen(false);
    };
    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [menuOpen]);

  const choose = (symbol: ObjectSymbol) => {
    setMenuOpen(false);
    props.onAdd(symbol);
  };

  return (
    <Panel title="Objects" status={`${savedCount} saved · ${draftCount} unsaved`}>
      <div className="toolbar">
        {placing ? (
          <button type="button" onClick={props.onCancelPlacing}>
            Cancel
          </button>
        ) : (
          <div className="add-menu" ref={menuRef}>
            <button
              type="button"
              className="primary"
              onClick={() => setMenuOpen((open) => !open)}
              disabled={busy || selected !== null}
              title={selected ? 'Clear the selection first: Esc or click on empty map' : undefined}
              aria-haspopup="menu"
              aria-expanded={menuOpen}
            >
              Add ▾
            </button>
            {menuOpen && (
              <div className="add-menu__list" role="menu" aria-label="Object to add">
                {OBJECT_SYMBOLS.map((option) => (
                  <button key={option.value} type="button" role="menuitem" onClick={() => choose(option.value)}>
                    <span className="add-menu__icon" aria-hidden="true">
                      {SYMBOL_PREVIEW[option.value]}
                    </span>
                    {option.label}
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
        {/* Deleting is its own state: hidden while placing and while there are unsaved objects (Save / Cancel). */}
        {!placing && draftCount === 0 && (
          <button
            type="button"
            className="danger"
            onClick={props.onDeleteSelected}
            disabled={busy || !selected}
            title={selected ? `Delete ${selected.name}` : 'Select an object on the map or in the table first'}
          >
            Delete
          </button>
        )}
      </div>

      {/* Second row, as on the mockup: Save / Cancel, only when there is something unsaved.
          Cancel simply throws the unsaved items away. */}
      {!placing && draftCount > 0 && (
        <div className="toolbar">
          <button type="button" onClick={props.onSave} disabled={busy}>
            Save ({draftCount})
          </button>
          <button type="button" onClick={props.onDiscardDrafts} disabled={busy}>
            Cancel
          </button>
        </div>
      )}

      {placing && (
        <Message kind="info">
          Click on the map to place the {placingSymbol === 'jeep' ? 'Jeep' : 'Marker'}. <kbd>Esc</kbd> cancel.
        </Message>
      )}
      {!placing && selected && (
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

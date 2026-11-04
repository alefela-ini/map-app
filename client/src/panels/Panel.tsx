import type { ReactNode } from 'react';

interface PanelProps {
  title: string;
  /** Short status shown next to the title, e.g. "3 saved · 1 unsaved". */
  status?: string;
  className?: string;
  children: ReactNode;
}

/** A titled card used by the right-hand side panels. */
export function Panel({ title, status, className, children }: PanelProps) {
  return (
    <section className={`panel ${className ?? ''}`} aria-label={title}>
      <header className="panel__header">
        <h2>{title}</h2>
        {status && <span className="panel__status">{status}</span>}
      </header>
      {children}
    </section>
  );
}

/** A one-line message under the panel buttons. */
export function Message({ kind, children, onDismiss }: { kind: 'info' | 'error'; children: ReactNode; onDismiss?: () => void }) {
  return (
    <p className={`message message--${kind}`} role={kind === 'error' ? 'alert' : 'status'}>
      <span>{children}</span>
      {onDismiss && (
        <button type="button" className="message__close" onClick={onDismiss} aria-label="Dismiss">
          ×
        </button>
      )}
    </p>
  );
}

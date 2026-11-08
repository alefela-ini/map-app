/**
 * Live updates: the server's Server-Sent Events stream (`GET /api/events`) says which collection changed;
 * the client then reloads that collection with the normal GET. Events carry no data on purpose, so a lost or
 * duplicated event can never leave the client with wrong data, only with data that is reloaded once more.
 */
import { API_BASE_URL } from './http';

export type ChangeTopic = 'polygons' | 'objects';

/** Pause before re-opening a stream that the browser closed for good (see `onerror` below). */
const RECONNECT_DELAY_MS = 3000;

/**
 * Calls `onChange(topic)` for every change made by anyone (this tab included), and `onConnected()` every time the
 * stream opens, the first time included: changes made before that moment (between the initial GET and the
 * subscription, or while disconnected) produced events this client never saw, so the caller reloads everything.
 * Returns a function that closes the stream.
 */
export function subscribeToChanges(onChange: (topic: ChangeTopic) => void, onConnected: () => void): () => void {
  if (typeof EventSource === 'undefined') return () => {};

  let source: EventSource | null = null;
  let retryTimer: ReturnType<typeof setTimeout> | undefined;
  let closed = false;

  const connect = () => {
    source = new EventSource(`${API_BASE_URL}/api/events`);

    source.addEventListener('changed', (event) => {
      const topic = (event as MessageEvent<string>).data;
      if (topic === 'polygons' || topic === 'objects') onChange(topic);
    });

    source.onopen = () => onConnected();

    source.onerror = () => {
      // After a network error the browser reconnects by itself. After an HTTP error (e.g. 502 from nginx while
      // the API restarts) the HTML spec makes it give up for good (readyState CLOSED); Chrome retries anyway,
      // other browsers may not. In that case open a new stream ourselves.
      if (source?.readyState === EventSource.CLOSED && !closed) {
        source.close();
        retryTimer = setTimeout(connect, RECONNECT_DELAY_MS);
      }
    };
  };

  connect();

  return () => {
    closed = true;
    clearTimeout(retryTimer);
    source?.close();
  };
}

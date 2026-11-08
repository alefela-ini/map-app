import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { subscribeToChanges, type ChangeTopic } from './changes';

/** Minimal stand-in for the browser's EventSource (the tests run in Node). */
class FakeEventSource {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSED = 2;
  static instances: FakeEventSource[] = [];

  readyState = FakeEventSource.CONNECTING;
  onopen: (() => void) | null = null;
  onerror: (() => void) | null = null;
  private readonly listeners = new Map<string, (event: { data: string }) => void>();

  readonly url: string;

  constructor(url: string) {
    this.url = url;
    FakeEventSource.instances.push(this);
  }

  addEventListener(type: string, listener: (event: { data: string }) => void) {
    this.listeners.set(type, listener);
  }

  close() {
    this.readyState = FakeEventSource.CLOSED;
  }

  // Test helpers
  open() {
    this.readyState = FakeEventSource.OPEN;
    this.onopen?.();
  }

  emit(type: string, data: string) {
    this.listeners.get(type)?.({ data });
  }

  fail(givesUp: boolean) {
    this.readyState = givesUp ? FakeEventSource.CLOSED : FakeEventSource.CONNECTING;
    this.onerror?.();
  }
}

describe('subscribeToChanges', () => {
  let changes: ChangeTopic[];
  let connects: number;
  let unsubscribe: () => void;

  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal('EventSource', FakeEventSource);
    FakeEventSource.instances = [];
    changes = [];
    connects = 0;
    unsubscribe = subscribeToChanges(
      (topic) => changes.push(topic),
      () => connects++,
    );
  });

  afterEach(() => {
    unsubscribe();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('reports which collection changed and ignores unknown topics', () => {
    const source = FakeEventSource.instances[0];
    source.open();
    source.emit('changed', 'objects');
    source.emit('changed', 'polygons');
    source.emit('changed', 'something-else');

    expect(source.url).toBe('/api/events');
    expect(changes).toEqual(['objects', 'polygons']);
  });

  it('asks for a full reload on the FIRST open too (changes between the initial GET and the subscription)', () => {
    FakeEventSource.instances[0].open();
    expect(connects).toBe(1);
  });

  it('asks for a full reload when the browser reconnects after a network error', () => {
    const source = FakeEventSource.instances[0];
    source.open();
    source.fail(false); // the browser keeps the same EventSource and retries
    source.open();

    expect(FakeEventSource.instances).toHaveLength(1);
    expect(connects).toBe(2);
  });

  it('opens a new stream itself when the browser gives up (HTTP error such as 502)', () => {
    FakeEventSource.instances[0].open();
    FakeEventSource.instances[0].fail(true);
    expect(FakeEventSource.instances).toHaveLength(1);

    vi.advanceTimersByTime(3000);
    expect(FakeEventSource.instances).toHaveLength(2);
    FakeEventSource.instances[1].open();
    expect(connects).toBe(2);
  });

  it('stops reconnecting after unsubscribe', () => {
    FakeEventSource.instances[0].fail(true);
    unsubscribe();
    vi.advanceTimersByTime(10_000);

    expect(FakeEventSource.instances).toHaveLength(1);
  });
});

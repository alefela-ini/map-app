import { useCallback, useEffect, useMemo, useState } from 'react';
import type { CollectionApi } from '../api/mapApi';
import { ApiError, describeError } from '../api/http';
import type { EditableItem } from '../types';

/**
 * State for one kind of map item (polygons or objects):
 * - `saved`  — what the server returned (the source of truth for the Map Data table),
 * - `drafts` — items created in the browser and not yet saved,
 * - a single selection, a busy flag and the last error.
 *
 * `api`, `fromDto` and `toRequest` must be stable references (module-level constants).
 */
export function useEditableCollection<TItem extends EditableItem, TDto, TRequest>(
  api: CollectionApi<TDto, TRequest>,
  fromDto: (dto: TDto) => TItem,
  toRequest: (item: TItem) => TRequest,
) {
  const [saved, setSaved] = useState<TItem[]>([]);
  const [drafts, setDrafts] = useState<TItem[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);

  const reload = useCallback(
    async (signal?: AbortSignal) => {
      const dtos = await api.list(signal);
      setSaved(dtos.map(fromDto));
      setLoaded(true);
    },
    [api, fromDto],
  );

  // Initial load. The abort guard also covers React StrictMode's double effect run.
  useEffect(() => {
    const controller = new AbortController();
    // Fetching from the server is synchronization with an external system; state is set after the response.
    // oxlint-disable-next-line react/set-state-in-effect
    reload(controller.signal).catch((e: unknown) => {
      if (!controller.signal.aborted) setError(describeError(e));
    });
    return () => controller.abort();
  }, [reload]);

  /** Runs a server action with a busy flag and error reporting. */
  const run = useCallback(async (action: () => Promise<void>, itemNames?: readonly string[]) => {
    setBusy(true);
    setError(null);
    try {
      await action();
    } catch (e) {
      setError(describeError(e, itemNames));
    } finally {
      setBusy(false);
    }
  }, []);

  const addDraft = useCallback((item: TItem) => {
    setDrafts((current) => [...current, item]);
    setSelectedId(item.id);
  }, []);

  const updateDraft = useCallback((id: string, patch: Partial<TItem>) => {
    setDrafts((current) => current.map((item) => (item.id === id ? { ...item, ...patch } : item)));
  }, []);

  /** POSTs all drafts; on success the server's full list replaces the local one. */
  const saveDrafts = useCallback(async () => {
    if (drafts.length === 0) return;
    const batch = drafts;
    const batchIds = new Set(batch.map((d) => d.id));

    await run(async () => {
      const result = await api.create(batch.map(toRequest));
      setSaved(result.map(fromDto));
      // Keep drafts created while the request was in flight.
      setDrafts((current) => current.filter((d) => !batchIds.has(d.id)));
      setSelectedId((current) => (current !== null && batchIds.has(current) ? null : current));
    }, batch.map((d) => d.name));
  }, [api, drafts, fromDto, run, toRequest]);

  /** Deletes the selected item: drafts locally, saved items on the server. */
  const deleteSelected = useCallback(async () => {
    if (selectedId === null) return;

    if (drafts.some((d) => d.id === selectedId)) {
      setDrafts((current) => current.filter((d) => d.id !== selectedId));
      setSelectedId(null);
      return;
    }

    await run(async () => {
      try {
        await api.remove(selectedId);
      } catch (e) {
        // Already deleted (e.g. from another tab) — the goal is reached.
        if (!(e instanceof ApiError && e.status === 404)) throw e;
      }
      setSelectedId(null);
      await reload();
    });
  }, [api, drafts, reload, run, selectedId]);

  /** Deletes every item: all drafts and everything on the server. */
  const deleteAll = useCallback(async () => {
    await run(async () => {
      await api.removeAll();
      setDrafts([]);
      setSelectedId(null);
      await reload();
    });
  }, [api, reload, run]);

  const items = useMemo(() => [...saved, ...drafts], [saved, drafts]);
  const selected = useMemo(() => items.find((i) => i.id === selectedId) ?? null, [items, selectedId]);

  return {
    saved,
    drafts,
    items,
    selected,
    selectedId,
    select: setSelectedId,
    busy,
    loaded,
    error,
    clearError: useCallback(() => setError(null), []),
    addDraft,
    updateDraft,
    saveDrafts,
    deleteSelected,
    deleteAll,
  };
}

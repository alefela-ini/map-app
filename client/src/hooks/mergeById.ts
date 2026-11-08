/**
 * Adds `incoming` items to `current`, replacing items with the same id. Used for a Save response, which
 * contains only the saved items: merging (not appending) keeps it correct if a reload already brought them,
 * or if a retried Save returns items that are already shown.
 */
export function mergeById<T extends { id: string }>(current: readonly T[], incoming: readonly T[]): T[] {
  const byId = new Map(incoming.map((item) => [item.id, item]));
  const merged = current.map((item) => byId.get(item.id) ?? item);
  const known = new Set(current.map((item) => item.id));
  return [...merged, ...incoming.filter((item) => !known.has(item.id))];
}

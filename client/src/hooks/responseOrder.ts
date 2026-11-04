/**
 * Keeps out-of-order responses from overwriting newer data.
 *
 * Several requests return the full list (the initial GET, every Save, the reload after Delete). They can
 * finish in a different order than they started: a slow initial GET may arrive after a Save, and would then
 * replace the fresh list with an old one. Each request takes a ticket when it starts; its response is applied
 * only if no request that started later has been applied already.
 */
export interface ResponseOrder {
  /** Call when a request starts; pass the ticket to {@link tryApply} when its response arrives. */
  next(): number;
  /** `true` if the response with this ticket is the newest so far and should be applied. */
  tryApply(ticket: number): boolean;
  /** `true` once any response has been applied. */
  readonly hasApplied: boolean;
}

export function createResponseOrder(): ResponseOrder {
  let issued = 0;
  let applied = 0;

  return {
    next: () => ++issued,
    tryApply(ticket) {
      if (ticket <= applied) return false;
      applied = ticket;
      return true;
    },
    get hasApplied() {
      return applied > 0;
    },
  };
}

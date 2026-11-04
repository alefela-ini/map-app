import { describe, expect, it } from 'vitest';
import { createResponseOrder } from './responseOrder';

describe('createResponseOrder', () => {
  it('ignores a slow initial GET that arrives after a Save', () => {
    const order = createResponseOrder();
    const initialGet = order.next(); // started first, but slow
    const save = order.next(); // started later

    expect(order.tryApply(save)).toBe(true); // Save response shown
    expect(order.tryApply(initialGet)).toBe(false); // late GET must not overwrite it
  });

  it('applies responses that arrive in order', () => {
    const order = createResponseOrder();
    const first = order.next();
    const second = order.next();

    expect(order.tryApply(first)).toBe(true);
    expect(order.tryApply(second)).toBe(true);
  });

  it('reports whether anything has been applied', () => {
    const order = createResponseOrder();
    const ticket = order.next();

    expect(order.hasApplied).toBe(false);
    order.tryApply(ticket);
    expect(order.hasApplied).toBe(true);
  });
});

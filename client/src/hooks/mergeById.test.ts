import { describe, expect, it } from 'vitest';
import { mergeById } from './mergeById';

const item = (id: string, name = id) => ({ id, name });

describe('mergeById', () => {
  it('appends new items after the existing ones', () => {
    expect(mergeById([item('a'), item('b')], [item('c')]).map((i) => i.id)).toEqual(['a', 'b', 'c']);
  });

  it('does not duplicate items that are already shown (e.g. a reload got there first)', () => {
    const merged = mergeById([item('a'), item('b', 'old')], [item('b', 'new'), item('c')]);
    expect(merged).toEqual([item('a'), item('b', 'new'), item('c')]);
  });
});

import { describe, expect, it } from 'vitest';
import { rooted } from './sources';

describe('a gate over the whole tree (STRUCT5c, D166)', () => {
  it('names a rooted glob’s sources from the sources’ top, wherever the gate sits', () => {
    expect(rooted({ '/src/work/Composer.tsx': 1, '/src/main.tsx': 2 })).toEqual({ './work/Composer.tsx': 1, './main.tsx': 2 });
    expect(Object.keys(rooted(import.meta.glob('/src/test/sources.ts')))).toEqual(['./test/sources.ts']);
  });
});

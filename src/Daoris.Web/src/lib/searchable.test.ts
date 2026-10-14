import { describe, expect, it } from 'vitest';
import { searchable } from './searchable';

describe('a search’s words', () => {
  it('searches by two characters, or by one Han character, as the driver does (`SessionEvents.Searchable`)', () => {
    for (const words of ['ab', ' a b ', '树', ' 区 ', '㐀', '豈', '𠀀']) expect(searchable(words), words).toBe(true);
    // One letter of any other script would find nearly everything; kana is not Han.
    for (const words of ['a', ' a ', 'あ', 'é', '', '   ']) expect(searchable(words), words).toBe(false);
  });
});

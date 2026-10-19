import { describe, expect, it } from 'vitest';
import { MENTION_OPTIONS, mentionAt, rankMentions, spellMention, withMention } from './mentions';

describe('a mention being written (CONV4d)', () => {
  it('is an @ at the start or after a space, up to the caret', () => {
    expect(mentionAt('@', 1)).toEqual({ start: 0, end: 1, query: '' });
    expect(mentionAt('read @src/en', 12)).toEqual({ start: 5, end: 12, query: 'src/en' });
    expect(mentionAt('line one\n@READ', 14)).toEqual({ start: 9, end: 14, query: 'READ' });
  });

  it('is not an address, nor a mention the caret has left', () => {
    expect(mentionAt('mail me@example.com', 19)).toBeNull();
    expect(mentionAt('@plain.md and then', 18)).toBeNull();
    expect(mentionAt('no mention here', 15)).toBeNull();
  });

  it('runs to the end of the word under the caret, so accepting replaces all of it', () => {
    // The caret after `@pl` in `@plain.md`: the whole word is the mention.
    expect(mentionAt('see @plain.md now', 7)).toEqual({ start: 4, end: 13, query: 'pl' });
  });

  it('reads a quoted mention through its spaces, as both doors do', () => {
    expect(mentionAt('see @"my no', 11)).toEqual({ start: 4, end: 11, query: 'my no' });
    // A closed quote ends it, and the closing quote is the mention's own.
    expect(mentionAt('see @"my notes.md" now', 9)).toEqual({ start: 4, end: 18, query: 'my ' });
    expect(mentionAt('see @"my notes.md" now', 22)).toBeNull();
  });
});

describe('what the list offers', () => {
  const tree = [
    'README.md',
    'docs/design.md',
    'docs/thread.md',
    'src/engine/render.ts',
    'src/engine/readers.ts',
    'src/main.ts',
    'tools/gather.mjs',
  ];

  it('puts a name that starts with what was typed first, then one containing it, then a path', () => {
    expect(rankMentions(tree, 'read')).toEqual([
      // the name starts with it, shortest first
      'README.md',
      'src/engine/readers.ts',
      // the name holds it
      'docs/thread.md',
    ]);
    expect(rankMentions(tree, 'engine/r')).toEqual(['src/engine/render.ts', 'src/engine/readers.ts']);
  });

  it('matches letters in order when nothing holds the text whole', () => {
    expect(rankMentions(tree, 'smn')).toEqual(['src/main.ts']);
    expect(rankMentions(tree, 'zzz')).toEqual([]);
  });

  /**
   * Seen on the window (CONV4d): `des` offered the one file it meant, then seven whose letters were
   * scattered across folder names (`.claude/rules/…`). Letters in order across a whole path are the
   * last resort, taken only when nothing better matched; within a name they still count.
   */
  it('takes letters scattered across folders only when nothing better matched', () => {
    const rules = ['.claude/rules/sensitive-info.md', 'docs/design notes.md', 'src/readers.ts'];
    expect(rankMentions(rules, 'des')).toEqual(['docs/design notes.md', 'src/readers.ts']);
    expect(rankMentions(['.claude/rules/sensitive-info.md'], 'des')).toEqual(['.claude/rules/sensitive-info.md']);
  });

  it('ignores case, and takes a backslash as the slash a Windows hand types', () => {
    expect(rankMentions(tree, 'DOCS\\DES')).toEqual(['docs/design.md']);
  });

  it('offers the shallowest files when nothing is typed yet, and never more than the list holds', () => {
    expect(rankMentions(tree, '')[0]).toBe('README.md');
    const many = Array.from({ length: 40 }, (_, i) => `f${String(i).padStart(2, '0')}.md`);
    expect(rankMentions(many, 'f')).toHaveLength(MENTION_OPTIONS);
  });
});

describe('what goes into the box', () => {
  it('spells a path as both doors read it: bare, or quoted when it holds a space', () => {
    expect(spellMention('docs/design.md')).toBe('@docs/design.md');
    expect(spellMention('docs/deep file.md')).toBe('@"docs/deep file.md"');
    expect(spellMention('笔记.md')).toBe('@笔记.md');
  });

  it('replaces the mention being written and leaves the caret after one space', () => {
    const text = 'look at @des please';
    const mention = mentionAt(text, 12)!;
    expect(withMention(text, mention, 'docs/design.md'))
      .toEqual({ text: 'look at @docs/design.md please', caret: 24 });

    const end = 'look at @my';
    expect(withMention(end, mentionAt(end, 11)!, 'my notes.md'))
      .toEqual({ text: 'look at @"my notes.md" ', caret: 23 });
  });
});

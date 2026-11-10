import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { NotePart } from '../data/api';
import { noteLines } from './noteLines';

const coded = (code: string, text: string): NotePart => ({ code, values: {}, text });

describe('the place a conversation working in place was told', () => {
  it('reads under its own lead in both languages, never the browser’s, the section as written', () => {
    const told = coded('chat.told-place', 'told the agent its place:');
    const place: NotePart = { words: '## Your place\n\nYou work in this folder in place. It is not a git repository: …', by: 'program' };
    expect(noteLines(i18n.getFixedT('en'), { parts: [told, place] }, 'en')).toEqual([
      { kind: 'said', text: 'The agent was told about the place it works in:' },
      { kind: 'words', text: place.words, by: 'program' },
    ]);
    expect(noteLines(i18n.getFixedT('zh'), { parts: [told] }, 'zh')[0].text).toBe('已告知智能体它工作的位置：');
  });
});

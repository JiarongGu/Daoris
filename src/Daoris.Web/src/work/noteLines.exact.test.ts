import { expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { noteLines } from './noteLines';

it('words where a quest marked exact ran, the role by its name as written', () => {
  const parts = [{ code: 'opening.exact', values: { role: 'Exact build' }, text: 'Exact build: it ran on Exact build’s order.' }];
  expect(noteLines(i18n.getFixedT('en'), { parts }, 'en')).toEqual([{ kind: 'said', text: "Exact build: it ran on Exact build's order." }]);
  expect(noteLines(i18n.getFixedT('zh'), { parts }, 'zh')).toEqual([{ kind: 'said', text: '精确开发：按Exact build的顺序运行。' }]);
});

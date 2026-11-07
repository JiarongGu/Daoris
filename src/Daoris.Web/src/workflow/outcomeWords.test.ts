import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { drawnFromJson, graphFlow } from './graphFlow';
import { howSaid, rowSaid, waySaid } from './edit/editWords';
import type { DraftWire } from './edit/graphDraft';

// The outcomes the monitor and the person's task declare are said in the page's words at the editor's and the chart's callers
// (`workflow.outcome.<kind>.<outcome>`), in both languages; a kind the page has no words for says the raw name, which stays the identity.

const t = i18n.t.bind(i18n);

const GRAPH = {
  entries: ['watch'],
  nodes: [
    { id: 'watch', kind: 'monitor', outcomes: ['merged', 'closed'] },
    { id: 'tidy', kind: 'tidy' },
    { id: 'sign', kind: 'human', outcomes: ['approved'] },
    { id: 'choice', kind: 'opinion', outcomes: ['merged'] },
  ],
  wires: [
    { id: 'w>t', kind: 'when', from: 'watch', to: 'tidy', outcome: 'merged' },
    { id: 'w>s', kind: 'when', from: 'watch', to: 'sign', outcome: 'closed' },
  ],
};

const when = (outcome: string): DraftWire => ({ id: 'w', kind: 'when', from: 'watch', to: 'tidy', outcome, on: null } as DraftWire);

const WORDS = {
  en: { merged: 'merged', closed: 'closed without a merge', approved: 'approved' },
  zh: { merged: '已合并', closed: '未合并即关闭', approved: '已批准' },
} as const;

describe.each(['en', 'zh'] as const)('the outcome words of a monitor and a task, in %s', (language) => {
  afterEach(async () => { await i18n.changeLanguage('en'); });
  const words = WORDS[language];

  it('are said in a row, a way and its cause, by the kind of the step they leave', async () => {
    await i18n.changeLanguage(language);
    expect(rowSaid(t, 'outcome:closed', 'monitor')).toBe(words.closed);
    expect(rowSaid(t, 'outcome:approved', 'human')).toBe(words.approved);
    expect(howSaid(t, when('merged'), 'monitor')).toContain(words.merged);
    expect(waySaid(t, when('closed'), 'Tidy', 'monitor')).toContain(words.closed);
    expect(howSaid(t, { kind: 'back', outcome: null, on: 'closed' }, 'monitor')).toContain(words.closed);
  });

  it('are raw where the kind has no words for the outcome', async () => {
    await i18n.changeLanguage(language);
    expect(rowSaid(t, 'outcome:found', 'look')).toBe('found');
    expect(rowSaid(t, 'outcome:merged')).toBe('merged');
    expect(howSaid(t, when('merged'), 'opinion')).toContain('merged');
  });

  it('are said on the chart’s ports and edges, and the raw name stays their identity', async () => {
    await i18n.changeLanguage(language);
    const flow = graphFlow(t, drawnFromJson(t, i18n, GRAPH), { label: 'How work moves' });
    const ports = flow.nodes.find((node) => node.id === 'watch')!.step!.ports;
    expect(ports.map((port) => [port.outcome, port.said ?? port.outcome])).toEqual([['merged', words.merged], ['closed', words.closed]]);
    const edge = flow.edges.find((each) => each.id === 'w>s')!;
    expect([edge.port, edge.label]).toEqual(['closed', words.closed]);
    expect(edge.says).toContain(words.closed);
    const approved = flow.nodes.find((node) => node.id === 'sign')!.step!.ports[0]!;
    expect([approved.outcome, approved.said ?? approved.outcome]).toEqual(['approved', words.approved]);
  });
});

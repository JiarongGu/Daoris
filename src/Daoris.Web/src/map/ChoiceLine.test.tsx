import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import i18n from '../locales/i18n';
import { ChoiceLine } from './ChoiceLine';
import { codeChoice, mapChoice } from './choiceWords';
import type { MapNode, Topology } from './topology';

// The choice's line is the Map's one polite live region, said once per choice.

const node = (id: string, extra: Partial<MapNode> = {}): MapNode => ({ id, owns: [], accepts: [], open: 0, working: false, parked: false, sessions: 0, ...extra });
const TOPOLOGY: Topology = {
  nodes: [node('repo-03', { open: 3, parked: true }), node('engine', { open: 1, working: true }), node('game')],
  quests: [], asks: [], chains: [], depends: [{ from: 'game', to: 'engine' }], knowledge: [], outside: 0, circles: 1,
};

beforeEach(() => i18n.changeLanguage('en'));
afterEach(cleanup);

describe('the choice in words', () => {
  it('names a repository, its open quests and a session waiting on the person', () => {
    expect(mapChoice(i18n.t.bind(i18n), TOPOLOGY, { kind: 'node', id: 'repo-03' })!.words).toBe('repo-03 — 3 open quests; a session waits on you');
    expect(mapChoice(i18n.t.bind(i18n), TOPOLOGY, { kind: 'node', id: 'engine' })!.words).toBe('engine — 1 open quest; a session is working there');
    expect(mapChoice(i18n.t.bind(i18n), TOPOLOGY, { kind: 'node', id: 'game' })!.words).toBe('game — 0 open quests');
  });

  it('names a line as a reader hears it, and nothing for a choice the data no longer holds', () => {
    expect(mapChoice(i18n.t.bind(i18n), TOPOLOGY, { kind: 'depends', from: 'game', to: 'engine' })!.words).toBe('game says it uses engine');
    expect(mapChoice(i18n.t.bind(i18n), TOPOLOGY, { kind: 'node', id: 'gone' })).toBeNull();
    expect(mapChoice(i18n.t.bind(i18n), TOPOLOGY, null)).toBeNull();
  });

  it('says a module with what it uses and what uses it, or that no dependency is recorded', () => {
    const deps = [{ from: 'session', to: 'store', kind: 'imports' }, { from: 'web', to: 'session', kind: 'imports' }, { from: 'api', to: 'session', kind: 'imports' }];
    expect(codeChoice(i18n.t.bind(i18n), deps, 'session')!.words).toBe('session — uses 1 · used by 2');
    expect(codeChoice(i18n.t.bind(i18n), deps, 'lonely')!.words).toBe('lonely — no dependency recorded');
  });

  it('says the same in Chinese, the id as written', async () => {
    await i18n.changeLanguage('zh');
    expect(mapChoice(i18n.t.bind(i18n), TOPOLOGY, { kind: 'node', id: 'repo-03' })!.words).toBe('repo-03 — 3 个未完成的委托；有会话在等你');
    expect(codeChoice(i18n.t.bind(i18n), [{ from: 'a', to: 'b', kind: 'imports' }], 'a')!.words).toBe('a — 依赖 1 个 · 被 0 个依赖');
  });
});

describe('the choice\'s line', () => {
  it('is a polite live region that reads the hint with nothing chosen', () => {
    render(<ChoiceLine choice={null} hint="Choose one" />);
    const line = screen.getByRole('status');
    expect(line).toHaveAttribute('aria-live', 'polite');
    expect(line).toHaveTextContent('Choose one');
  });

  it('is said once per choice: a count changing under the same choice is not read again', () => {
    const { rerender } = render(<ChoiceLine choice={{ key: 'node:a', words: 'a — 3 open quests' }} hint="Choose one" />);
    expect(screen.getByRole('status')).toHaveTextContent('a — 3 open quests');
    rerender(<ChoiceLine choice={{ key: 'node:a', words: 'a — 4 open quests' }} hint="Choose one" />);
    expect(screen.getByRole('status')).toHaveTextContent('a — 3 open quests');
    rerender(<ChoiceLine choice={{ key: 'node:b', words: 'b — 1 open quest' }} hint="Choose one" />);
    expect(screen.getByRole('status')).toHaveTextContent('b — 1 open quest');
    rerender(<ChoiceLine choice={null} hint="Choose one" />);
    expect(screen.getByRole('status')).toHaveTextContent('Choose one');
  });
});

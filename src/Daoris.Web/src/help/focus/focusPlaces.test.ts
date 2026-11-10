import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { MACHINE, type Place } from '../focus';
import { focusChip } from './focusChip';
import { focusRows, type PlaceRepository } from './focusPlaces';
import { keepFocusRecent, readFocusRecent } from './focusRecent';

/** The picker of `docs/2026-11-11-ask-scope-ux-design.md` §1: Games holds engine and tools, Work holds api and site. */
const REPOSITORIES: PlaceRepository[] = [
  { name: 'tools', workspace: 'Games', checkout: true },
  { name: 'engine', workspace: 'Games', checkout: true },
  { name: 'site', workspace: 'Work', checkout: false },
  { name: 'api', workspace: 'Work', checkout: true },
];

const games: Place = { kind: 'workspace', workspace: 'Games' };
const work: Place = { kind: 'workspace', workspace: 'Work' };
const engine: Place = { kind: 'repository', workspace: 'Games', repository: 'engine' };
const api: Place = { kind: 'repository', workspace: 'Work', repository: 'api' };

const said = (rows: ReturnType<typeof focusRows>) => rows.map((row) => `${row.group}:${row.key}${row.nested ? ' (in)' : ''}`);

describe('the focus picker', () => {
  it('lists the window first, then Recent, this machine and the other workspaces, whatever is chosen', () => {
    const rows = focusRows({ repositories: REPOSITORIES, window: 'Games', recent: [engine, games, work, api], open: new Set(['work']), typed: '' });
    expect(said(rows)).toEqual([
      'first:Games', 'recent:Games/engine', 'recent:Work', 'recent:Work/api', 'machine:machine',
      'other:Work', 'other:Work/api (in)', 'other:Work/site (in)',
    ]);
    expect(rows[0].mark).toBe('this-window');
    expect(rows.find((row) => row.key === 'Work' && row.group === 'other')?.count).toBe(2);
    expect(rows.find((row) => row.key === 'Work/site')?.mark).toBe('no-checkout');
    expect(rows.find((row) => row.key === 'Games/engine')?.beside).toBe('Games');
  });

  it('keeps five recent places at most and never the first row', () => {
    const many: Place[] = [games, engine, api, work, MACHINE, { kind: 'repository', workspace: 'Games', repository: 'tools' }];
    const recent = focusRows({ repositories: REPOSITORIES, window: 'Games', recent: many, open: new Set(), typed: '' })
      .filter((row) => row.group === 'recent');
    expect(recent.map((row) => row.key)).toEqual(['Games/engine', 'Work/api', 'Work', 'machine', 'Games/tools']);
  });

  it('flattens a search into one list, no place twice, a workspace name matching its repositories', () => {
    const rows = focusRows({ repositories: REPOSITORIES, window: 'Games', recent: [api], open: new Set(), typed: 'wor' });
    expect(said(rows)).toEqual(['recent:Work/api', 'other:Work', 'other:Work/site']);
    expect(rows.every((row) => !row.nested)).toBe(true);
    expect(focusRows({ repositories: REPOSITORIES, window: 'Games', recent: [], open: new Set(), typed: 'zzz' })).toEqual([]);
  });

  it('tells two repositories of one name apart by the workspace beside each', () => {
    const twins = [...REPOSITORIES, { name: 'engine', workspace: 'Work', checkout: true }];
    const rows = focusRows({ repositories: twins, window: 'Games', recent: [], open: new Set(), typed: 'engine' });
    expect(rows.map((row) => `${row.key} ${row.beside}`)).toEqual(['Games/engine Games', 'Work/engine Work']);
  });

  it('offers no workspace level on a machine whose only workspace is default, and starts it on this machine', () => {
    const only = [{ name: 'tools', workspace: 'default', checkout: true }];
    const rows = focusRows({ repositories: only, window: 'default', recent: [], open: new Set(), typed: '' });
    expect(said(rows)).toEqual(['first:machine', 'other:default/tools']);
    expect(rows[0].mark).toBe('this-window');
  });

  it('puts this machine first, marked this window, in a window on every workspace', () => {
    const rows = focusRows({ repositories: REPOSITORIES, window: null, recent: [], open: new Set(), typed: '' });
    expect(said(rows)).toEqual(['first:machine', 'other:Games', 'other:Work']);
  });
});

describe('the focus picker’s Recent', () => {
  afterEach(() => window.localStorage.clear());

  it('keeps the places chosen, newest first, none twice, five at most, and reads nothing from a broken store', () => {
    for (const place of [games, engine, api, work, MACHINE, engine, { kind: 'repository', workspace: 'Work', repository: 'site' } as Place]) {
      keepFocusRecent(place);
    }
    expect(readFocusRecent().map((place) => JSON.stringify(place))).toEqual(
      [{ kind: 'repository', workspace: 'Work', repository: 'site' }, engine, MACHINE, work, api].map((place) => JSON.stringify(place)));
    window.localStorage.setItem('daoris.help.focus.recent', '{oops');
    expect(readFocusRecent()).toEqual([]);
  });
});

describe('the focus chip', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', 'Games · this window', 'Ask Daoris’s focus: workspace Games, this window’s'],
    ['zh', 'Games · 此窗口', '问道衍的聚焦：工作区 Games（此窗口的）'],
  ])('says the window’s workspace by default (%s)', async (language, text, label) => {
    await i18n.changeLanguage(language);
    expect(focusChip({ focus: games, next: null, window: 'Games', narrow: false })).toEqual({ icon: 'workspace', text, label });
  });

  it.each([
    ['en', 'engine', 'Ask Daoris’s focus: repository engine in Games', 'This machine', 'Ask Daoris’s focus: this machine'],
    ['zh', 'engine', '问道衍的聚焦：Games 中的仓库 engine', '本机', '问道衍的聚焦：本机'],
  ])('says a chosen repository and this machine (%s)', async (language, text, label, machine, machineLabel) => {
    await i18n.changeLanguage(language);
    expect(focusChip({ focus: engine, next: null, window: 'Games', narrow: false })).toEqual({ icon: 'repository', text, label });
    expect(focusChip({ focus: MACHINE, next: null, window: 'Games', narrow: false })).toEqual({ icon: 'machine', text: machine, label: machineLabel });
  });

  it.each([
    ['en', 'Games · engine next message', 'engine · next', 'Ask Daoris’s focus: workspace Games, this window’s, engine from your next message'],
    ['zh', 'Games · 下一条消息起 engine', 'engine · 下一条起', '问道衍的聚焦：工作区 Games（此窗口的），从你的下一条消息起为 engine'],
  ])('says a change waiting for the next message, shorter when narrow (%s)', async (language, wide, narrow, label) => {
    await i18n.changeLanguage(language);
    expect(focusChip({ focus: games, next: engine, window: 'Games', narrow: false })).toEqual({ icon: 'workspace', text: wide, short: narrow, label, pending: true });
    expect(focusChip({ focus: games, next: engine, window: 'Games', narrow: true })).toEqual({ icon: 'repository', text: narrow, label, pending: true });
  });

  it.each([
    ['en', 'engine · gone', 'Ask Daoris’s focus: repository engine, no longer on this machine', 'engine · gone · Games next message'],
    ['zh', 'engine · 已不存在', '问道衍的聚焦：仓库 engine，已不在本机', 'engine · 已不存在 · 下一条消息起 Games'],
  ])('says a focus no longer here, and its widening (%s)', async (language, text, label, widening) => {
    await i18n.changeLanguage(language);
    expect(focusChip({ focus: engine, next: null, window: 'Games', narrow: false, gone: true })).toEqual({ icon: 'gone', text, label });
    expect(focusChip({ focus: engine, next: games, window: 'Games', narrow: false, gone: true }).text).toBe(widening);
  });
});

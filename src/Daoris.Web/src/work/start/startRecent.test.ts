import { afterEach, describe, expect, it, vi } from 'vitest';
import { HELP_REPOSITORY } from '../../data/api';
import { CHOSEN_START, keepChosen, keepRecent, MAX_RECENT, mergeRecent, readChosen, readRecent, RECENT_STARTS, withRecent } from './startRecent';

/** What this machine remembers of its starts (NEW1b, the entry design §1): the picker's *Recent*, and the one last chosen. */
describe('the starts this machine remembers', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    window.localStorage.removeItem(RECENT_STARTS);
    window.localStorage.removeItem(CHOSEN_START);
  });

  it('keeps the newest first, none twice, and only a few', () => {
    expect(withRecent(['engine', 'game'], 'game')).toEqual(['game', 'engine']);
    const many = ['a', 'b', 'c', 'd', 'e'];
    expect(withRecent(many, 'f')).toEqual(['f', 'a', 'b', 'c', 'd']);
    expect(withRecent(many, 'f')).toHaveLength(MAX_RECENT);
  });

  /** NEW1f: Recent is where the person has been, not only what the composer started. */
  it('merges this machine’s own sessions into the starts, newest first, one a repository, five at most', () => {
    const row = (repository: string, updated: string, id = `${repository}-1`) => ({ id, repository, updated });
    // The dates are parsed, not compared as text: +02:00 is earlier than its text suggests.
    const merged = mergeRecent(['game', 'tools'], [
      row('engine', '2026-10-12T10:00:00Z'),
      row('tools', '2026-10-12T09:00:00Z'),
      row('game', '2026-10-12T11:00:00+02:00'),
    ]);
    expect(merged).toEqual(['engine', 'game', 'tools']);

    // A tie keeps the composer's starts first; a repository is listed once.
    expect(mergeRecent(['tools'], [row('game', '2026-10-12T09:00:00Z'), row('tools', '2026-10-12T09:00:00Z')]))
      .toEqual(['tools', 'game']);

    expect(mergeRecent(['a'], ['b', 'c', 'd', 'e', 'f', 'g'].map((name, i) => row(name, `2026-10-12T0${i}:00:00Z`))))
      .toHaveLength(MAX_RECENT);
  });

  it('leaves out Ask Daoris’s chats and a session from elsewhere', () => {
    expect(mergeRecent([], [
      { id: 'a1b2c3d4', repository: HELP_REPOSITORY, updated: '2026-10-12T10:00:00Z' },
      { id: 'laptop/a1b2c3d4', repository: 'engine', updated: '2026-10-12T10:00:00Z' },
      { id: 'a1b2c3d5', repository: 'game', updated: '2026-10-12T09:00:00Z' },
    ])).toEqual(['game']);
  });

  it('remembers a start and the repository chosen across a reload', () => {
    keepRecent('engine');
    expect(keepRecent('tools')).toEqual(['tools', 'engine']);
    expect(readRecent()).toEqual(['tools', 'engine']);

    expect(readChosen()).toBeNull();
    keepChosen('game');
    expect(readChosen()).toBe('game');
  });

  it('reads nothing rather than failing when what is kept is garbage or storage refuses', () => {
    window.localStorage.setItem(RECENT_STARTS, '{not json');
    expect(readRecent()).toEqual([]);
    window.localStorage.setItem(RECENT_STARTS, JSON.stringify(['engine', 7, '', null]));
    expect(readRecent()).toEqual(['engine']);

    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('refused'); });
    expect(readRecent()).toEqual([]);
    expect(readChosen()).toBeNull();
  });
});

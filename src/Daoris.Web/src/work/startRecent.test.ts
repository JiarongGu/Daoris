import { afterEach, describe, expect, it, vi } from 'vitest';
import { CHOSEN_START, keepChosen, keepRecent, MAX_RECENT, readChosen, readRecent, RECENT_STARTS, withRecent } from './startRecent';

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

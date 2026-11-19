import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../../locales/i18n';
import { panelLayoutOf } from './panelLayout';
import { SessionPanel, type PanelFacts } from './SessionPanel';
import { storyOf } from './story';
import { BARE_WAIT, CHANGES, HEAD, LINE, RELEASE_WAIT, TRACK } from './session.fixtures';

// The panel's regions in their order, its rail beside the story from 68rem of its own width, and its narrow fold below 40rem:
// no rail, the head carrying the changes as one fact, the dock one card whole and the rest a line each.

const NOW = Date.parse('2026-11-20T15:18:00Z');
const FACTS: PanelFacts = {
  id: 's1', head: HEAD, track: TRACK, story: storyOf(LINE), waits: [RELEASE_WAIT, BARE_WAIT], changes: CHANGES, now: NOW,
  reads: 'next', onAnswer: () => {}, onSend: () => {},
};
const regions = (container: HTMLElement) => [...container.querySelectorAll('[data-region]')].map((node) => node.getAttribute('data-region'));

describe('panelLayoutOf', () => {
  it('stands the rail beside the story from 68rem and folds the head below 40rem', () => {
    expect(panelLayoutOf(1280, 16)).toEqual({ rail: true, narrow: false });
    expect(panelLayoutOf(1087, 16)).toEqual({ rail: false, narrow: false });
    expect(panelLayoutOf(639, 16)).toEqual({ rail: false, narrow: true });
  });
});

describe('SessionPanel', () => {
  it('draws the head, the track, the story, the band, the dock and the composer in that order, the rail beside', () => {
    const { container } = render(<SessionPanel facts={FACTS} band={<p>Nothing running</p>} width={1280} />, { wrapper: Tooltip.Provider });
    expect(regions(container)).toEqual(['head', 'track', 'story', 'rail', 'band', 'dock', 'composer']);
    expect(within(container.querySelector('[data-region="rail"]')!).getByText('src/limits/bucket.ts')).toBeTruthy();
  });

  it('names the actor, the model that ran and the state in its head', () => {
    const { container } = render(<SessionPanel facts={{ ...FACTS, head: { ...HEAD, asked: 'sonnet-4.5' } }} width={1280} />, { wrapper: Tooltip.Provider });
    const head = container.querySelector<HTMLElement>('[data-region="head"]')!;
    expect(within(head).getByText('Working')).toBeTruthy();
    expect(within(head).getByText(/Builder · Claude · asked sonnet-4.5, ran opus-4.1 · home/)).toBeTruthy();
  });

  it('folds at a narrow width: no rail, the changes one fact in the head, one card whole and the rest a line', () => {
    const { container } = render(<SessionPanel facts={FACTS} width={400} />, { wrapper: Tooltip.Provider });
    expect(regions(container)).not.toContain('rail');
    expect(within(container.querySelector<HTMLElement>('[data-region="head"]')!).getByText(/5 files \+248 −37/)).toBeTruthy();
    const dock = container.querySelector('[data-region="dock"]')!;
    expect(within(dock as HTMLElement).getAllByRole('article')).toHaveLength(1);
    expect(within(dock as HTMLElement).getByRole('button', { name: /Which header name/ })).toBeTruthy();
    expect(within(dock as HTMLElement).getByText('Waiting on you 2')).toBeTruthy();
  });

  it('says so with no session, and draws a skeleton while one loads', () => {
    const { rerender } = render(<SessionPanel facts={null} width={1280} />);
    expect(screen.getByText('No session open')).toBeTruthy();
    rerender(<SessionPanel facts={{ ...FACTS, head: { ...HEAD, state: 'loading' }, story: [] }} loading width={1280} />);
    expect(screen.getByText('Reading the session')).toBeTruthy();
  });
});

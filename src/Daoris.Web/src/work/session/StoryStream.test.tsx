import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import '../../locales/i18n';
import type { LineRowHome } from '../exchanges/LineRows';
import { lineRowsOf } from '../exchanges/rows';
import { CHANNEL_WORDS, HELPER, OPINION } from '../exchanges/rows.fixtures';
import { toDockLine, toStepRows } from '../track/seek';
import { storyOf } from './story';
import { StoryStream } from './StoryStream';
import { WaitCard } from './WaitCard';
import { LINE, RELEASE_WAIT, at } from './session.fixtures';

// The story drawn: one stream, each line its kind's part in the order it happened, the activity folded to a line that opens to
// each call, the read mark a rule before the first line after it.

const STORY = storyOf(LINE);
const ACTOR = { maker: 'Claude', model: 'opus-4.1' };
const parts = (container: HTMLElement) => [...container.querySelectorAll('[data-part]')].map((node) => node.getAttribute('data-part'));

describe('StoryStream', () => {
  it('draws a line of turns, a step, a helper, an opinion and its line\'s rows in time order', () => {
    const { container } = render(<StoryStream story={STORY} actor={ACTOR} />);
    expect(parts(container)).toEqual(['step', 'owner', 'tools', 'helper', 'agent', 'line', 'step', 'step', 'line', 'opinion', 'agent']);
    expect(container.querySelector('[data-channel-row="m1"]')?.textContent).toContain('quota');
    expect(container.querySelector('[data-exchange-row="piece:p1"]')?.textContent).toContain('Asked gemini, as partner');
  });

  it('names each actor, and the model where one acted', () => {
    render(<StoryStream story={STORY} actor={ACTOR} />);
    expect(screen.getByText('You')).toBeTruthy();
    expect(screen.getAllByText('Claude · opus-4.1')).toHaveLength(2);
    expect(screen.getByText(/Second opinion · Codex · gpt-5.1/)).toBeTruthy();
    expect(screen.getByText(/Asked knowledge helper/)).toBeTruthy();
  });

  it('folds a burst of activity to one line that opens to each call', () => {
    render(<StoryStream story={STORY} actor={ACTOR} />);
    const fold = screen.getByRole('button', { name: /read 2 files · edited 1 file · ran 1 command · 1 failed/ });
    expect(screen.queryByText('npm test')).toBeNull();
    fireEvent.click(fold);
    expect(screen.getByText('npm test')).toBeTruthy();
  });

  it('marks where the person left off before the first line after it', () => {
    const { container } = render(<StoryStream story={STORY} actor={ACTOR} readMark={Date.parse(at(9, 22))} />);
    expect(parts(container).slice(4, 7)).toEqual(['agent', 'since', 'line']);
  });

  it("brings a step's first rule into view from its chip on the track", () => {
    render(<StoryStream story={STORY} actor={ACTOR} />);
    const seen: (string | null)[] = [];
    Element.prototype.scrollIntoView = function scrolled(this: Element) { seen.push(this.getAttribute('data-step-row')); };
    try {
      toStepRows('r1', 'review');
    } finally {
      delete (Element.prototype as Partial<Element>).scrollIntoView;
    }
    expect(seen).toEqual(['r1/review']);
  });

  it('says so when nothing has happened yet', () => {
    render(<StoryStream story={[]} actor={ACTOR} />);
    expect(screen.getByText('Nothing has happened in this session yet.')).toBeTruthy();
  });
});

// A line's requests and channel messages are rows of the one story, each pressed where the host hands its presses.

const home = (over: Partial<LineRowHome> = {}): LineRowHome => ({ attended: 's1', docked: [], onToDock: vi.fn(), ...over });
const rowOf = (id: string, side: 'asked' | 'back') => document.querySelector<HTMLElement>(`[data-exchange-row="${id}"][data-side="${side}"]`)!;

describe("a line's rows in the story", () => {
  it('reads a helper and a second opinion by their actors, the verdict beside what it judged, and opens the session that took each', () => {
    const onOpenSession = vi.fn();
    const story = storyOf({ ...LINE, rows: lineRowsOf({ exchanges: [HELPER, OPINION] }) });
    render(<StoryStream story={story} actor={ACTOR} home={home({ onOpenSession, findings: { 'opinion:o1': 2 } })} />);
    expect(rowOf('helper:h1', 'asked').textContent).toContain('Asked codex, as helper');
    expect(rowOf('opinion:o1', 'back').textContent).toContain('claude, as reviewer: answered · taken · 2 findings · on this session’s work');
    fireEvent.click(within(rowOf('opinion:o1', 'back')).getByRole('button', { name: 'Open its session' }));
    expect(onOpenSession).toHaveBeenCalledWith('r-sess');
  });

  it('answers a channel message in place, to its poster’s repository, and sends it on to its session', () => {
    const onPost = vi.fn();
    const onForward = vi.fn();
    const rows = lineRowsOf({ messages: [{ ...CHANNEL_WORDS[0]!, receipts: [{ repository: 'web', state: 'waiting' }] }] });
    render(<StoryStream story={storyOf({ ...LINE, rows })} actor={ACTOR} home={home({ channel: { onPost, onForward } })} />);
    const row = document.querySelector<HTMLElement>('[data-channel-row="m1"]')!;
    fireEvent.click(within(row).getByRole('button', { name: 'Reply' }));
    fireEvent.change(within(row).getByRole('textbox'), { target: { value: 'Read it, thanks.' } });
    fireEvent.click(within(row).getByRole('button', { name: 'Send' }));
    expect(onPost).toHaveBeenCalledWith({ text: 'Read it, thanks.', to: 'db', re: 'm1' }, expect.anything());
    fireEvent.click(within(row).getByRole('button', { name: /Send to its session/ }));
    expect(onForward).toHaveBeenCalledWith('m1', 'web', expect.anything());
  });

  it('leads a request waiting on the person to its card in the dock, with no press of its own', () => {
    const waiting = { ...HELPER, outcome: 'asked', holds: true, answered: undefined, session: undefined };
    const story = storyOf({ ...LINE, rows: lineRowsOf({ exchanges: [waiting] }) });
    render(
      <>
        <StoryStream story={story} actor={ACTOR} home={home({ docked: ['helper:h1'], onToDock: toDockLine })} />
        <div data-region="dock">
          <WaitCard wait={{ ...RELEASE_WAIT, id: 'helper:h1' }} now={Date.now()} onAnswer={vi.fn()} />
        </div>
      </>,
    );
    const row = rowOf('helper:h1', 'asked');
    expect(within(row).getAllByRole('button').map((press) => press.textContent)).toEqual(['Waits on you, in the dock']);
    fireEvent.click(within(row).getByRole('button'));
    expect(document.activeElement?.closest('[data-wait]')?.getAttribute('data-wait')).toBe('helper:h1');
  });
});

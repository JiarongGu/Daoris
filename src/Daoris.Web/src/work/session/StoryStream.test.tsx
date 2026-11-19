import { describe, expect, it } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import '../../locales/i18n';
import { storyOf } from './story';
import { StoryStream } from './StoryStream';
import { LINE, at } from './session.fixtures';

// The story drawn: one stream, each line its kind's part in the order it happened, the activity folded to a line that opens to
// each call, the read mark a rule before the first line after it.

const STORY = storyOf(LINE);
const ACTOR = { maker: 'Claude', model: 'opus-4.1' };
const parts = (container: HTMLElement) => [...container.querySelectorAll('[data-part]')].map((node) => node.getAttribute('data-part'));

describe('StoryStream', () => {
  it('draws a line of turns, a step, a helper, an opinion and a channel message in time order', () => {
    const { container } = render(<StoryStream story={STORY} actor={ACTOR} />);
    expect(parts(container)).toEqual(['step', 'owner', 'tools', 'helper', 'agent', 'channel', 'step', 'step', 'opinion', 'agent']);
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
    expect(parts(container).slice(4, 7)).toEqual(['agent', 'since', 'channel']);
  });

  it('says so when nothing has happened yet', () => {
    render(<StoryStream story={[]} actor={ACTOR} />);
    expect(screen.getByText('Nothing has happened in this session yet.')).toBeTruthy();
  });
});

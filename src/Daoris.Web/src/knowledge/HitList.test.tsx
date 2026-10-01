import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { CJK_HIT, HITS } from './fixtures';
import { HitList, type HitsAnswer } from './HitList';

// Search's list as a molecule (FRAME1f, D118 §2): what was typed and answered arrives, and every press goes out.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const answered = (over: Partial<Extract<HitsAnswer, { state: 'answered' }>> = {}): HitsAnswer => ({
  state: 'answered', hits: HITS, more: false, byMeaning: false, nothing: false, searching: false, ...over,
});

function list(over: Partial<Parameters<typeof HitList>[0]> = {}) {
  const props = {
    query: 'chunk hydration', onQuery: vi.fn(), localOnly: true, onLocalOnly: vi.fn(), answer: answered(),
    marked: 'chunk hydration', semantic: false, chosen: null, onChoose: vi.fn(), onConverge: vi.fn(), ...over,
  };
  render(<HitList {...props} />);
  return props;
}

describe("Search's list", () => {
  it('is the box, local only, then the hits, each a row of its list', () => {
    list();

    expect(screen.getByRole('searchbox', { name: 'search knowledge' })).toHaveValue('chunk hydration');
    expect(screen.getByRole('checkbox', { name: "Each repository's own only" })).toBeChecked();
    expect(screen.getByText('3 results')).toBeInTheDocument();
    for (const item of screen.getAllByRole('listitem')) expect(item).toHaveAttribute('data-list-row');
    // The box stays in reach as the hits scroll under it.
    expect(screen.getByRole('searchbox').closest('.sticky')).toContainElement(screen.getByRole('checkbox'));
  });

  it('chooses a hit by its entry, and wears the choice on its row', async () => {
    const props = list({ chosen: HITS[1]!.id });

    await userEvent.click(within(screen.getByRole('listitem', { name: 'world-streaming' })).getByRole('button'));
    expect(props.onChoose).toHaveBeenCalledWith(HITS[0]!.id);
    expect(within(screen.getByRole('listitem', { name: 'A console that is not UTF-8' })).getByRole('button'))
      .toHaveAttribute('aria-current', 'true');
  });

  /** A row says where the hit lives and what it is, in the reader's words: the repository and the kind. */
  it('says each hit\'s repository and kind, and its path in its tip', () => {
    list();

    const meta = within(screen.getByRole('listitem', { name: 'world-streaming' })).getByText('game · knowledge');
    expect(meta).toHaveAttribute('title', '.claude/knowledge/world-streaming.md');
  });

  /** A capped list with nothing saying so is the one answer a search must never give quietly. */
  it('says the service had more than it shows', () => {
    list({ answer: answered({ more: true }) });
    expect(screen.getByText('first 3 — more match, so narrow it')).toBeInTheDocument();
  });

  it('sets local only, and clears the box, by the person\'s press alone', async () => {
    const props = list();

    await userEvent.click(screen.getByRole('checkbox', { name: "Each repository's own only" }));
    expect(props.onLocalOnly).toHaveBeenCalledWith(false);
    await userEvent.click(screen.getByRole('button', { name: 'Clear search' }));
    expect(props.onQuery).toHaveBeenCalledWith('');
  });

  /** Nothing typed asks nothing, and says nothing beneath the box. */
  it('shows the box alone before anything is asked', () => {
    list({ query: '', answer: { state: 'idle' } });

    expect(screen.queryByRole('listitem')).toBeNull();
    expect(screen.queryByText(/result/)).toBeNull();
  });

  /** D118 §3h: a search that never answered says the sentence in place, and is never blank. */
  it('says the sentence of a search that never answered, in place', () => {
    list({ answer: { state: 'unanswered', sentence: 'the service is not answering' } });

    expect(screen.getByText('the service is not answering')).toBeInTheDocument();
    expect(screen.queryByRole('listitem')).toBeNull();
  });

  /** A hit's title and excerpt are content: shown as they are, and the words marked in either language. */
  it('shows a 中文 hit as it is, its matched words marked', () => {
    list({ query: '场景加载', marked: '场景加载', answer: answered({ hits: [CJK_HIT] }) });

    expect(screen.getByRole('listitem', { name: '场景加载的顺序' })).toHaveTextContent('渲染管线');
    expect(screen.getAllByText(/场景|加载/, { selector: 'mark' }).length).toBeGreaterThan(0);
  });
});

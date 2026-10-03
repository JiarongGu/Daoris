import { afterEach, describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import i18n from '../i18n';
import type { NotePart } from '../api';
import { code } from '../test/code';
import { Note } from './Note';

// Props-only, like every molecule here (LANG1b, D142; the language design §2, §5, §6): the coded parts in both languages,
// a split note with the agent's question, a code the page does not know, and a record from before parts are each reached
// by passing them.

const ASKED: NotePart[] = [
  { code: 'ended.parked-asked', values: {}, text: 'It stopped with its quest still taken, to ask you:' },
  { words: 'Which branch?\n\n1. `main`\n2. `release/1.0`', by: 'agent' },
];

describe('a session’s note', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  /** §2: Daoris's lines are chrome, from the catalogue in the reader's language, run together as one paragraph. */
  it('words Daoris’s lines in the reader’s language', async () => {
    const parts: NotePart[] = [
      { code: 'ended.untouched-exit', values: { exit: 3 }, text: 'Exit 3 before taking its quest.' },
      { code: 'started.other-account', values: {}, text: 'It runs on another account.' },
    ];
    const { rerender } = render(<Note note="Exit 3 before taking its quest. It runs on another account." parts={parts} />);
    expect(screen.getByText('Exit 3 before taking its quest. It runs on another account.')).toBeInTheDocument();

    await i18n.changeLanguage('zh');
    rerender(<Note note="Exit 3 before taking its quest. It runs on another account." parts={parts} />);
    expect(screen.getByText('退出码 3，未接下委托。它改用另一个账户运行。')).toBeInTheDocument();
    expect(screen.queryByText(/Exit 3/)).toBeNull();
    // Worded, so nothing says it is shown as recorded.
    expect(screen.queryByText('按原文显示')).toBeNull();
  });

  /** §2, §9: a split note — the lead-in worded, the agent's question beneath it as a quote, its formatting kept. */
  it('sets the agent’s words apart beneath the lead-in, as written', async () => {
    await i18n.changeLanguage('zh');
    render(<Note note="…" parts={ASKED} />);

    const lead = screen.getByText('它停了下来，委托仍已接下，想问你：');
    const question = screen.getByText(/Which branch\?/);
    expect(question.closest('blockquote')).not.toBeNull();
    expect(question.closest('blockquote')!.className).toContain('whitespace-pre-wrap');
    expect(question.closest('blockquote')!.textContent).toBe('Which branch?\n\n1. `main`\n2. `release/1.0`');
    expect(lead.compareDocumentPosition(question)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    // Someone's words are never marked: they are theirs, in whatever language they wrote.
    expect(screen.queryByText('按原文显示')).toBeNull();
  });

  /** §5: a code the page does not know shows its own English, marked, beside the lines it could word. */
  it('marks a line it cannot word, and shows it as recorded', async () => {
    await i18n.changeLanguage('zh');
    render(
      <Note
        note="The quest reached done. It handed its tree on."
        parts={[
          { code: 'ended.done', values: {}, text: 'The quest reached done.' },
          { code: 'ended.handed-on', values: {}, text: 'It handed its tree on.' },
        ]}
      />,
    );

    expect(screen.getByText('委托已完成。')).toBeInTheDocument();
    const recorded = screen.getByText('It handed its tree on.');
    expect(recorded.parentElement!.textContent).toBe('按原文显示It handed its tree on.');
    expect(screen.getAllByText('按原文显示')).toHaveLength(1);
  });

  /** §6: a record from before parts shows its note as kept, marked, in either language. */
  it('shows a record from before parts as it was kept, marked', async () => {
    const { rerender } = render(<Note note={'the quest reached done.\nIn the tree it worked in.'} />);
    expect(screen.getByText('shown as recorded')).toBeInTheDocument();
    expect(screen.getByText(/the quest reached done\./).textContent).toBe('the quest reached done.\nIn the tree it worked in.');

    await i18n.changeLanguage('zh');
    rerender(<Note note={'the quest reached done.\nIn the tree it worked in.'} parts={[]} />);
    expect(screen.getByText('按原文显示')).toBeInTheDocument();
  });

  it('draws nothing for a record with no note', () => {
    const { container } = render(<Note note={null} parts={null} />);
    expect(container).toBeEmptyDOMElement();
  });

  /** A row too narrow for blocks: one run, the agent's words inline, the clamp the row asks for. */
  it('runs as one line for a row, clamped where it is asked to be', async () => {
    await i18n.changeLanguage('zh');
    const { container } = render(<Note parts={ASKED} compact clamp={2} />);
    const run = container.firstElementChild as HTMLElement;
    expect(run.tagName).toBe('SPAN');
    expect(run.className).toContain('line-clamp-2');
    expect(run.querySelector('blockquote')).toBeNull();
    expect(run.textContent).toBe('它停了下来，委托仍已接下，想问你：Which branch?\n\n1. `main`\n2. `release/1.0`');
  });

  /** A backticked command in a worded line is code, as every sentence's is (`Inline`); someone's words are not parsed. */
  it('sets a worded line’s backticked command as code, and leaves someone’s words as written', () => {
    render(<Note parts={[{ code: 'account.refused', values: { owner: 'Claude Code' }, text: '…' }, ...ASKED.slice(1)]} />);
    expect(screen.getByText(code('daoris agent'))).toBeInTheDocument();
    expect(screen.queryByText(code('main'))).toBeNull();
  });
});

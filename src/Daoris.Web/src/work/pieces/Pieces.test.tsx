import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { AskPartner } from './AskPartner';
import { type PieceRow, partnersOf, readPieces, shownOf } from './pieceRows';
import { type PieceSection, Pieces } from './Pieces';

// COAGENT1b (D199; the co-agent design §6.1): the Pieces section under a driven session's head, in English and Chinese. Each piece the
// session asked another agent to write, where the driver's look left it, its frozen commit and its files; a working piece's door and
// stop; and *Ask another agent to work on a piece…*, the person's own ask, with the piece, its paths and the agent named.

const roster = {
  harnesses: ['claude-code', 'codex-acp', 'stub-b'],
  names: { 'claude-code': 'Claude Code', 'codex-acp': 'Codex', 'stub-b': 'Stub B' },
};

const nameOf = (agent: string) => roster.names[agent as keyof typeof roster.names] ?? agent;

const row = (over: Partial<PieceRow> = {}): PieceRow => ({
  id: 'p1', piece: 'Make the report’s window seven days.', paths: ['src'], agent: 'codex-acp', askedBy: 'person', at: '2026-10-22T10:00:00Z',
  state: 'asked', code: null, end: null, partner: null, startedOn: null, commit: null, ref: null, files: null, beyond: null, ...over,
});

const handed = row({
  id: 'p2', state: 'handed', end: 'answered', partner: 'pt1', startedOn: 'codex-acp', commit: 'c0ffee0000000000000000000000000000000000',
  ref: 'refs/daoris/pieces/p2', files: ['README.md', 'src/window.ts'], beyond: ['README.md'],
});

const section = (over: Partial<PieceSection> = {}): PieceSection => ({ pieces: [], nameOf, ...over });

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

describe('a piece’s rows', () => {
  it('say each of the driver’s states in the section’s words, an end that is no answer by its end', () => {
    expect(['asked', 'waiting', 'approved', 'preparing', 'started', 'ending', 'imported', 'answered', 'handed', 'taken-in', 'not-taken-in',
      'held', 'refused', 'uncertain', 'discarded', 'later'].map((state) => shownOf({ state, end: null }))).toEqual([
      'asked', 'waits', 'working', 'working', 'working', 'working', 'answered', 'answered', 'answered', 'takenIn', 'notTakenIn', 'held',
      'refused', 'uncertain', 'discarded', 'asked',
    ]);
    expect(['stopped', 'out-of-time', 'limited', 'answered'].map((end) => shownOf({ state: 'handed', end })))
      .toEqual(['stopped', 'outOfTime', 'limited', 'answered']);
  });

  it('name the partners once each and count those working, and none where none was asked', () => {
    expect(partnersOf([], nameOf)).toBeNull();
    expect(partnersOf([row({ state: 'started' }), handed, row({ id: 'p3', agent: 'stub-b', state: 'preparing' })], nameOf))
      .toEqual({ agents: ['Codex', 'Stub B'], working: 2 });
  });

  it('are read defensively: a row with no id or no piece is left out, a missing list is empty', () => {
    expect(readPieces(null)).toEqual([]);
    const rows = readPieces({ pieces: [{ id: 'p1', piece: 'Write it.', state: 'started', paths: ['src', 3] }, { id: 'p2' }, { piece: 'x' }] });
    expect(rows).toHaveLength(1);
    expect(rows[0]).toMatchObject({ id: 'p1', piece: 'Write it.', state: 'started', paths: ['src'], files: null, askedBy: 'person' });
  });
});

describe('the Pieces section', () => {
  it('lists each piece as written, where it stands, its frozen commit and its files', () => {
    render(<Pieces section={section({ pieces: [row({ state: 'refused', code: 'not-measured' }), handed] })} headingId="pieces-s1" />);

    const list = within(screen.getByRole('region', { name: 'Pieces' }));
    expect(list.getAllByText('Make the report’s window seven days.')).toHaveLength(2);
    expect(list.getByText('Refused · by Codex · the agent\'s confinement as a partner is not measured')).toBeInTheDocument();
    expect(list.getByText('Answered · by Codex · c0ffee0 at refs/daoris/pieces/p2 · 2 files · 1 file beyond its piece')).toBeInTheDocument();
  });

  it('offers a working piece’s page and its stop, which asks first and keeps the work', async () => {
    const onOpen = vi.fn();
    const onStop = vi.fn((_: string, answered: { done: () => void }) => answered.done());
    render(<Pieces section={section({ pieces: [row({ state: 'started', partner: 'pt1', startedOn: 'codex-acp' })], onOpen, onStop })} headingId="h" />);

    expect(screen.getByText('Working · by Codex')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Open its page' }));
    expect(onOpen).toHaveBeenCalledWith('pt1');

    await userEvent.click(screen.getByRole('button', { name: 'Stop…' }));
    expect(screen.getByText(/What it wrote so far is kept as the piece’s work/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Stop the partner' }));
    expect(onStop).toHaveBeenCalledWith('pt1', expect.objectContaining({ done: expect.any(Function) }));
  });

  // COAGENT1b2: a piece's later acts, each asked first and told to its press by the piece's id.
  it('offers Discard… on a piece holding work or a copy, asking first, and the hand-over only on an uncertain one', async () => {
    const onAct = vi.fn((_: string, __: string, answered: { done: () => void }) => answered.done());
    const uncertain = row({ id: 'p3', state: 'uncertain', code: 'job-unknown' });
    render(<Pieces section={section({ pieces: [handed, uncertain, row({ id: 'p4', state: 'taken-in' }), row({ id: 'p5', state: 'discarded' })], onAct })} headingId="h" />);

    expect(screen.getAllByRole('button', { name: 'Discard…' })).toHaveLength(2);
    expect(screen.getAllByRole('button', { name: 'Hand over what it left' })).toHaveLength(1);

    await userEvent.click(within(document.querySelector('[data-piece="p3"]') as HTMLElement).getByRole('button', { name: 'Hand over what it left' }));
    expect(screen.getByText(/Its job is ended now/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Hand it over' }));
    expect(onAct).toHaveBeenCalledWith('p3', 'hand-over', expect.objectContaining({ done: expect.any(Function) }));

    await userEvent.click(within(document.querySelector('[data-piece="p2"]') as HTMLElement).getByRole('button', { name: 'Discard…' }));
    expect(onAct).toHaveBeenCalledTimes(1);
    await userEvent.click(screen.getByRole('button', { name: 'Discard the piece' }));
    expect(onAct).toHaveBeenLastCalledWith('p2', 'discard', expect.objectContaining({ done: expect.any(Function) }));
  });

  it('says Discard… and the hand-over in Chinese, and offers neither without the press', async () => {
    await i18n.changeLanguage('zh');
    const { rerender } = render(<Pieces section={section({ pieces: [row({ state: 'uncertain', code: 'job-unknown' })], onAct: vi.fn() })} headingId="h" />);
    expect(screen.getByRole('button', { name: '丢弃…' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '交付它留下的' })).toBeInTheDocument();

    rerender(<Pieces section={section({ pieces: [row({ state: 'uncertain', code: 'job-unknown' })] })} headingId="h" />);
    expect(screen.queryByRole('button', { name: '丢弃…' })).toBeNull();
  });

  it('offers no stop for a piece whose partner does not work, and no door without its press', () => {
    render(<Pieces section={section({ pieces: [handed] })} headingId="h" />);
    expect(screen.queryByRole('button', { name: 'Stop…' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Open its page' })).toBeNull();
  });

  it('opens the ask in place, with the piece, its paths and the agent named', async () => {
    const onAsk = vi.fn();
    render(<Pieces section={section({ ask: { from: 'claude-code', roster, onAsk } })} headingId="h" />);

    await userEvent.click(screen.getByRole('button', { name: 'Ask another agent to work on a piece…' }));
    expect(screen.getByText(/writes one piece of this session’s work in a clone of its own/)).toBeInTheDocument();
    await userEvent.type(screen.getByRole('textbox', { name: 'The piece' }), '  Make the window seven days.  ');
    await userEvent.type(screen.getByRole('textbox', { name: /Paths it is about/ }), 'src/window.ts  docs');
    await userEvent.click(screen.getByRole('button', { name: 'Ask' }));

    expect(onAsk).toHaveBeenCalledWith(
      { piece: 'Make the window seven days.', agent: 'codex-acp', paths: ['src/window.ts', 'docs'] }, expect.anything());
  });

  /** COAGENT1c: where the frame holds the ask, the head's ⋯ opens the same ask the section's press opens, and its close is the frame's. */
  it('opens the ask the frame holds open, as the head’s ⋯ asks, and tells the frame of its own press and its close', async () => {
    const onOpen = vi.fn();
    const { rerender } = render(<Pieces section={section({ ask: { from: 'claude-code', roster, onAsk: vi.fn(), open: false, onOpen } })} headingId="h" />);

    await userEvent.click(screen.getByRole('button', { name: 'Ask another agent to work on a piece…' }));
    expect(onOpen).toHaveBeenLastCalledWith(true);
    expect(screen.queryByRole('textbox', { name: 'The piece' })).toBeNull();

    rerender(<Pieces section={section({ ask: { from: 'claude-code', roster, onAsk: vi.fn(), open: true, onOpen } })} headingId="h" />);
    expect(screen.getByRole('textbox', { name: 'The piece' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Ask another agent to work on a piece…' })).toBeNull();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<Pieces
      section={section({
        pieces: [row({ state: 'started', partner: 'pt1', startedOn: 'codex-acp' }), handed, row({ id: 'p3', state: 'uncertain', code: 'job-unknown' })],
        onOpen: vi.fn(), onStop: vi.fn(), ask: { from: 'claude-code', roster, onAsk: vi.fn() },
      })}
      headingId="h"
    />);

    expect(screen.getByRole('region', { name: '分项' })).toBeInTheDocument();
    expect(screen.getByText('进行中 · 由 Codex 做')).toBeInTheDocument();
    expect(screen.getByText('已答复 · 由 Codex 做 · c0ffee0，位于 refs/daoris/pieces/p2 · 2 个文件 · 超出分项的文件 1 个')).toBeInTheDocument();
    expect(screen.getByText('不确定：工作时 Daoris 重启了 · 由 Codex 做')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: '打开它的页面' })).toHaveLength(2);
    expect(screen.getByRole('button', { name: '停止…' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '请另一个智能体做一个分项…' })).toBeInTheDocument();
  });
});

describe('AskPartner', () => {
  it('waits for a piece, offers only other agents, and says a refusal inside it keeping the words', async () => {
    const ask = vi.fn((_: unknown, answered: { refused: (sentence: string) => void }) => answered.refused('This session cannot lead a piece.'));
    render(<AskPartner from="claude-code" roster={roster} onAsk={ask} onClose={vi.fn()} />);

    const move = screen.getByRole('button', { name: 'Ask' });
    expect(move).toBeDisabled();
    await userEvent.click(screen.getByRole('combobox', { name: 'Which agent' }));
    expect(screen.queryByRole('option', { name: 'Claude Code' })).toBeNull();
    await userEvent.click(screen.getByRole('option', { name: 'Stub B' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'The piece' }), 'Write it.');
    await userEvent.click(move);

    expect(ask).toHaveBeenCalledWith({ piece: 'Write it.', agent: 'stub-b', paths: [] }, expect.anything());
    expect(await screen.findByText('This session cannot lead a piece.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'The piece' })).toHaveValue('Write it.');
  });

  it('says there is no other agent, and offers nothing to confirm', () => {
    render(<AskPartner from="claude-code" roster={{ harnesses: ['claude-code'] }} onAsk={vi.fn()} onClose={vi.fn()} />);
    expect(screen.getByText('No other agent is set up on this machine to write a piece.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Ask' })).toBeNull();
  });
});

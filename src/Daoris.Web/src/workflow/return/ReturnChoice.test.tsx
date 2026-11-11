import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { NodeWord } from '../NodeWord';
import type { RunStep } from '../run';

// Under a folder's return waiting on files changed in both the folder and the session's copy, each path is offered the three
// choices, each pressed as the run's node word naming the path; nothing is chosen until the person presses.

const step = (paths: string, refused?: string) => ({
  step: { id: 'back', kind: 'return' },
  node: 'return',
  acts: ['keep-yours', 'take-session', 'keep-both', 'stop'],
  wait: { code: 'both-changed', values: refused === undefined ? { paths } : { paths, refused } },
}) as unknown as RunStep;

afterEach(async () => { await i18n.changeLanguage('en'); });

describe("a folder's return waiting on the person's choice", () => {
  it.each([
    ['en', 'Keep yours', 'keep-yours'],
    ['en', "Take the session's", 'take-session'],
    ['zh', '两者都保留', 'keep-both'],
  ])('in %s, offers each path the three choices and presses %s for that path alone', async (language, press, act) => {
    await i18n.changeLanguage(language);
    const onWord = vi.fn().mockResolvedValue({ run: 'q7', node: 'back', act, done: true });
    render(<NodeWord run="q7" step={step('notes.md\nplan/q4.xlsx')} onWord={onWord} />);

    const rows = screen.getAllByRole('listitem');
    expect(rows).toHaveLength(2);
    expect(within(rows[1]).getByText('plan/q4.xlsx')).toBeInTheDocument();
    await userEvent.click(within(rows[1]).getByRole('button', { name: press }));

    expect(onWord).toHaveBeenCalledWith({ run: 'q7', node: 'back', act, words: 'plan/q4.xlsx' });
    expect(within(rows[1]).queryByRole('button', { name: press })).toBeNull();
    expect(within(rows[0]).getByRole('button', { name: press })).toBeInTheDocument();
  });

  it.each([
    ['en', 'shared/new.md', 'link-folder', 'is a link or junction in your folder, or under one, so nothing is written through it.', 'Keep yours'],
    ['en', 'Notes.md', 'collision', 'names the same file to the disk as another path here, its name differing only in case, so neither is brought back.', 'Keep yours'],
    ['zh', 'plan', 'type-copy', '会话的副本把一个文件换成了同名文件夹，或把文件夹换成了文件，所以两边都不带回。', '保留你的'],
    ['zh', '../outside.txt', 'escapes', '指向文件夹之外，所以不带回。', '保留你的'],
  ])('in %s, says what it found at a refused path and offers it Keep yours alone', async (language, path, code, said, keep) => {
    await i18n.changeLanguage(language);
    const onWord = vi.fn().mockResolvedValue({ run: 'q7', node: 'back', act: 'keep-yours', done: true });
    render(<NodeWord run="q7" step={step('notes.md', `${path}\t${code}`)} onWord={onWord} />);

    const rows = screen.getAllByRole('listitem');
    expect(rows).toHaveLength(2);
    expect(within(rows[1]).getByText(path)).toBeInTheDocument();
    expect(within(rows[1]).getByText(said)).toBeInTheDocument();
    expect(within(rows[1]).getAllByRole('button').map((button) => button.textContent)).toEqual([keep]);
    await userEvent.click(within(rows[1]).getByRole('button', { name: keep }));

    expect(onWord).toHaveBeenCalledWith({ run: 'q7', node: 'back', act: 'keep-yours', words: path });
  });

  it("says the driver's refusal on the path it was pressed for", async () => {
    const onWord = vi.fn().mockResolvedValue({ run: 'q7', node: 'back', act: 'keep-both', done: false, refused: 'not-waiting' });
    render(<NodeWord run="q7" step={step('notes.md')} onWord={onWord} />);

    await userEvent.click(screen.getByRole('button', { name: 'Keep both' }));

    expect(await screen.findByText('back waits for no such press of yours now.')).toBeInTheDocument();
  });
});

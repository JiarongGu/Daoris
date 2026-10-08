import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../i18n';
import type { UpdateState } from '../update/UpdateBanner';
import { UpdateSection } from './Update';

// UPDATE1b (D139 §3, §6): the install's update as Settings' row draws it, from props alone. The Driver domain's suite
// holds it over the mocked bridge; this holds the words a state carries and the states the bridge rarely shows.

const STAGED = { id: '20261003T120000Z-ab12cd34', version: '0.0.1', commit: 'abc1234', at: '2026-10-03T12:00:00Z' };

const standing = (extra: Partial<UpdateState>): UpdateState => ({
  state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, last: null, ...extra,
});

const draw = (update: UpdateState | undefined, busy = false) => {
  const onSay = vi.fn();
  const { container, unmount } = render(
    <Tooltip.Provider><UpdateSection update={update} busy={busy} onSay={onSay} /></Tooltip.Provider>,
  );
  return { onSay, container, unmount };
};

describe("Settings' update row", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws nothing before the shell answers', () => {
    expect(draw(undefined).container).toBeEmptyDOMElement();
  });

  it('applying, says Daoris starts again and offers nothing to press', () => {
    draw(standing({ state: 'applying', staged: STAGED, mode: 'now' }));

    expect(screen.getByText('installing')).toBeTruthy();
    expect(screen.getByText(/Installing: Daoris closes and starts again in a moment\./)).toBeTruthy();
    expect(screen.queryByRole('button', { name: /Update|Not now/ })).toBeNull();
  });

  it('refused by a check it has no words for, keeps the driver\'s own sentence', () => {
    draw(standing({ state: 'refused', staged: STAGED, problem: { code: 'elsewhere', message: 'a check this page is older than.' } }));

    expect(screen.getByText(/Not installed: a check this page is older than\./)).toBeTruthy();
  });

  it('names a build with no commit by its version alone, and leaves out a time it was not given', () => {
    draw(standing({ state: 'waiting', staged: { id: 'b1', version: '0.0.2', commit: null, at: null }, mode: 'not-now' }));

    expect(screen.getByText('0.0.2')).toBeTruthy();
    expect(screen.queryByText('Commit')).toBeNull();
    expect(screen.queryByText('Staged')).toBeNull();
  });

  it('holds its words while one is on its way', async () => {
    const { onSay } = draw(standing({ state: 'draining', staged: STAGED, mode: 'when-idle', driven: 1 }), true);

    for (const word of ['Update now', 'Not now']) {
      const button = screen.getByRole('button', { name: word });
      expect(button).toBeDisabled();
      await userEvent.click(button);
    }
    expect(onSay).not.toHaveBeenCalled();
  });

  // UPDATE1d: the row tells the swap `last` carries, the journal's record told or not, and never the banner's once-said
  // `outcome`, which a dismissal puts away while the terminal still says the swap (D50).
  it('says the last swap from `last`, never from the outcome the banner says once', () => {
    draw(standing({
      outcome: { phase: 'installed', build: 'old', version: '0.0.1', commit: 'aaa1111' },
      last: { phase: 'rolled-back', build: 'new', version: '0.0.2', commit: 'bbb2222', reason: 'start' },
    }));

    expect(screen.getByText('Daoris 0.0.2 (bbb2222) could not start, so Daoris went back to the build before it: the new build would not start.')).toBeTruthy();
    expect(screen.queryByText(/updated to 0\.0\.1/)).toBeNull();
  });

  it('after a dismissal, still says the last swap; with no journal, has no row for one', () => {
    const { unmount } = draw(standing({ last: { phase: 'installed', build: 'b1', version: '0.0.1', commit: 'abc1234' } }));

    expect(screen.getByText('Last update')).toBeTruthy();
    expect(screen.getByText('Daoris was updated to 0.0.1 (abc1234).')).toBeTruthy();
    unmount();

    draw(standing({ outcome: { phase: 'installed', build: 'b1', version: '0.0.1', commit: 'abc1234' } }));
    expect(screen.queryByText('Last update')).toBeNull();
  });

  // SWAP2c (D139's SWAP2 note): the row words a check's code from the banner's catalogue, so SWAP2's two codes are said
  // in the reader's language here too, never in the journal's English sentence.
  it.each([
    {
      code: 'move', phase: 'rolled-back',
      en: 'Daoris 0.0.2 could not start, so Daoris went back to the build before it: a file could not be moved for a reason other than being held open, so what had moved was put back; daoris-driver update says why.',
      zh: 'Daoris 0.0.2 无法启动，已退回到之前的版本：有文件因被占用以外的原因无法移动，已移动的文件都已放回原处；daoris-driver update 会说明原因。',
    },
    {
      code: 'error', phase: 'refused',
      en: 'The staged Daoris 0.0.2 was refused before anything was replaced: the launcher met an error before it could put the build in place, so the build before it runs on; stage this one again to retry.',
      zh: '暂存的 Daoris 0.0.2 在替换任何文件之前被拒绝：启动器在把构建换上之前出错，之前的构建照常运行；请重新暂存这个构建再试。',
    },
  ])('says a last swap ended by `$code` in its own words, in English and in 中文', async ({ code, phase, en, zh }) => {
    const update = standing({ last: { phase, build: 'b1', version: '0.0.2', commit: null, reason: code, detail: 'the journal’s own English sentence.' } });

    const { unmount } = draw(update);
    expect(screen.getByText(en)).toBeTruthy();
    unmount();

    await i18n.changeLanguage('zh');
    draw(update);
    expect(screen.getByText(zh)).toBeTruthy();
  });

  it('speaks 中文: waiting after Not now, and a swap refused', async () => {
    await i18n.changeLanguage('zh');
    draw(standing({
      state: 'waiting', staged: STAGED, mode: 'not-now',
      last: { phase: 'refused', build: 'x', version: '0.0.1', commit: 'abc1234', reason: 'size' },
    }));

    expect(screen.getByText('暂不')).toBeTruthy();
    expect(screen.getByText(/你选了「暂不更新」：工作照常进行，这个构建等你发话。/)).toBeTruthy();
    expect(screen.getByRole('button', { name: '空闲时更新' })).toBeTruthy();
    expect(screen.getByText('上次更新')).toBeTruthy();
    expect(screen.getByText('暂存的 Daoris 0.0.1 (abc1234) 在替换任何文件之前被拒绝：某个文件的大小与清单不符。')).toBeTruthy();
  });
});

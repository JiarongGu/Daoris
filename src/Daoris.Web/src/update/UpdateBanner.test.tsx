import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../i18n';
import { en } from '../locales';
import { UpdateBanner, type UpdateState } from './UpdateBanner';

// UPDATE1 (D139 §2, §3): the install's update as a quiet strip — staged and waiting, draining, applying, refused, and how
// the last swap ended — with the screen's words for it: Update when idle, Update now, Not now.

const STAGED = { id: '20261003-ab12', version: '0.0.1', commit: 'abc1234', at: '2026-10-03T12:00:00Z' };

const state = (extra: Partial<UpdateState>): UpdateState => ({
  state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, last: null, ...extra,
});

const draw = (update: UpdateState | undefined, busy = false) => {
  const onSay = vi.fn();
  const onDismiss = vi.fn();
  const { container, unmount } = render(<UpdateBanner update={update} busy={busy} onSay={onSay} onDismiss={onDismiss} />);
  return { onSay, onDismiss, container, unmount };
};

// SWAP2c (D139's SWAP2 note): the two codes SWAP2 added to the journal, each said in the reader's language. The
// journal's own sentence is English and stays the terminal's (`daoris-driver update`).
const DETAIL = 'the journal’s own English sentence.';
const SWAP2_CODES = [
  {
    code: 'move', phase: 'rolled-back',
    en: 'went back to the build before it: a file could not be moved for a reason other than being held open, so what had moved was put back; daoris-driver update says why.',
    zh: '已退回到之前的版本：有文件因被占用以外的原因无法移动，已移动的文件都已放回原处；daoris-driver update 会说明原因。',
  },
  {
    code: 'error', phase: 'refused',
    en: 'was refused before anything was replaced: the launcher met an error before it could put the build in place, so the build before it runs on; stage this one again to retry.',
    zh: '在替换任何文件之前被拒绝：启动器在把构建换上之前出错，之前的构建照常运行；请重新暂存这个构建再试。',
  },
] as const;

describe('the update banner', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws nothing with nothing staged and nothing to say, nor before the shell answers', () => {
    expect(draw(state({})).container).toBeEmptyDOMElement();
    expect(draw(undefined).container).toBeEmptyDOMElement();
  });

  it('while draining, names the build and what still runs, and offers Update now and Not now', async () => {
    const { onSay } = draw(state({ state: 'draining', staged: STAGED, mode: 'when-idle', driven: 1, turns: 1 }));

    expect(screen.getByRole('status')).toHaveTextContent('Daoris 0.0.1 (abc1234) installs when the work running now ends');
    expect(screen.getByRole('status')).toHaveTextContent('2 sessions still running.');
    await userEvent.click(screen.getByRole('button', { name: 'Not now' }));
    await userEvent.click(screen.getByRole('button', { name: 'Update now' }));
    expect(onSay.mock.calls).toEqual([['not-now'], ['now']]);
    expect(screen.queryByRole('button', { name: 'Update when idle' })).toBeNull();
  });

  it('waiting after Not now, offers Update when idle and Update now', async () => {
    const { onSay } = draw(state({ state: 'waiting', staged: STAGED, mode: 'not-now' }));

    expect(screen.getByRole('status')).toHaveTextContent('Daoris 0.0.1 (abc1234) is ready to install.');
    await userEvent.click(screen.getByRole('button', { name: 'Update when idle' }));
    expect(onSay).toHaveBeenCalledWith('when-idle');
    expect(screen.queryByRole('button', { name: 'Not now' })).toBeNull();
  });

  it('applying, says Daoris starts again and offers nothing to press', () => {
    draw(state({ state: 'applying', staged: STAGED, mode: 'now' }));

    expect(screen.getByRole('status')).toHaveTextContent('Installing Daoris 0.0.1 (abc1234). Daoris starts again in a moment.');
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('refused, says why by the check’s code, and the driver’s own words for a code it does not know', () => {
    draw(state({ state: 'refused', staged: STAGED, problem: { code: 'hash', message: 'the staged build’s app/x.dll differs.' } }));
    expect(screen.getByRole('status')).toHaveTextContent('was not installed: a file is not the one its manifest names.');
  });

  it('after a roll-back, says it once with its reason, and Dismiss puts it away', async () => {
    const { onDismiss } = draw(state({
      outcome: { phase: 'rolled-back', build: 'b1', version: '0.0.2', commit: null, reason: 'exited', detail: 'it ended.' },
    }));

    expect(screen.getByRole('status')).toHaveTextContent(
      'Daoris 0.0.2 could not start, so Daoris went back to the build before it: the new build ended before it came up.');
    await userEvent.click(screen.getByRole('button', { name: 'Dismiss' }));
    expect(onDismiss).toHaveBeenCalledOnce();
  });

  it.each(SWAP2_CODES)('a swap ended by `$code` is said in its own words, in English and in 中文', async ({ code, phase, en: english, zh }) => {
    const update = state({ outcome: { phase, build: 'b1', version: '0.0.2', commit: null, reason: code, detail: DETAIL } });

    const { unmount } = draw(update);
    expect(screen.getByRole('status')).toHaveTextContent(english);
    expect(screen.getByRole('status')).not.toHaveTextContent(DETAIL);
    unmount();

    await i18n.changeLanguage('zh');
    draw(update);
    expect(screen.getByRole('status')).toHaveTextContent(zh);
    expect(screen.getByRole('status')).not.toHaveTextContent(DETAIL);
  });

  // The banner's set is what made SWAP2's codes fall through to the journal's English: every reason the catalogue words,
  // the banner words, so a code given a sentence is never left out of the set again.
  it('words every reason the catalogue has a sentence for', () => {
    const codes = Object.keys(en).filter((key) => key.startsWith('update.why.')).map((key) => key.slice('update.why.'.length));
    expect(codes).toEqual(expect.arrayContaining(['move', 'error']));

    for (const code of codes) {
      const { unmount } = draw(state({ outcome: { phase: 'refused', build: 'b1', version: '0.0.2', reason: code, detail: DETAIL } }));
      expect(screen.getByRole('status')).toHaveTextContent(i18n.t(`update.why.${code}`));
      expect(screen.getByRole('status')).not.toHaveTextContent(DETAIL);
      unmount();
    }
  });

  it('after an update, says the version it was updated to', () => {
    draw(state({ outcome: { phase: 'installed', build: 'b1', version: '0.0.2', commit: 'def5678' } }));
    expect(screen.getByRole('status')).toHaveTextContent('Daoris was updated to 0.0.2 (def5678).');
  });

  it('holds its words while one is on its way', () => {
    draw(state({ state: 'draining', staged: STAGED, mode: 'when-idle', driven: 1 }), true);
    expect(screen.getByRole('button', { name: 'Update now' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Not now' })).toBeDisabled();
  });

  it('speaks 中文 in its own words', async () => {
    await i18n.changeLanguage('zh');
    draw(state({ state: 'draining', staged: STAGED, mode: 'when-idle', driven: 1 }));

    expect(screen.getByRole('status')).toHaveTextContent('正在运行的工作结束后将安装 Daoris 0.0.1 (abc1234)');
    expect(screen.getByRole('button', { name: '立即更新' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '暂不更新' })).toBeInTheDocument();
  });
});

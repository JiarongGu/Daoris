import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../i18n';
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
  const { container } = render(<UpdateBanner update={update} busy={busy} onSay={onSay} onDismiss={onDismiss} />);
  return { onSay, onDismiss, container };
};

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

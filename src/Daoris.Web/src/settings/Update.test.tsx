import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../i18n';
import { UpdateSection, type UpdateStanding } from './Update';

// UPDATE1b (D139 §3, §6): the install's update as Settings' row draws it, from props alone. The Driver domain's suite
// holds it over the mocked bridge; this holds the words a state carries and the states the bridge rarely shows.

const STAGED = { id: '20261003T120000Z-ab12cd34', version: '0.0.1', commit: 'abc1234', at: '2026-10-03T12:00:00Z' };

const standing = (extra: Partial<UpdateStanding>): UpdateStanding => ({
  state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, ...extra,
});

const draw = (update: UpdateStanding | undefined, busy = false) => {
  const onSay = vi.fn();
  const { container } = render(
    <Tooltip.Provider><UpdateSection update={update} busy={busy} onSay={onSay} /></Tooltip.Provider>,
  );
  return { onSay, container };
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

  it('prefers the swap `last` keeps to the outcome the banner still shows', () => {
    draw(standing({
      outcome: { phase: 'installed', build: 'old', version: '0.0.1', commit: 'aaa1111' },
      last: { phase: 'rolled-back', build: 'new', version: '0.0.2', commit: 'bbb2222', reason: 'start' },
    }));

    expect(screen.getByText('Daoris 0.0.2 (bbb2222) could not start, so Daoris went back to the build before it: the new build would not start.')).toBeTruthy();
    expect(screen.queryByText(/updated to 0\.0\.1/)).toBeNull();
  });

  it('speaks 中文: waiting after Not now, and a swap refused', async () => {
    await i18n.changeLanguage('zh');
    draw(standing({
      state: 'waiting', staged: STAGED, mode: 'not-now',
      outcome: { phase: 'refused', build: 'x', version: '0.0.1', commit: 'abc1234', reason: 'size' },
    }));

    expect(screen.getByText('暂不')).toBeTruthy();
    expect(screen.getByText(/你选了「暂不更新」：工作照常进行，这个构建等你发话。/)).toBeTruthy();
    expect(screen.getByRole('button', { name: '空闲时更新' })).toBeTruthy();
    expect(screen.getByText('上次更新')).toBeTruthy();
    expect(screen.getByText('暂存的 Daoris 0.0.1 (abc1234) 在替换任何文件之前被拒绝：某个文件的大小与清单不符。')).toBeTruthy();
  });
});

import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { attentionGroup, attentionWorded, rowRun, type PushedAttention } from './attention';
import { attentionActs } from './attentionOffers';
import { AttentionRow } from './AttentionRow';

/**
 * An end the service's sweep wrote names no session, so no session's page holds it: its row names the entry and the revision it
 * ends, says why as the sweep wrote it, is approved or declined in the row, and leads nowhere.
 */
const REASON = 'Its cited record `r1` is superseded by `r2`.';
const SWEPT: PushedAttention = {
  kind: 'memory', id: '9f8e7d6c5b4a3921', since: '2026-10-31T10:00:00Z', where: 'engine',
  memory: { ends: 1, replacements: 0, staged: 0, end: { op: '9f8e7d6c5b4a3921', repository: 'engine', target: 'm42', revision: 3, reason: REASON } },
};

describe('an end the sweep wrote, as a row in what needs you', () => {
  it('is worded by its entry and its reason, offered its approval and decline, and has no session to run', async () => {
    const worded = attentionWorded(SWEPT);

    expect(worded).toMatchObject({
      id: '9f8e7d6c5b4a3921', kind: 'memory', where: 'engine', title: 'End of engine:m42 at revision 3', detail: REASON, session: null,
    });
    expect(attentionActs(worded)).toEqual(['approve-end', 'decline-end']);
    expect(rowRun(worded)).toBeNull();
    expect(attentionGroup(SWEPT)).toBe('word');
    await i18n.changeLanguage('zh');
    try {
      expect(attentionWorded(SWEPT).title).toBe('结束 engine:m42（修订 3）');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('approves or declines in its row, with no door even where the band hands one', async () => {
    const memoryEnd = vi.fn();
    const open = vi.fn();
    const item = attentionWorded(SWEPT);
    render(<AttentionRow item={item} acts={{ memoryEnd }} onOpen={open} />);

    expect(screen.getAllByRole('button').map((button) => button.textContent)).toEqual(['Approve the end', 'Decline']);
    await userEvent.click(screen.getByRole('button', { name: 'Approve the end' }));
    expect(memoryEnd).toHaveBeenCalledWith(item, true);
    await userEvent.click(screen.getByRole('button', { name: 'Decline' }));
    expect(memoryEnd).toHaveBeenLastCalledWith(item, false);
    expect(open).not.toHaveBeenCalled();
  });
});

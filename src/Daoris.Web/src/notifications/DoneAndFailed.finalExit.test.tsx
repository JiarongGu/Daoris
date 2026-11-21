import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import i18n from '../locales/i18n';
import { DoneAndFailed, noticeWords, runOfNotice } from './DoneAndFailed';

describe('Done and failed: a step at the owner\'s final exit', () => {
  const notice = {
    id: 'look-final-exit-exit:q7/check#3', kind: 'look', code: 'final-exit', at: '2026-11-21T09:02:00.000Z', read: false, quest: 'q7', act: 'check',
  };

  it.each([
    ['en', 'Run #q7, step `check`: every holder failed, so it reached you as Daoris\'s final exit; its answer waits in What needs you'],
    ['zh', '进展 #q7 的步骤 `check`：所有担任者都未能完成，它作为道衍的最终出口交到你手上；该给的回答在“等你处理”里'],
  ])('names its run and step (%s)', async (language, words) => {
    await i18n.changeLanguage(language);
    expect(noticeWords(notice, i18n.t)).toBe(words);
    await i18n.changeLanguage('en');
  });

  it('opens its run\'s review by run, with no session named; another look\'s news naming a quest opens no run', () => {
    const onOpen = vi.fn();
    render(<DoneAndFailed notices={[notice]} onRead={vi.fn()} onOpen={onOpen} onDismiss={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: /Open/ }));
    expect(onOpen).toHaveBeenCalledWith(notice);
    expect(runOfNotice(notice)).toBe('q7');
    expect(runOfNotice({ ...notice, id: 'look-lost-claim-s3', code: 'lost-claim', session: 's3', quest: 'q3' })).toBeNull();
  });
});

import { afterEach, describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { RECORD } from './sessionRecord.fixtures';
import { FinishedRecord } from './FinishedRecord';

// KEEPSIZE1: a finished session whose tree is let go shows its record on its page, in both languages.

describe('the record of a finished tree on its session’s page', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says its landing, tip, recovery ref and commits once the tree is let go', () => {
    render(<FinishedRecord record={RECORD} />);

    expect(screen.getByRole('region', { name: 'What was kept of its work' })).toBeTruthy();
    expect(screen.getByText(/Landed as daoris\/land-1 on main/)).toBeTruthy();
    expect(screen.getByText('Branch daoris/s-1, tip c2c2c2c.')).toBeTruthy();
    expect(screen.getByText('The recovery ref refs/daoris/tips/s-1 holds the tip: its tree can be made again.')).toBeTruthy();
    expect(screen.getByText('Its commits (2)')).toBeTruthy();
    expect(screen.getByText(/Teach the parser dates/)).toBeTruthy();
    expect(screen.getByText('1 file held no commit: notes.md.')).toBeTruthy();
  });

  it('says the same in 中文, and where no ref holds the tip or the commits could not be read', async () => {
    await i18n.changeLanguage('zh');
    const { rerender } = render(<FinishedRecord record={RECORD} />);
    expect(screen.getByRole('region', { name: '其工作留存的记录' })).toBeTruthy();
    expect(screen.getByText('其提交（2）')).toBeTruthy();
    expect(screen.getByText('有 1 个文件未被任何提交保存：notes.md。')).toBeTruthy();

    rerender(<FinishedRecord record={{ ...RECORD, post: { ...RECORD.post, ref: null, commits: null } }} />);
    expect(screen.getByText('没有恢复引用保留末端提交。')).toBeTruthy();
    expect(screen.getByText('已无法从末端提交读取其提交。')).toBeTruthy();
  });
});

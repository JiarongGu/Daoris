import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// ONEWAY1ad-ii (the oneway design §1.3): This machine's Old records, a molecule over a fixture list: the workflow part's census first, then one old
// way ended and one kept for an older build, the floor, in English and in Chinese.

import i18n from '../../locales/i18n';
import { KEPT, KEPT_ENDED } from './keptFixtures';
import { OldRecordsView } from './OldRecords';

describe('Old records, on This machine', () => {
  beforeEach(() => { i18n.changeLanguage('en'); });
  afterEach(cleanup);

  it('ONEWAY1ad-ii: says the workflow part first, then one read ended and one kept, with its row and the build it waits out', async () => {
    const onReview = vi.fn();
    render(<OldRecordsView read={KEPT} onReview={onReview} />);
    expect(within(screen.getByRole('list', { name: "The conversion's parts" })).getByText('Workflows: 4 old records wait for your conversion')).toBeTruthy();
    const reads = within(screen.getByRole('list', { name: 'Old ways still read' })).getAllByRole('listitem');
    expect(reads.map((each) => each.querySelector('code')?.textContent)).toEqual(['git-moved-key', 'solicited-older-host']);
    expect(reads[0]!.textContent).toContain('nothing old here');
    expect(reads[0]!.textContent).not.toContain('Deleted by');
    expect(reads[1]!.textContent).toContain('kept until:');
    expect(reads[1]!.textContent).toContain("The floor passes COMMS1b's build. Deleted by ONEWAY1af.");
    expect(reads[1]!.textContent).toContain("Waits out: the builds before COMMS1b's");
    expect(screen.getByText(/No floor yet/)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Review conversion…' }));
    expect(onReview).toHaveBeenCalledTimes(1);
  });

  it('ONEWAY1ad-ii: an ended part offers no review, and a floor and a refused Undo are said', () => {
    render(<OldRecordsView read={KEPT_ENDED} onReview={() => {}} />);
    expect(screen.getByText('Workflows: nothing old here')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Review conversion…' })).toBeNull();
    expect(screen.getByText('The oldest build that can still write this home: 0.43.0.')).toBeTruthy();
    expect(screen.getByText(/This build \(0\.50\.0 on\) no longer reads/)).toBeTruthy();
  });

  it('ONEWAY1ad-ii: reads in Chinese', () => {
    i18n.changeLanguage('zh');
    render(<OldRecordsView read={KEPT} onReview={() => {}} />);
    expect(screen.getByText('工作流：4 条旧记录等待你的转换')).toBeTruthy();
    const reads = within(screen.getByRole('list', { name: '仍在读取的旧方式' })).getAllByRole('listitem');
    expect(reads[0]!.textContent).toContain('这里没有旧内容');
    expect(reads[1]!.textContent).toContain('保留至：');
    expect(reads[1]!.textContent).toContain('由 ONEWAY1af 删除。');
    expect(screen.getByRole('button', { name: '审阅转换…' })).toBeTruthy();
  });
});

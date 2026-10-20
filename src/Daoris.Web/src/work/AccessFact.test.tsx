import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import type { Session } from '../data/api';
import { accessRow, startedFull } from './AccessFact';
import { SessionHead } from './head/SessionHead';
import { SessionPageHead } from './head/SessionPageHead';

// SESSACCESS1 (D184 point 1; the UI/UX design §3.3 and §5, D185): a session says the access it started under, read from its
// record's pause map as the driver resolved it at the start, never from today's settings. The fixture is that map.

const TODAY = { permissions: 'auto', routing: 'person', landing: 'person', goAheads: 'named', questions: 'claude-code' };
const FULL = { permissions: 'unrestricted', routing: 'intake', landing: 'rule', goAheads: 'never', questions: 'claude-code' };
const SOME = { ...TODAY, permissions: 'unrestricted', landing: 'rule' };

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'working', kind: 'driven',
  created: '2026-10-02T09:00:00Z', updated: '2026-10-02T09:30:00Z', ...over,
});

const pageHead = (over: Partial<Session> = {}) => render(
  <SessionPageHead session={session(over)} title="Expose a streaming budget" shown="working" facts={['engine', '12m']} acts={[]} />,
);

/** The record's *Details*, opened, and its *Access* row's value; null where it has none. */
async function detailsAccess(over: Partial<Session> = {}): Promise<string | null> {
  render(<SessionHead session={session(over)} headed />);
  await userEvent.click(screen.getByRole('button', { name: /Details|详情/ }));
  const label = screen.queryByText(i18n.t('work.access.details'), { selector: 'dt' });
  return label ? label.nextElementSibling!.textContent : null;
}

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

describe('the access a session started under', () => {
  it('wears the full-access chip on its facts line only where every pause it recorded is open', () => {
    pageHead({ access: FULL });
    const line = screen.getByText(/engine · 12m/);
    expect(within(line).getByText('Full access')).toBeInTheDocument();
    expect(line.querySelector('[data-access="full"] svg')).not.toBeNull();
  });

  it('wears nothing where today’s pauses or only some open ones were recorded, or none was', () => {
    for (const access of [TODAY, SOME, null]) {
      pageHead({ access });
      expect(screen.queryByText('Full access')).toBeNull();
      expect(screen.getByText('engine · 12m').textContent).toBe('engine · 12m');
      cleanup();
    }
  });

  it('never counts a pause the page does not know as open, nor a record missing one it knows', () => {
    expect(startedFull({ access: { ...FULL, sandbox: 'off' } })).toBe(false);
    expect(startedFull({ access: { permissions: 'unrestricted', routing: 'intake', landing: 'rule', goAheads: 'never' } })).toBe(false);
    expect(startedFull({ access: FULL })).toBe(true);
    expect(startedFull({ access: undefined })).toBe(false);
  });

  it('lists each pause and its value as recorded in Details, in the setup’s words', async () => {
    expect(await detailsAccess({ access: TODAY })).toBe(
      'Permission prompts: Auto mode · Intake routing: You accept · Landing: When you accept · Go-aheads: Named acts · Questions: As Claude Code asks',
    );
    cleanup();
    expect(await detailsAccess({ access: FULL })).toBe(
      'Permission prompts: None · Intake routing: Intake routes · Landing: Landing rule · Go-aheads: Never · Questions: As Claude Code asks',
    );
  });

  it('says a pause or a value the page does not know by its code, in the record’s order after the known ones', () => {
    const row = accessRow(i18n.t, session({ access: { sandbox: 'off', permissions: 'later' } }));
    render(<p>{row.value}</p>);
    const said = screen.getByText(/Permission prompts/).closest('p')!;
    expect(said.textContent).toBe('Permission prompts: later · sandbox: off');
    expect([...said.querySelectorAll('code')].map((each) => each.textContent)).toEqual(['later', 'sandbox', 'off']);
  });

  it('says Not recorded for a driven record from before the field, and nothing for a conversation or an intake', async () => {
    expect(await detailsAccess({ access: null })).toBe('Not recorded');
    expect(accessRow(i18n.t, session({ kind: 'chat', quest: null })).value).toBeNull();
    expect(accessRow(i18n.t, session({ kind: 'chat', quest: null, ask: '0fda18' })).value).toBeNull();
  });

  it('says each in 中文', async () => {
    await i18n.changeLanguage('zh');
    pageHead({ access: FULL });
    expect(screen.getByText('完全访问')).toBeInTheDocument();
    cleanup();
    expect(await detailsAccess({ access: SOME })).toBe('权限提示：不提示 · 受理分派：由你采纳 · 落地：落地规则 · 放行：点名的行动前 · 提问：同 Claude Code 一样');
    cleanup();
    expect(await detailsAccess({ access: null })).toBe('未记录');
  });

  it('wears the chip beside its word in a window with no header', () => {
    render(<SessionHead session={session({ access: FULL })} />);
    expect(screen.getByText('Full access')).toBeInTheDocument();
  });
});

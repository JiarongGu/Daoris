import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import type { SessionGrouping } from '../groups';
import { rowRun } from '../attention/attention';
import type { Attention } from '../attention/AttentionRow';
import { placedFact } from './SessionRow';

// A session the driver placed by its run's waits: its row says the step its run waits at, one way whoever holds it, "waiting on
// <role>: you", in each shipped language, and the band's row opens its run. Which session stands for which run, and in which group, is
// .NET's (`RunWaitGroupsTests`).

const placed = (over: Partial<SessionGrouping>): SessionGrouping => ({
  session: 's1', group: 'needs-you', state: 'needs-you', reason: 'review', shown: 'completed', archived: true, teammate: false, ...over,
});

const accept = placed({ waits: [{ run: 'q1', node: 'review', why: 'accept', role: 'acceptor', since: '2026-11-10T09:30:00Z' }] });
const approval = placed({ group: 'needs-you', waits: [{ run: 'q1', node: 'approve', why: 'approval', role: 'approver' }] });

const said = (grouping: SessionGrouping) => {
  const fact = placedFact(grouping, grouping.shown);
  return fact ? i18n.t(fact.line, fact.values) : null;
};

describe('a row its run waits placed', () => {
  afterEach(() => i18n.changeLanguage('en'));

  it('says the step its run waits at, on its role, held by you, ahead of its tree’s work, in English and Chinese', async () => {
    await i18n.changeLanguage('en');
    expect(said({ ...accept, work: { commits: 2, uncommitted: 0 } })).toBe('its run’s review: waiting on `acceptor`: you');
    expect(said(approval)).toBe('its run’s approve: waiting on `approver`: you');
    await i18n.changeLanguage('zh');
    expect(said(accept)).toBe('它的进展停在 review：等待 `acceptor`：你');
    expect(said(approval)).toBe('它的进展停在 approve：等待 `approver`：你');
  });

  it('says the role a plugin step waits on, which the person holds, as any other wait on them', async () => {
    const actor = placed({ reason: 'actor', waits: [{ run: 'q1', node: 'release', why: 'actor', role: 'approver' }] });
    await i18n.changeLanguage('en');
    expect(said(actor)).toBe('its run’s release: waiting on `approver`: you');
    await i18n.changeLanguage('zh');
    expect(said(actor)).toBe('它的进展停在 release：等待 `approver`：你');
  });

  it('says the step that put it in Waiting on you where more than one waits, and a word a newer host adds alike', async () => {
    await i18n.changeLanguage('en');
    const both = placed({ group: 'needs-you', waits: [{ run: 'q1', node: 'review', why: 'accept', role: 'acceptor' }, { run: 'q1', node: 'go', why: 'go-ahead', role: 'approver' }] });
    expect(said(both)).toBe('its run’s go: waiting on `approver`: you');
    expect(said(placed({ group: 'needs-you', waits: [{ run: 'q1', node: 'next', why: 'newer', role: 'lead' }] }))).toBe('its run’s next: waiting on `lead`: you');
  });

  it('is offered in What needs you as a review row that opens its run', () => {
    const row = { kind: 'review', id: 's1', placed: accept } as unknown as Attention;
    expect(rowRun(row)).toEqual({ session: 's1' });
  });
});

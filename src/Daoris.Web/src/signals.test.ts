import { afterEach, describe, expect, it } from 'vitest';
import i18n from './i18n';
import {
  type Consideration, TOAST_LIMIT, capped, newsFrom, sittingBecause, sittingSentence, waitsForAccount, withNotice,
} from './signals';

/**
 * The driver's tick lines, as notices. Written from four identical toasts stacked on the deployed
 * application — one repository, one reason, one per tick, forever.
 */
describe('newsFrom', () => {
  const HELD = 'held  #7786da → testbed-core: `…` has never been trusted by this harness';

  /** 🔴 The defect, in one assertion. */
  it('says nothing about a hold that has not changed', () => {
    expect(newsFrom([HELD], [HELD])).toEqual([]);
  });

  it('tells the person the first time', () => {
    expect(newsFrom([], [HELD])).toEqual([HELD]);
  });

  /**
   * News again, and deliberately. The state it reports went away and came back — a quest that was
   * held, ran, and is held again is a second thing that happened, not a repeat of the first.
   */
  it('tells them again when it stopped and came back', () => {
    expect(newsFrom([HELD], ['engine  spawned s1a2b3c4'])).toEqual(['engine  spawned s1a2b3c4']);
    expect(newsFrom(['engine  spawned s1a2b3c4'], [HELD])).toEqual([HELD]);
  });

  it('lets genuinely different lines all through', () => {
    const tick = ['engine  spawned s1', 'game  spawned s2', 'sync  fed 2'];
    expect(newsFrom([], tick)).toEqual(tick);
  });

  /** A tick that repeats itself within one report is still one piece of news. */
  it('does not say the same thing twice in one tick', () => {
    expect(newsFrom([], [HELD, HELD])).toEqual([HELD]);
  });

  it('carries only what changed when a tick says several things', () => {
    const before = [HELD, 'sync  fed 2'];
    const now = [HELD, 'engine  spawned s3'];
    expect(newsFrom(before, now)).toEqual(['engine  spawned s3']);
  });

  it('is empty for an empty tick, and never mutates what it was given', () => {
    const before = [HELD];
    expect(newsFrom(before, [])).toEqual([]);
    expect(before).toEqual([HELD]);
  });
});

/**
 * Why a quest is sitting, in the driver's own words. The driver has said this every tick since D46
 * ("sitting must always say why"); the page dropped it, and the Overview asked "is anything
 * sitting" without ever answering "why" (deployed application, 2026-09-23).
 */
describe('sittingBecause', () => {
  const considered: Consideration[] = [
    { quest: '7786da', repository: 'testbed-core', verdict: 'Held', reason: 'the person paused testbed-core' },
    { quest: 'abc123', repository: 'engine', verdict: 'Start', reason: 'starting' },
    { quest: 'def456', repository: 'game', verdict: 'NotDrivable', reason: 'game is not drivable on this machine' },
  ];

  it('answers with the consideration for a quest the driver is not starting', () => {
    expect(sittingBecause(considered, '7786da')?.reason).toBe('the person paused testbed-core');
    expect(sittingBecause(considered, 'def456')?.verdict).toBe('NotDrivable');
  });

  /** A quest the driver is starting is not sitting — saying "starting" under it would be noise. */
  it('answers nothing for a quest the driver is starting', () => {
    expect(sittingBecause(considered, 'abc123')).toBeNull();
  });

  it('answers nothing for a quest the driver has not spoken about', () => {
    expect(sittingBecause(considered, 'nobody')).toBeNull();
    expect(sittingBecause([], '7786da')).toBeNull();
  });
});

/**
 * REV3 web-rest F13: both windows capped the notices on screen and THEN added the new one, so the
 * corner held four where the limit says three.
 */
describe('withNotice', () => {
  it('adds the newest and keeps the limit, counting the one just added', () => {
    const full = Array.from({ length: TOAST_LIMIT }, (_, n) => n);
    expect(withNotice(full, 99)).toHaveLength(TOAST_LIMIT);
    expect(withNotice(full, 99).at(-1)).toBe(99);
    expect(withNotice([], 1)).toEqual([1]);
  });

  /**
   * 🔴 UX5 U30: with the service stopped, the page's own request and the driver's tick said the same
   * thing in the same moment, and the corner held it twice. A sentence already on screen is said
   * once: its newest copy replaces it, so it moves to the newest place and its timer starts again.
   */
  it('says a sentence already on screen once, its newest copy replacing it', () => {
    const same = (a: { text: string }, b: { text: string }) => a.text === b.text;
    const shown = [{ id: 1, text: 'the service is not answering' }, { id: 2, text: 'saved' }];

    expect(withNotice(shown, { id: 3, text: 'the service is not answering' }, TOAST_LIMIT, same))
      .toEqual([{ id: 2, text: 'saved' }, { id: 3, text: 'the service is not answering' }]);
    // Without the rule, the list is only capped, as before.
    expect(withNotice(shown, { id: 3, text: 'the service is not answering' })).toHaveLength(3);
  });
});

describe('capped', () => {
  it('keeps the newest, because the newest is what a corner can promise to show', () => {
    expect(capped([1, 2, 3, 4, 5], 3)).toEqual([3, 4, 5]);
  });

  it('leaves a short list whole', () => {
    expect(capped([1, 2], 3)).toEqual([1, 2]);
    expect(capped([], 3)).toEqual([]);
  });

  it('survives a cap of nothing', () => {
    expect(capped([1, 2, 3], 0)).toEqual([]);
    expect(capped([1, 2, 3], -1)).toEqual([]);
  });
});

/**
 * 🔴 UX5 U27: in 中文 every outstanding row said *搁置 —* over the driver's English. The driver's
 * sentence is Daoris's own voice, so it is chrome, and chrome translates. It translates by VERDICT,
 * never by matching the English, and only where the words need nothing the page lacks. Every other
 * verdict keeps the driver's words, since its sentence names what the page is not told (a session,
 * a cap), and a verdict the page has not heard of keeps them too.
 */
describe('sittingSentence', () => {
  const sits = (verdict: string, reason: string): Consideration =>
    ({ quest: '9a9492', repository: 'engine', verdict, reason });

  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("passes the driver's own words through in English, whatever the verdict", async () => {
    await i18n.changeLanguage('en');
    const notDrivable = sits('NotDrivable', '`engine` has not been opted into driving on this machine.');
    expect(sittingSentence(notDrivable)).toBe(notDrivable.reason);
  });

  it('translates the verdicts that need nothing the page lacks, and keeps the rest verbatim', async () => {
    await i18n.changeLanguage('zh');
    for (const verdict of ['NotDrivable', 'Held', 'NoRoot']) {
      const said = sittingSentence(sits(verdict, 'the driver said so.'));
      expect(said).not.toBe('the driver said so.');
      expect(said).toMatch(/[一-鿿]/);
    }
    const busy = sits('RepositoryBusy', 'session `c4a7c4a7` is active in `engine` — one session per repository.');
    expect(sittingSentence(busy)).toBe(busy.reason);
    const unheardOf = sits('SomethingNew', 'a reason this page has never seen.');
    expect(sittingSentence(unheardOf)).toBe(unheardOf.reason);
  });

  /**
   * SESSUX1b, SESSUX1d (D126 §3.3): a quest the person's stop holds says so in 中文 too, naming the session from the
   * tick's `heldBy` rather than out of the driver's English, with the terminal's door that releases it. With no session
   * named (a shell older than the fact), the driver's words stand.
   */
  it('says a stop’s hold in 中文, naming the session the tick says holds it', async () => {
    const reason = 'you stopped session `s1a2b3c4`; Try again carries it on — `daoris driver retry 9a9492 --session s1a2b3c4`.';
    const held: Consideration = { ...sits('Stopped', reason), heldBy: 's1a2b3c4' };

    await i18n.changeLanguage('en');
    expect(sittingSentence(held)).toBe(reason);

    await i18n.changeLanguage('zh');
    const said = sittingSentence(held);
    expect(said).toMatch(/你停止了会话 `s1a2b3c4`/);
    expect(said).toMatch(/重试/);
    expect(said).toContain('`daoris driver retry 9a9492 --session s1a2b3c4`');
    expect(sittingSentence(sits('Stopped', reason))).toBe(reason);
  });

  /**
   * UPDATE1 (D139 §2): a quest an update's drain holds says so in 中文 from the tick's `forUpdate`, never from the driver's
   * English; any other `Blocked` hold keeps the driver's words.
   */
  it('says an update’s hold in 中文 from the tick’s fact, and no other Blocked hold', async () => {
    const reason = "an update is waiting for this machine's sessions to end: nothing new starts until it is installed and Daoris starts again.";
    const held: Consideration = { ...sits('Blocked', reason), forUpdate: true };

    await i18n.changeLanguage('en');
    expect(sittingSentence(held)).toBe(reason);

    await i18n.changeLanguage('zh');
    expect(sittingSentence(held)).toMatch(/更新/);
    expect(sittingSentence(held)).toContain('`daoris-driver update --cancel`');
    const plugin = sits('Blocked', 'held by plugin `hold-by-title`.');
    expect(sittingSentence(plugin)).toBe(plugin.reason);
  });

  /**
   * SESSUX1i (D126 §4.6): a quest parked on its failed sessions is *What needs you*'s row, and its detail is this
   * sentence. The tick carries the number the planner parked it at (`strikes`), so 中文 says it, with the terminal's
   * door. With no number (a shell older than the fact, or a park not read yet), the driver's words stand.
   */
  it('says a park in 中文, with the number the tick says failed', async () => {
    const reason = '3 session(s) have failed on `#9a9492` without landing anything — parked, because trying again spends an account rather than making progress. `daoris driver retry 9a9492` starts it again once you know why.';
    const parked: Consideration = { ...sits('Exhausted', reason), strikes: 3 };

    await i18n.changeLanguage('en');
    expect(sittingSentence(parked)).toBe(reason);

    await i18n.changeLanguage('zh');
    const said = sittingSentence(parked);
    expect(said).toMatch(/3 个会话/);
    expect(said).toMatch(/挂起/);
    expect(said).toContain('`daoris driver retry 9a9492`');
    expect(sittingSentence(sits('Exhausted', reason))).toBe(reason);
  });

  /**
   * TOOL4g (D125 §4): a quest held because every account its start may use is cooling waits for an account. The tick names
   * whose account, which and until when, so 中文 says it from those facts; English is the driver's own sentence. Any other
   * hold at spawn keeps the driver's words in both.
   */
  it("says a wait for an account in 中文 from the account the tick names, and the driver's words in English", async () => {
    const reason = 'the `claude-code` account `account-1` is cooling until Oct 3, 16:02 (Etc/UTC), as the agent said.';
    const waits: Consideration = {
      ...sits('Blocked', reason), waitsFor: { agent: 'claude-code', account: 'account-1', until: '2026-10-03T16:02:00Z', stated: true },
    };
    const own: Consideration = { ...waits, waitsFor: { ...waits.waitsFor!, account: null, stated: false } };

    await i18n.changeLanguage('en');
    expect(sittingSentence(waits)).toBe(reason);

    await i18n.changeLanguage('zh');
    const said = sittingSentence(waits);
    expect(said).toContain('claude-code 的账户 account-1');
    expect(said).toContain('智能体如此说明');
    expect(said).toContain('无需你处理');
    expect(sittingSentence(own)).toContain('claude-code 自己的登录');
    expect(sittingSentence(own)).toContain('Daoris 的默认值');
    expect(sittingSentence(sits('Blocked', 'the tree is dirty.'))).toBe('the tree is dirty.');
  });

  /**
   * TOOL6g: a hold that passed accounts not signed in names each and its sign-in, beside a wait on a cooling account or with
   * none cooling, so a person reading *waits for account-1* knows a sign-in frees it. 中文 says it from the tick's
   * `signedOut`; English is the driver's own sentence, which says the same.
   */
  it('says the accounts not signed in and the sign-in for each, beside a wait and with none cooling', async () => {
    const reason = 'no `claude-code` account this start may use is ready: … A sign-in starts it sooner: `daoris agent login claude-code --profile account-2`, or Settings → Agents.';
    const signedOut = { agent: 'claude-code', accounts: ['account-2', 'account-3'] };
    const beside: Consideration = {
      ...sits('Blocked', reason), signedOut,
      waitsFor: { agent: 'claude-code', account: 'account-1', until: '2026-10-06T09:00:00Z', stated: true },
    };
    const alone: Consideration = { ...sits('Blocked', reason), signedOut };

    await i18n.changeLanguage('en');
    expect(sittingSentence(beside)).toBe(reason);
    expect(sittingSentence(alone)).toBe(reason);

    await i18n.changeLanguage('zh');
    const waits = sittingSentence(beside);
    expect(waits).toContain('claude-code 的账户 account-1');
    expect(waits).toContain('account-2、account-3 未登录');
    expect(waits).toContain('`daoris agent login claude-code --profile account-2`');
    expect(waits).toContain('`daoris agent login claude-code --profile account-3`');
    // The Agents place, where Settings → Agents went (UX6e, D150 §5).
    expect(waits).toContain('「智能体」');
    expect(waits).not.toContain('设置 → 智能体');
    const none = sittingSentence(alone);
    expect(none).toContain('account-2、account-3 未登录');
    expect(none).toContain('`daoris agent login claude-code --profile account-3`');
    expect(none).not.toContain('冷却至');
  });

  /**
   * ACCT2b (D125's ACCT2b note): the tick carries the name the person gave each account beside its id, so 中文 says the
   * name, as the driver's English does; an account with no name is said by its id; the sign-in keeps the id.
   */
  it('says each account by the name the tick carries, its sign-in by its id, and an unnamed one by its id', async () => {
    const reason = 'the `claude-code` account `work` is cooling until Oct 6, 09:00 (Etc/UTC), as the agent said.';
    const named: Consideration = {
      ...sits('Blocked', reason),
      waitsFor: { agent: 'claude-code', account: 'acct-3f9c1a2b', name: 'work', until: '2026-10-06T09:00:00Z', stated: true },
      signedOut: { agent: 'claude-code', accounts: ['account-2', 'acct-77aa00ff'], names: ['personal', null] },
    };

    await i18n.changeLanguage('en');
    expect(sittingSentence(named)).toBe(reason);

    await i18n.changeLanguage('zh');
    const said = sittingSentence(named);
    expect(said).toContain('claude-code 的账户 work');
    expect(said).not.toContain('acct-3f9c1a2b');
    expect(said).toContain('personal、acct-77aa00ff 未登录');
    expect(said).toContain('`daoris agent login claude-code --profile account-2`');
    expect(sittingSentence({ ...named, waitsFor: undefined })).toContain('personal、acct-77aa00ff 未登录');
  });

  /**
   * PAUSE1e (D132 §2.3): a paused quest says whose pause holds it, from the tick's `pausedBy`, with the terminal's door that
   * resumes it: an ask's pause and a quest's own say it in their own words. With no pause named (a shell older than the
   * fact), the driver's words stand.
   */
  it('says a pause in 中文, naming the ask or the quest whose pause the tick says holds it', async () => {
    const asks = 'paused with ask `#a1`; Resume starts it — `daoris-driver ask --resume a1`.';
    const byAsk: Consideration = { ...sits('Paused', asks), pausedBy: { scope: 'ask', id: 'a1' } };
    const own = 'you paused `#9a9492`; Resume carries it on — `daoris-driver quest resume 9a9492`.';
    const byQuest: Consideration = { ...sits('Paused', own), pausedBy: { scope: 'quest', id: '9a9492' } };

    await i18n.changeLanguage('en');
    expect(sittingSentence(byAsk)).toBe(asks);
    expect(sittingSentence(byQuest)).toBe(own);

    await i18n.changeLanguage('zh');
    expect(sittingSentence(byAsk)).toContain('已随需求 `#a1` 暂缓');
    expect(sittingSentence(byAsk)).toContain('`daoris-driver ask --resume a1`');
    expect(sittingSentence(byQuest)).toContain('`daoris-driver quest resume 9a9492`');
    expect(sittingSentence(byQuest)).not.toContain('需求');
    expect(sittingSentence(sits('Paused', asks))).toBe(asks);
  });
});

describe('waitsForAccount', () => {
  it('is a hold at spawn the tick names an account for, and nothing else', () => {
    const waitsFor = { agent: 'claude-code', account: 'account-1', until: '2026-10-03T16:02:00Z', stated: true };
    const base = { quest: 'q1', repository: 'engine', reason: 'cooling.' };
    expect(waitsForAccount({ ...base, verdict: 'Blocked', waitsFor })).toBe(true);
    expect(waitsForAccount({ ...base, verdict: 'Blocked' })).toBe(false);
    expect(waitsForAccount({ ...base, verdict: 'Exhausted', waitsFor })).toBe(false);
    expect(waitsForAccount(null)).toBe(false);
  });
});

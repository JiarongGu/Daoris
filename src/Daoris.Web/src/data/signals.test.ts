import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import {
  type Consideration, TOAST_LIMIT, capped, sittingBecause, sittingSentence, waitsForAccount, withNotice,
} from './signals';

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
   * REVIEWENV1c2: a set-up step the planner sits because it cannot be shown here (`CannotShow`, REVIEWENV1c) says so in 中文,
   * with the driver's sentence as written beneath: it names an environment, an address and the door that declares one, none of
   * which the tick carries. English passes the driver's words through, as every verdict's does.
   */
  it('says a set-up step that cannot be shown here from the tick’s fact in each language, and the driver’s sentence without it', async () => {
    const reason = "set-up step `#9a9492` shows its chain's work in `dev`, and no review rule here names `dev` for `engine`: "
      + '`daoris driver review engine dev --kind local|deployed --procedure <path>` declares it.';
    const withFact = (code: string, address: string | null): Consideration => ({
      ...sits('CannotShow', reason), cannotShow: { code, environment: 'dev', address },
    } as Consideration);

    await i18n.changeLanguage('en');
    const enUnnamed = sittingSentence(withFact('unnamed', null));
    expect(enUnnamed).toContain('`dev`');
    expect(enUnnamed).not.toBe(reason);
    expect(enUnnamed).not.toMatch(/[一-鿿]/);
    expect(sittingSentence(withFact('no-address', null))).toMatch(/address/);
    expect(sittingSentence(withFact('no-window', 'http://localhost:5173'))).toMatch(/window/);
    expect(sittingSentence(withFact('no-window', 'http://localhost:5173'))).toContain('http://localhost:5173');
    expect(sittingSentence(sits('CannotShow', reason))).toBe(reason);

    await i18n.changeLanguage('zh');
    for (const [code, word] of [['unnamed', '审查规则'], ['no-address', '地址'], ['no-window', '窗口']] as const) {
      const said = sittingSentence(withFact(code, 'http://localhost:5173'));
      expect(said).toContain('`dev`');
      expect(said).toContain('搭建步骤');
      expect(said).toContain(word);
      expect(said).not.toContain('set-up step');
      expect(said).not.toContain(reason);
    }
    expect(sittingSentence(sits('CannotShow', reason))).toBe(reason);
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
   * OWNSPACE1b-B (D175 point 3): no Daoris account is signed in, said from the tick's `noAccount` fact. English passes the driver's
   * sentence (its only copy); 中文 says it from the agent, with the terminal's twin of the sign-in.
   */
  it('says a hold for no Daoris account from the fact, never from an empty list', async () => {
    const reason = 'No `claude-code` account of Daoris\'s own is signed in.';
    const none: Consideration = { ...sits('Blocked', reason), signedOut: { agent: 'claude-code', accounts: [], noAccount: true } };

    await i18n.changeLanguage('en');
    expect(sittingSentence(none)).toBe(reason);

    await i18n.changeLanguage('zh');
    const said = sittingSentence(none);
    expect(said).toContain('claude-code');
    expect(said).toContain('`daoris agent login claude-code --new`');
    expect(said).not.toContain(reason);
  });

  /** ACCTQUOTE1 (D125's ACCTQUOTE1 note): each sign-in is a command a person pastes, so its id is spelled for a shell. */
  it('spells each sign-in\'s account for a shell, and names one no shell can take by a placeholder', async () => {
    const reason = 'no `claude-code` account this start may use is ready.';
    const signedOut: Consideration = {
      ...sits('Blocked', reason), signedOut: { agent: 'claude-code', accounts: ['R&D', 'my lab', 'account-2'] },
    };

    await i18n.changeLanguage('zh');
    const said = sittingSentence(signedOut);
    expect(said).toContain('`daoris agent login claude-code --profile <account>`');
    expect(said).toContain('`daoris agent login claude-code --profile "my lab"`');
    expect(said).toContain('`daoris agent login claude-code --profile account-2`');
    // The accounts are said as they are; only the command is spelled.
    expect(said).toContain('R&D、my lab、account-2 未登录');
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

  /**
   * CARRY2c (D80's CARRY2b note): a quest whose take is not this machine's said the driver's English in both languages. The
   * tick now names whose the take is (`takenBy`): the machine and its session where a record named them, a take made here,
   * and the session here not carried on, so 中文 says it from those facts in each of its three forms. English is the driver's
   * own sentence; with no facts (a shell older than them), the driver's words stand in both.
   */
  it('says a take elsewhere in 中文 from the machine and session the tick names, and the driver’s words without them', async () => {
    const named = 'Quest `#9a9492` is taken on `alice-laptop`, by session `alice-laptop/ab12cd34`: the take is theirs, so session `s1` is not carried on over it.';
    const byTeammate: Consideration = {
      ...sits('TakenElsewhere', named),
      takenBy: { machine: 'alice-laptop', session: 'alice-laptop/ab12cd34', here: false, last: 's1' },
    };
    const unnamed = 'Quest `#9a9492` is taken on another machine: the take is theirs, so session `s1` is not carried on over it.';
    const byAnother: Consideration = { ...sits('TakenElsewhere', unnamed), takenBy: { machine: null, session: null, here: false, last: 's1' } };
    const outside = 'Quest `#9a9492` was taken here after session `s1` ended, by a chat or by work outside Daoris: the take is theirs, so session `s1` is not carried on over it.';
    const byHere: Consideration = { ...sits('TakenElsewhere', outside), takenBy: { machine: null, session: null, here: true, last: 's1' } };

    await i18n.changeLanguage('en');
    expect(sittingSentence(byTeammate)).toBe(named);
    expect(sittingSentence(byAnother)).toBe(unnamed);
    expect(sittingSentence(byHere)).toBe(outside);

    await i18n.changeLanguage('zh');
    const teammate = sittingSentence(byTeammate);
    expect(teammate).toContain('委托 `#9a9492` 已在 alice-laptop 上由会话 `alice-laptop/ab12cd34` 接下');
    expect(teammate).toContain('不会接续会话 `s1`');
    expect(teammate).not.toContain('taken');
    const another = sittingSentence(byAnother);
    expect(another).toContain('已在另一台机器上接下');
    expect(another).toContain('不会接续会话 `s1`');
    const here = sittingSentence(byHere);
    expect(here).toContain('会话 `s1` 结束后');
    expect(here).toContain('本机');
    expect(here).toContain('聊天');
    expect(here).not.toContain('另一台机器');
    expect(sittingSentence(sits('TakenElsewhere', named))).toBe(named);
  });

  // FABRIC-CLOSE1c23: a take lost to the person's own reopen is said as that, in both languages.
  it('says a take lost to the reopen in 中文 without another machine', async () => {
    const lost = 'Quest `#9a9492` was lost to your reopen of the work it grew from: the take is not this machine’s, so session `s1` is not carried on over it.';
    const byReopen: Consideration = { ...sits('TakenElsewhere', lost), takenBy: { machine: null, session: null, here: false, reopened: true, last: 's1' } };
    await i18n.changeLanguage('en');
    expect(sittingSentence(byReopen)).toBe(lost);
    await i18n.changeLanguage('zh');
    const zh = sittingSentence(byReopen);
    expect(zh).toContain('重新打开');
    expect(zh).toContain('不会接续会话 `s1`');
    expect(zh).not.toContain('另一台机器');
  });

  /**
   * OVERVIEWNOISE1c: the six verdicts that kept the driver's English are worded by the code the tick names beside them, in each
   * language, with the facts it carries — the cap as a number, the session, the other quest — and the terminal's doors verbatim.
   */
  describe('the verdicts the page words by their code', () => {
    type Said = NonNullable<Consideration['said']>;
    const coded = (verdict: string, said: Said): Consideration => ({ ...sits(verdict, 'the driver said so.'), said });
    const cases: [string, Said, string, RegExp][] = [
      ['AtCapacity', { code: 'cap', cap: 6 }, 'The concurrency cap (6) is spent — it frees as sessions finish.', /并发上限（6）已用满/],
      ['RepositoryBusy', { code: 'serving', session: 's1' }, 'Session `s1` is already working on `#9a9492`.', /会话 `s1` 已在处理 `#9a9492`/],
      ['RepositoryBusy', { code: 'opening' }, '`#9a9492` is starting on this machine already: its session opens once its tree and account are ready.', /`#9a9492` 已在本机启动中/],
      ['RepositoryBusy', { code: 'tree', session: 's1' }, 'Session `s1` is active in the tree `#9a9492` goes back into — one session per tree.', /会话 `s1` 正在 `#9a9492` 要回到的工作树中运行/],
      ['RepositoryBusy', { code: 'treeOpening', quest: 'q7' }, 'Queued behind quest `#q7`, which is starting in the tree `#9a9492` goes back into — one session per tree.', /排在委托 `#q7` 之后：它正在 `#9a9492` 要回到的工作树中启动/],
      ['RepositoryBusy', { code: 'treeThisLook', quest: 'q7' }, 'Queued behind quest `#q7`, which goes into the tree `#9a9492` goes back into at this look — one session per tree.', /这一轮查看中，它会进入 `#9a9492` 要回到的工作树/],
      ['RepositoryBusy', { code: 'repository', session: 's1' }, 'Session `s1` is active in the receiver\'s repository — one session per repository, whose root is its one tree. Sessions there run side by side once they open trees of their own.', /会话 `s1` 正在受托方的仓库中运行/],
      ['RepositoryBusy', { code: 'queued', quest: 'q7' }, 'Queued behind quest `#q7` in the receiver\'s repository — oldest first, one at a time.', /排在委托 `#q7` 之后——先到先办/],
      ['RepositoryBusy', { code: 'repositoryOpening', quest: 'q7' }, 'Queued behind quest `#q7`, which is starting in the receiver\'s repository — one session per repository, whose root is its one tree.', /排在委托 `#q7` 之后：它正在受托方的仓库中启动/],
      ['Waiting', { code: 'asked', quest: 'q2', repository: 'Backend' }, 'Waits on `#q2`, asked of `Backend` — it goes on, in the same tree, once that is answered.', /在等 `#q2`（已向 `Backend` 提出）/],
      ['Waiting', { code: 'unreadRunning', quest: 'q2', repository: 'Backend', session: 's9' }, 'Waits on `#q2`, which `Backend` closed done: its evidence is read when session `s9` ends — it goes on, in the same tree, once that evidence is found.', /会话 `s9` 停下时读取/],
      ['Waiting', { code: 'unread', quest: 'q2', repository: 'Backend' }, 'Waits on `#q2`, which `Backend` closed done, and its evidence is not read yet — it goes on, in the same tree, once the evidence is found, `daoris-driver quest check q2`, or once you accept the done as it stands, your yes: `daoris-driver quest accept q2`.', /`daoris-driver quest check q2`）.*`daoris-driver quest accept q2`/],
      ['Waiting', { code: 'unreadAtCommit', quest: 'q2', repository: 'Backend' }, 'Waits on `#q2`, which `Backend` closed done, and its evidence is not read yet — it goes on, in the same tree, once the evidence is found, `daoris-driver quest check q2 --commit <sha>`, or once you accept the done as it stands, your yes: `daoris-driver quest accept q2`.', /`daoris-driver quest check q2 --commit <sha>`/],
      ['Waiting', { code: 'missing', quest: 'q2', repository: 'Backend' }, 'Waits on `#q2`, which `Backend` closed done without the evidence it names — it goes on, in the same tree, once a later commit holds it, `daoris-driver quest check q2 --commit <sha>`, or once you accept the done as it stands, your yes: `daoris-driver quest accept q2`.', /却没有它所指明的证据/],
      ['Waiting', { code: 'unreviewed', quest: 'q2', repository: 'Backend', environment: 'local' }, 'Waits on `#q2`, which `Backend` closed done: it waits for your review in `local` — it goes on, in the same tree, once you say it is reviewed, `daoris-driver quest review q2 reviewed`, or skip the review, `daoris-driver quest review q2 skip`.', /正等你在 `local` 中审阅/],
      ['Waiting', { code: 'unreviewed', quest: 'q2', repository: 'Backend' }, 'Waits on `#q2`, which `Backend` closed done: it waits for your review — it goes on, in the same tree, once you say it is reviewed, `daoris-driver quest review q2 reviewed`, or skip the review, `daoris-driver quest review q2 skip`.', /正等你审阅——/],
      ['Waiting', { code: 'departed', quest: 'q2', repository: 'Backend' }, 'Waits on `#q2`, which `Backend` closed done departing from what you required — it goes on, in the same tree, once you accept that, your yes: `daoris-driver quest accept q2`.', /偏离了你的要求/],
      ['NotAdopted', { code: 'unregistered' }, 'The receiver is not registered on this machine, so there is nowhere to start it.', /受托方没有在本机注册/],
      ['NotAdopted', { code: 'noRoot' }, 'The receiver has not adopted Daoris and no root is known for it on this machine, so there is nothing to start and nothing there to see the quest.', /本机也不知道它的根目录/],
      ['NotAdopted', { code: 'noConnector' }, 'The receiver has not adopted Daoris, so a session there would have no connector to take the quest with — only the protocol door hands it one. Drive with an ACP agent, or adopt it.', /没有连接器/],
      ['WaitsForOpinion', { code: 'landing', state: 'reading' }, 'Set-up step `#9a9492` waits for a second opinion on the work it shows, so you look once at work that already answered another agent. The opinion: being read.', /搭建步骤 `#9a9492` 在等.*第二意见：/],
      ['WaitsForOpinion', { code: 'landing', state: null }, 'Set-up step `#9a9492` waits for a second opinion on the work it shows, which is not judged yet.', /这条意见还没有评定/],
      ['WaitsForOpinion', { code: 'steps', quest: 'q1', state: 'with-session' }, 'Waits for a second opinion on the step before it, `#q1`, before it starts. The opinion: being answered.', /上一步 `#q1` 的第二意见。第二意见：/],
      ['WaitsForOpinion', { code: 'steps', quest: 'q1', state: null }, 'Waits for a second opinion on the step before it, `#q1`, before it starts: it is not judged yet.', /上一步 `#q1` 的第二意见：还没有评定/],
      ['SourceReopened', { code: 'withdrawn', quest: 'q1' }, 'Quest `#9a9492` grew from the work of `#q1`, which you reopened: it is withdrawn, and nothing starts it until you go on with it on the new work or abandon it.', /它已撤回/],
      ['SourceReopened', { code: 'held', quest: 'q1' }, 'Quest `#9a9492` grew from the work of `#q1`, which you reopened: it is set aside, and nothing carries it on until you go on with it on the new work or abandon it.', /它已搁置/],
      ['TransientHeld', { code: 'transient', session: 's1' }, "Its last session's provider kept failing: it waits for your words.", /服务方一直出错：它在等你的话/],
      // STEWARDROLE1b (D209 §5.1, §3.1a): the steward's stop held for a Try again, and a start held out of the owner's checkout.
      ['Stopped', { code: 'steward', session: 's1' }, 'The steward stopped session `s1`; Try again, by the steward or the owner, carries it on.', /^管家停止了会话 `s1`；管家或所有者「重试」会让它继续。$/],
      ['Blocked', { code: 'checkout', session: 's1' }, 'Its earlier work is in your checkout, so carrying it on is yours.', /^它之前的工作在你的检出里，所以由你来让它继续。$/],
    ];

    it.each(cases)('words %s by its code in English and in 中文 (%#)', async (verdict, said, english, chinese) => {
      await i18n.changeLanguage('en');
      expect(sittingSentence(coded(verdict, said))).toBe(english);
      await i18n.changeLanguage('zh');
      const zh = sittingSentence(coded(verdict, said));
      expect(zh).toMatch(chinese);
      expect(zh).not.toContain('the driver said so.');
      expect(zh).not.toMatch(/\{\{|undefined/);
    });

    it('keeps the driver’s words where the code is not one it knows, or names another verdict’s, or the cap is missing', async () => {
      await i18n.changeLanguage('zh');
      expect(sittingSentence(coded('RepositoryBusy', { code: 'somethingNew' }))).toBe('the driver said so.');
      expect(sittingSentence(coded('AtCapacity', { code: 'serving', session: 's1' }))).toBe('the driver said so.');
      expect(sittingSentence(coded('AtCapacity', { code: 'cap' }))).toBe('the driver said so.');
    });
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

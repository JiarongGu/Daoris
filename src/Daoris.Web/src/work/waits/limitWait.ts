import type { NotePart, Session } from '../../data/api';
import { type AccountsAnswer, type AccountCooling, agentOf, machineScope, workspaceScope } from '../../settings/accounts/accounts';
import { toolOf, type ToolDoor } from '../../agents/tools';
import { moment } from '../../locales/format';
import type { Translate } from '../pausing/pausing';
import type { SessionEvent } from '../conversation/conversation';
import { noteText } from '../noteLines';
import type { AccountNames } from '../accountNames';

// What a line an account's limit cut off says while it waits (LIMITRESUME1d, D206 §3.7): one sentence, the same in the session's
// head, its foot, its row and its quest's page, read from facts the page already holds. Pure, so every surface reads one rule.

/**
 * A limited line's wait (D206 §3.2): the earliest moment an account it may run on is ready again, whose account that is, and so which
 * resume comes. Computed, never stored, as the driver computes it: the walk of the line's agent (`scope.next`) found no account ready,
 * and the earliest of those it passed cooling is the one it waits for.
 */
export type LimitWait = {
  /** The margin-added moment that account is ready again (§3.2): *ready again at*, never *reset at*. */
  until: string;
  /** That account; null for the agent's own sign-in. */
  account: string | null;
  /** Whether that account is the one the line's last part ran on, so it goes on in its own conversation (§3.3 step 1). */
  own: boolean;
  /** Whether the agent said that reset; false is Daoris's default standing in (*no reset was said*). */
  stated: boolean;
  /** The kind of limit its last reset did not lift (`limit.not-lifted`'s `hit`), where its record says so (§3.3's bound). */
  notLifted: string | null;
};

const live = (cooling: AccountCooling | null | undefined, now: Date) =>
  Boolean(cooling && Date.parse(cooling.until) > now.getTime());

/** The kind of limit a record says its last reset did not lift (`limit.not-lifted`), else null. */
export function notLiftedOf(parts: readonly NotePart[] | null | undefined): string | null {
  const part = (parts ?? []).find((each) => each.code === 'limit.not-lifted');
  const hit = part?.code ? part.values?.hit : null;
  return typeof hit === 'string' && hit ? hit : null;
}

/**
 * Whether a record is a driven line an account's limit cut off and that goes on by itself (D206 §3.1, §3.3): `failed` with the
 * ledger's `limit` flag, its quest still open or taken, and nothing of the person's holding it (a pause holds it, §3.7).
 */
export function limitedLine(session: Pick<Session, 'state' | 'limit' | 'kind' | 'quest'>, questStatus?: string | null, paused = false) {
  return session.state === 'failed' && Boolean(session.limit) && session.kind !== 'chat' && Boolean(session.quest)
    && (questStatus === undefined || questStatus === null || questStatus === 'Open' || questStatus === 'Taken') && !paused;
}

/**
 * The line's wait (D206 §3.2, §3.7): where the walk of its agent's accounts found none ready (`next.reason` is `waits`), the earliest
 * of those it passed cooling; its own where that is the account the record ran on. Null where an account is ready (the line goes on at
 * the driver's next look, so it waits for nothing), where the answer holds no walk, and in a browser, which holds no accounts.
 */
export function limitWaitOf({ session, workspace, doors, answer, now }: {
  session: Pick<Session, 'adapter' | 'profile' | 'noteParts'>;
  /** The line's workspace, as the registry names it; the set's one scope where it names none (D197). */
  workspace?: string | null;
  doors: readonly ToolDoor[];
  answer: AccountsAnswer | null | undefined;
  now: Date;
}): LimitWait | null {
  const door = doors.find((each) => each.harness === session.adapter);
  const agent = agentOf(answer, door ? toolOf(door) : session.adapter);
  if (!agent) return null;
  const scope = (workspace ? workspaceScope(agent, workspace) : null) ?? machineScope(agent);
  if (scope.next?.reason !== 'waits') return null;

  const facts = Array.isArray(agent.accounts) ? agent.accounts : [];
  const coolingOf = (account: string | null) =>
    account === null ? agent.own?.cooling : facts.find((each) => each.name === account)?.cooling;
  const waiting = (scope.next.others ?? [])
    .filter((each) => each.hold === 'cooling')
    .map((each) => {
      const account = each.account ?? null;
      const cooling = coolingOf(account);
      return { account, until: each.until ?? cooling?.until ?? null, stated: cooling?.stated ?? true };
    })
    .filter((each): each is { account: string | null; until: string; stated: boolean } =>
      Boolean(each.until) && Date.parse(each.until!) > now.getTime())
    .sort((one, other) => Date.parse(one.until) - Date.parse(other.until));
  const earliest = waiting[0]
    // A walk that named no cooling account: the record's own, where it still cools.
    ?? (live(coolingOf(session.profile ?? null), now)
      ? { account: session.profile ?? null, until: coolingOf(session.profile ?? null)!.until, stated: coolingOf(session.profile ?? null)!.stated }
      : null);
  if (!earliest) return null;

  return {
    until: earliest.until,
    account: earliest.account,
    own: earliest.account === (session.profile ?? null),
    stated: earliest.stated,
    notLifted: notLiftedOf(session.noteParts),
  };
}

/**
 * A conversation's wait as its events hold it (LIMITRESUME1d2, D206 §3.5, §3.7): the `chat.limit-waits` line the driver says where no
 * account of its agent had room, and when the first is ready again. Never drawn from the accounts alone, which would claim a resume
 * for a conversation no limit holds.
 */
export type ChatLimitHeld = {
  /** The note's part, so a page that holds no walk says the driver's own line in the reader's language. */
  part: NotePart;
  /** The margin-added moment the first account of its agent is ready again (§3.2). */
  until: string;
  /** Whether the agent said that reset (`stated`, `assumed`); otherwise Daoris's default stands in. */
  stated: boolean;
};

// What ends a conversation's wait after its line, as the driver's `LimitHeld.Waiting` and the look's stage read them: the person's
// *Stop waiting*, the wake's resume in its own conversation or its refusal, and the move to another account.
const LEAVES_THE_WAIT = new Set(['chat.limit-stopped-waiting', 'limit.resumed', 'limit.resume-refused', 'moved.on']);

/**
 * Whether a conversation still waits for its limit, read from its events (D206 §3.5): its last `chat.limit-waits` line, with no turn
 * since (it went on), no line that ends the wait, and no refusal that waits for the person (a note naming its `why`). Null otherwise.
 */
export function chatLimitHeldOf(events: readonly SessionEvent[]): ChatLimitHeld | null {
  for (let at = events.length - 1; at >= 0; at--) {
    const event = events[at]!;
    if (event.kind === 'turn') return null;
    if (event.kind !== 'note') continue;
    if (event.why) return null;
    const parts = event.parts ?? [];
    if (parts.some((part) => part.code !== undefined && LEAVES_THE_WAIT.has(part.code))) return null;
    const part = parts.find((each) => each.code === 'chat.limit-waits');
    if (!part?.code) continue;
    const until = part.values?.until;
    const why = part.values?.why;
    return typeof until === 'string' && until
      ? { part, until, stated: why === 'stated' || why === 'assumed' }
      : null;
  }
  return null;
}

/**
 * The wait in words (D206 §3.7): when it resumes by itself and on which account (or, no reset said, when it tries again), then which
 * resume comes, then the reset that did not lift it where one did not. `label` names an account as the roster names it, the own
 * sign-in by its own name.
 */
export function limitWaitSaid(t: Translate, wait: LimitWait, label: (account: string | null) => string): string[] {
  const time = moment(wait.until);
  return [
    wait.stated
      ? t('work.limit.waits', { time, account: label(wait.account) })
      : t('work.limit.noReset', { time }),
    t(wait.own ? 'work.limit.own' : 'work.limit.briefed'),
    ...(wait.notLifted ? [t('work.limit.notLifted', { hit: wait.notLifted })] : []),
  ];
}

/**
 * A conversation's wait in words (LIMITRESUME1d2, D206 §3.7): the walk's sentence where the page holds the accounts and the walk
 * waits, else the driver's own `chat.limit-waits` line in the reader's language (a browser, or a walk not read yet). Null once that
 * line's moment has passed and no walk says more: the look wakes it then, and a line saying a time gone by would claim a wait.
 */
export function chatLimitWaitSaid(t: Translate, { held, wait, label, language, names, now }: {
  held: ChatLimitHeld;
  wait: LimitWait | null;
  label: (account: string | null) => string;
  language: string;
  names?: AccountNames;
  now: Date;
}): string[] | null {
  if (wait) return limitWaitSaid(t, wait, label);
  if (!(Date.parse(held.until) > now.getTime())) return null;
  return [noteText(t, { note: null, parts: [held.part] }, language, names)];
}

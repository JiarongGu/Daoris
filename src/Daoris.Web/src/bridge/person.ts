import { type QueryClient, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { api, holdPersonKey, type ServiceConfirmation } from '../data/api';
import { keys } from '../data/queries';
import type { Confirmation, ConfirmationAnswer, ConfirmationField, ConfirmationNamed } from '../person/Confirmations';
import type { AppointmentAnswer, AppointmentNow, HolderNow, StandingAnswer, StandingConfirmationNow, StandingUnread } from '../person/standing';
import { call, confirmBound, refusedNoRoute } from './call';
import { notPushed } from './state/slices';

// The person's door in the window (PERSONDOOR1f, RPC1c; D156 points 2 and 4, the person-door design §2.2 and §4.2): the person
// key the shell holds for its host, kept in memory; the presses a terminal sent over the pipe that wait for the person's Confirm;
// and, once the page holds the key, what waits at the service's own confirmations, as the shell tells it (NOTIFY2j). Every route here is the window's alone: the
// pipe's census refuses each to a terminal, which asks and never answers.

/** A press a terminal sent over the pipe, waiting for the window, as `PERSON_CONFIRMATIONS` answers it and `CONFIRMATIONS` tells it. */
export type PipeConfirmation = {
  id: string; module: string; route: string; fields: ConfirmationField[];
  asker?: { process?: string | null; pid?: number | null } | null; asked: string;
  /** None (CONFIRMWAIT1a-i): a terminal's press waits until the person answers; a shell before it told its time. */
  expires?: string | null;
  /** Whether a terminal still waits on its line for it (the confirm-wait design §8.1); absent from a shell before it. */
  waitedOn?: boolean | null;
  /** That its Confirm is being judged against what it rests on (§5.2): its buttons are replaced meanwhile. */
  checking?: boolean | null;
  /** The longest that judgement may take, in milliseconds, which the Confirm waits for (§5.2). */
  checkWithin?: number | null;
  /** When a window first said its card was in front of the person (CONFIRMSEEN1); absent until then, and from a shell before it. */
  shownAt?: string | null;
  /** The values its sentence names by role (NOTIFY2b, the modules' `PipeWording`), beside `fields`; absent from a shell before them. */
  named?: ConfirmationNamed[] | null;
  /** That it carries a key, so it is not kept across a restart (CONFIRMWAIT1a-ii, the confirm-wait design §7.1); absent before it. */
  keyed?: boolean | null;
  /** That it waits again from its record after a restart, its asker the record's (§7.2, §8.1); absent before it. */
  restored?: boolean | null;
};

/** What waits, whole, numbered so the newest telling is kept (as `UNDER_WAY` is). */
export type PipeConfirmations = { seq: number | null; waiting: PipeConfirmation[] };

const NONE: PipeConfirmations = { seq: null, waiting: [] };

/** A telling or a read of what waits, or null where it is of no shape this reads. */
function confirmationsOf(told: unknown): PipeConfirmations | null {
  const shaped = told as { seq?: unknown; waiting?: unknown } | null | undefined;
  if (!Array.isArray(shaped?.waiting)) return null;
  return {
    seq: typeof shaped.seq === 'number' ? shaped.seq : null,
    waiting: (shaped.waiting as PipeConfirmation[]).filter((each) => typeof each?.id === 'string' && typeof each.route === 'string'),
  };
}

/** Keep a telling where it is newer than what is kept: the bus may deliver one late, and a read may land after a telling. */
function keep(client: QueryClient, told: unknown) {
  const now = confirmationsOf(told);
  if (!now) return;
  const kept = client.getQueryData<PipeConfirmations>(keys.terminalAsks)?.seq ?? null;
  if (now.seq !== null && kept !== null && now.seq <= kept) return;
  client.setQueryData(keys.terminalAsks, now);
}

const fromPipe = (waiting: PipeConfirmation): Confirmation => ({
  source: 'pipe', id: waiting.id, module: waiting.module, route: waiting.route, fields: waiting.fields ?? [],
  named: Array.isArray(waiting.named) ? waiting.named : [], asker: waiting.asker ?? null, asked: waiting.asked ?? null,
  expires: waiting.expires ?? null, shown: waiting.shownAt != null,
  waitedOn: typeof waiting.waitedOn === 'boolean' ? waiting.waitedOn : null, checking: waiting.checking === true,
  checkWithin: typeof waiting.checkWithin === 'number' ? waiting.checkWithin : null,
  keyed: waiting.keyed === true, restored: waiting.restored === true,
});

/**
 * What a terminal asked this window to confirm over the pipe (RPC1c): read as the page mounts, for a window opened or reloaded
 * while one waits, then kept by the shell's `CONFIRMATIONS` at each change. None in a browser, which has no pipe, and none from a
 * shell older than the route.
 */
export function useTerminalConfirmations(): Confirmation[] {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  useShenoraEvent<PipeConfirmations>('DAORIS', 'CONFIRMATIONS', (told) => keep(client, told));
  const query = useQuery({
    queryKey: keys.terminalAsks,
    queryFn: async () => {
      const read = confirmationsOf(await call<PipeConfirmations>('PERSON_CONFIRMATIONS')) ?? NONE;
      const kept = client.getQueryData<PipeConfirmations>(keys.terminalAsks);
      return kept && kept.seq !== null && read.seq !== null && kept.seq >= read.seq ? kept : read;
    },
    enabled: isAvailable,
    // The telling keeps it current: nothing asks again by itself.
    staleTime: Infinity,
    retry: false,
  });
  return (query.data ?? NONE).waiting.map(fromPipe);
}

/** What waits at the service's door as the shell tells it (NOTIFY2j), numbered so the newest telling is kept. */
export type ServiceConfirmationsTold = { seq: number | null; waiting: ServiceConfirmation[] };

/** A telling or a read of what waits at the service, or null where it is of no shape this reads. */
function serviceOf(told: unknown): ServiceConfirmationsTold | null {
  const shaped = told as { seq?: unknown; waiting?: unknown } | null | undefined;
  if (!Array.isArray(shaped?.waiting)) return null;
  return {
    seq: typeof shaped.seq === 'number' ? shaped.seq : null,
    waiting: (shaped.waiting as ServiceConfirmation[]).filter((each) => typeof each?.id === 'string' && typeof each.expires === 'string'),
  };
}

/** The shell's answer while it tells what waits at the service; `older` where it is older than the telling (NOTIFY2j). */
type ServiceRead = ServiceConfirmationsTold | 'older';

/**
 * What waits at the service for the person's confirmation (PERSONDOOR1b's doors, D156 §4.2), asked only while the page holds the
 * person key: its Confirm and Refuse take the key, and a host handed none lists none. **Told, not polled** (NOTIFY2j, D196 point 4):
 * the shell holds one wait on the service and tells every window what waits at each change (`SERVICE_CONFIRMATIONS`), so each
 * window counts the one list it was told; read as the page mounts, for a window opened mid-wait. A shell older than the telling,
 * and a browser, keep the poll of the service's list. `read` says whether this window has read it at all, so its first read is
 * told apart from an arrival.
 */
export function useServiceConfirmations(held: boolean): { waiting: Confirmation[]; read: boolean } {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  useShenoraEvent<ServiceConfirmationsTold>('DAORIS', 'SERVICE_CONFIRMATIONS', (told) => {
    const now = serviceOf(told);
    const kept = client.getQueryData<ServiceRead>(keys.serviceTold);
    // Kept once this window has read, where it is newer: the bus may deliver one late, and the read may land after a telling.
    if (!now || kept === undefined || kept === 'older') return;
    if (now.seq !== null && kept.seq !== null && now.seq <= kept.seq) return;
    client.setQueryData<ServiceRead>(keys.serviceTold, now);
  });
  const told = useQuery({
    queryKey: keys.serviceTold,
    queryFn: async (): Promise<ServiceRead> => {
      let answer: unknown;
      try {
        answer = await call<ServiceConfirmationsTold>('SERVICE_CONFIRMATIONS');
      } catch (error) {
        if (refusedNoRoute(error)) return 'older';
        throw error;
      }
      const read = serviceOf(answer);
      if (!read) return 'older';
      const kept = client.getQueryData<ServiceRead>(keys.serviceTold);
      return kept && kept !== 'older' && kept.seq !== null && read.seq !== null && kept.seq >= read.seq ? kept : read;
    },
    enabled: isAvailable && held,
    // The telling keeps it current: nothing asks again by itself.
    staleTime: Infinity,
    retry: false,
  });
  const polled = useQuery({
    queryKey: keys.serviceAsks,
    queryFn: ({ signal }) => api.confirmations(signal),
    enabled: held && (!isAvailable || told.data === 'older'),
    refetchInterval: 5_000,
    retry: false,
  });
  const list = told.data && told.data !== 'older' ? told.data.waiting : polled.data;
  return { waiting: (list ?? []).filter((each) => each.state === 'waiting').map((each): Confirmation => ({
    source: 'service',
    id: each.id,
    route: `${each.method} ${each.route}`,
    act: each.act,
    fields: [...(each.values ?? []), ...(each.fields ?? [])],
    asker: null,
    asked: each.asked ?? null,
    expires: each.expires,
  })), read: list !== undefined };
}

/**
 * The person's Confirm or Refuse (RPC1c, PERSONDOOR1f): a terminal's press by its confirmation's id over the bridge, the service's
 * at its own door with the key. One no longer waiting is answered, never thrown, for the card to say where it was pressed (the
 * UI/UX design's R3): the bridge's code (`gone`), or the host's own sentence. A terminal's Confirm is answered once its grounds are
 * judged (CONFIRMWAIT1a-i, the confirm-wait design §5.2), so it waits as long as the press says that may take: `changed` with the
 * ground's reason and its values, or `unread` where they could not be read just now.
 */
export function useAnswerConfirmation() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async ({ waiting, confirm }: { waiting: Confirmation; confirm: boolean }): Promise<ConfirmationAnswer> => {
      if (waiting.source === 'pipe') {
        const bound = confirmBound(waiting.checkWithin);
        const answer = await call<{
          answered?: boolean; why?: string | null; message?: string | null; reason?: string | null; values?: Record<string, unknown> | null;
        }>('PERSON_CONFIRM', { id: waiting.id, confirm }, bound ? { timeoutMs: bound } : undefined);
        if (answer?.answered === true) return { answered: true };
        const values = Object.fromEntries(Object.entries(answer?.values ?? {}).filter((each): each is [string, string] => typeof each[1] === 'string'));
        return {
          answered: false, why: answer?.why ?? null, message: answer?.message ?? '',
          ...(typeof answer?.reason === 'string' ? { reason: answer.reason, values } : {}),
        };
      }

      try {
        await api.answerConfirmation(waiting.id, confirm);
        return { answered: true };
      } catch (error) {
        return { answered: false, why: null, message: (error as Error).message };
      }
    },
    onSettled: (_answer, _error, { waiting }) => {
      if (waiting.source === 'service') void client.invalidateQueries({ queryKey: keys.serviceAsks });
    },
  });
}

/**
 * The window's word that a terminal's press is in front of the person (CONFIRMSEEN1): kept once by the shell, which tells every window
 * by `CONFIRMATIONS`; it moves no clock (CONFIRMWAIT1a-i). A service's confirmation has none: its door counts its
 * own. Answered, never thrown: one no longer waiting is `gone`, which says nothing here, since the telling takes its card away.
 */
export function useConfirmationShown() {
  return useMutation({
    mutationFn: async (waiting: Confirmation): Promise<boolean> => {
      if (waiting.source !== 'pipe') return false;
      const answer = await call<{ shown?: boolean }>('PERSON_CONFIRM_SHOWN', { id: waiting.id });
      return answer?.shown === true;
    },
  });
}

const UNREAD: readonly StandingUnread[] = ['unread', 'newer', 'other-machine'];

const strings = (value: unknown): string[] =>
  (Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string') : []);

/**
 * `STATE`'s `standingConfirmation`, read field by field: the bridge leaves a null out, so each field is read defensively. Null where
 * the shell says none, an older shell's or one that serves no pipe, which draws no row.
 */
export function standingOf(raw: unknown): StandingConfirmationNow | null {
  if (typeof raw !== 'object' || raw === null || Array.isArray(raw)) return null;
  const shaped = raw as Record<string, unknown>;
  const presses = strings(shaped.presses);
  return {
    offered: shaped.offered === true,
    // On names a press, or it is not on: a grant of nothing reads as off, as the shell's own `IsOn` does.
    on: shaped.on === true && presses.length > 0,
    presses,
    built: strings(shaped.built),
    since: typeof shaped.since === 'string' && shaped.since.length > 0 ? shaped.since : null,
    unread: UNREAD.find((why) => why === shaped.unread) ?? null,
    unwritten: shaped.unwritten === true,
    used: typeof shaped.used === 'number' && Number.isFinite(shaped.used) ? shaped.used : 0,
    held: strings(shaped.held),
    appointmentsOffered: shaped.appointmentsOffered === true,
    supervisorOffered: shaped.supervisorOffered === true,
    stewardOffered: shaped.stewardOffered === true,
    appointments: appointmentsOf(shaped.appointments),
    holders: holdersOf(shaped.holders),
  };
}

/**
 * The owner's appointments (SUPERVISOR1a-ii; STEWARDROLE1a-i's scope), each read field by field; one with no role, or with neither a
 * scope nor a repository, is left out.
 */
function appointmentsOf(raw: unknown): AppointmentNow[] {
  if (!Array.isArray(raw)) return [];
  return raw.flatMap((each): AppointmentNow[] => {
    if (typeof each !== 'object' || each === null) return [];
    const shaped = each as Record<string, unknown>;
    const repository = typeof shaped.repository === 'string' ? shaped.repository : '';
    const scope = typeof shaped.scope === 'string' && shaped.scope.length > 0 ? shaped.scope
      : repository ? `repository:${repository}` : null;
    if (typeof shaped.role !== 'string' || scope === null) return [];
    return [{
      role: shaped.role,
      repository,
      holder: typeof shaped.holder === 'string' ? shaped.holder : 'terminal',
      since: typeof shaped.since === 'string' && shaped.since.length > 0 ? shaped.since : null,
      dormant: shaped.dormant === true,
      answers: typeof shaped.answers === 'number' && Number.isFinite(shaped.answers) ? shaped.answers : 0,
      scope,
      holderId: typeof shaped.holderId === 'string' && shaped.holderId.length > 0 ? shaped.holderId : null,
    }];
  });
}

/** The living holders (STEWARDROLE1a-i, D209 §2.2), each read field by field; one with no id is left out. */
function holdersOf(raw: unknown): HolderNow[] {
  if (!Array.isArray(raw)) return [];
  return raw.flatMap((each): HolderNow[] => {
    if (typeof each !== 'object' || each === null) return [];
    const shaped = each as Record<string, unknown>;
    if (typeof shaped.id !== 'string' || shaped.id.length === 0) return [];
    return [{
      id: shaped.id,
      since: typeof shaped.since === 'string' && shaped.since.length > 0 ? shaped.since : null,
      roles: strings(shaped.roles),
    }];
  });
}

/**
 * The owner's appointment of a role, or its turning off (SUPERVISOR1a-ii, D208 §2.3, §5; STEWARDROLE1a-i, D209 §2.2, §2.3): `on`
 * true appoints, which makes the window start a holder's tab, Daoris's own pinned harness with the key in its environment, or add the
 * role to a living holder; false turns that one appointment off at once. The scope is `repository:<name>` for a repository's role and
 * `machine` for the steward's; `share` is a later *Appoint…*'s choice while a holder lives, `own` or that holder's id. Sent from the
 * window alone (the census refuses it to a terminal). The answer is read field by field, so a refusal keeps its code; it never
 * carries a key. The route declares `sessions` and `driver`, so the state hub reads both again (D169 point 6).
 */
export function useSetAppointment() {
  return useMutation({
    mutationFn: async ({ role, repository, on, share }: { role: string; repository?: string; on: boolean; share?: string }): Promise<AppointmentAnswer> => {
      const scope = repository ? `repository:${repository}` : 'machine';
      const answer = await call<{ done?: boolean; why?: string | null; message?: string | null }>(
        'SET_APPOINTMENT', { role, scope, holder: 'terminal', on, ...(share ? { share } : {}) });
      return { done: answer?.done === true, why: answer?.why ?? null, message: answer?.message ?? '' };
    },
  });
}

/**
 * The person's grant or revoke of the standing confirmation (TERMTRUST1a, D201 point 5; the terminal-trust design §4.1): the presses
 * ticked, `[]` to turn it off, sent from the window alone (the census refuses it to a terminal). The answer is read field by field, so
 * a refusal keeps its code; a revoke `done` with `unwritten` is off for this run, its record unwritten. The route declares `driver`, so
 * the state hub reads the driver's state again (D169 point 6): this asks nothing again itself.
 */
export function useSetStandingConfirmation() {
  return useMutation({
    mutationFn: async (presses: readonly string[]): Promise<StandingAnswer> => {
      const answer = await call<{ done?: boolean; why?: string | null; message?: string | null }>(
        'SET_STANDING_CONFIRMATION', { presses: [...presses] });
      return { done: answer?.done === true, why: answer?.why ?? null, message: answer?.message ?? '' };
    },
  });
}

/**
 * Hear a press run under the standing confirmation (`STANDING_USED`, the terminal-trust design §5 step 4), and read the driver's
 * state again, whose `used` the *Notifications* line counts. Where the shell pushes that state (PUSH1a) its push carries the count, and
 * nothing is asked here.
 */
export function useStandingUsed() {
  const client = useQueryClient();
  useShenoraEvent('DAORIS', 'STANDING_USED', () => {
    void client.invalidateQueries({ queryKey: keys.driver, predicate: notPushed(client) });
  });
}

/**
 * The person key the shell holds for its host (D156 point 2, design §2.2), asked as the page loads and held in `data/api.ts`'s
 * memory, never in the query cache, the page's storage or a URL; a reload asks again. Answers whether one is held, and whether the
 * ask has settled. None in a browser, from a shell older than the route, or where the shell holds none.
 */
export function usePersonKey(): { held: boolean; settled: boolean } {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: keys.personKeyHeld,
    queryFn: async () =>
      (await holdPersonKey(async () => (await call<{ key?: string | null }>('PERSON_KEY'))?.key ?? null)) !== null,
    enabled: isAvailable,
    staleTime: Infinity,
    retry: false,
  });
  return { held: query.data === true, settled: !isAvailable || query.isSuccess || query.isError };
}

/**
 * Whether the window should say the host gates the person's acts with a key this page does not hold (PERSONDOOR1f, design §2.3's
 * adoption notice): asked once the page's own ask for the key has settled empty, of the host's proof of possession. A host that
 * proves a key this window was never handed was started by something other than this window's shell.
 */
export function useUnkeyedHost(person: { held: boolean; settled: boolean }): boolean {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: keys.hostKeyed,
    queryFn: ({ signal }) => api.hostHoldsAKey(signal),
    enabled: isAvailable && person.settled && !person.held,
    staleTime: Infinity,
    retry: false,
  });
  return isAvailable && person.settled && !person.held && query.data === true;
}

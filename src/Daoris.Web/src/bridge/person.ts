import { type QueryClient, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { api, holdPersonKey, type ServiceConfirmation } from '../data/api';
import { keys } from '../data/queries';
import type { Confirmation, ConfirmationAnswer, ConfirmationField, ConfirmationNamed } from '../person/Confirmations';
import {
  type DeskRequestNow, type HolderNow, type RoleHoldNow, type RoleTerminalAnswer, type StandingConfirmationNow, type StewardActNow, type StewardLandNow,
} from '../person/standing';
import { call, confirmBound } from './call';

// The person's door in the window: the person
// key the shell holds for its host, kept in memory; the presses a terminal sent over the pipe that wait for the person's Confirm;
// and, once the page holds the key, what waits at the service's own confirmations, as the shell tells it. Every route here is the window's alone: the
// pipe's census refuses each to a terminal, which asks and never answers.

/** A press a terminal sent over the pipe, waiting for the window, as `PERSON_CONFIRMATIONS` answers it and `CONFIRMATIONS` tells it. */
export type PipeConfirmation = {
  id: string; module: string; route: string; fields: ConfirmationField[];
  asker?: { process?: string | null; pid?: number | null } | null; asked: string;
  /** Whether a terminal still waits on its line for it; absent where the shell says nothing. */
  waitedOn?: boolean | null;
  /** That its Confirm is being judged against what it rests on: its buttons are replaced meanwhile. */
  checking?: boolean | null;
  /** The longest that judgement may take, in milliseconds, which the Confirm waits for. */
  checkWithin?: number | null;
  /** When a window first said its card is in front of the person; absent until then, and from a shell without it. */
  shownAt?: string | null;
  /** The values its sentence names by role (the modules' `PipeWording`), beside `fields`; absent from a shell without them. */
  named?: ConfirmationNamed[] | null;
  /** That it carries a key, so it is not kept across a restart; absent from a shell without it. */
  keyed?: boolean | null;
  /** That it waits again from its record after a restart, its asker the record's; absent from a shell without it. */
  restored?: boolean | null;
  /** That its Confirm also lets its terminal speak for the person in the conversation until it lets go; absent from a shell without it. */
  hold?: boolean | null;
  /** Where its terminal stands, as the pipe's code, where that is to be said; absent from a shell without it. */
  standing?: string | null;
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
  shown: waiting.shownAt != null,
  waitedOn: typeof waiting.waitedOn === 'boolean' ? waiting.waitedOn : null, checking: waiting.checking === true,
  checkWithin: typeof waiting.checkWithin === 'number' ? waiting.checkWithin : null,
  keyed: waiting.keyed === true, restored: waiting.restored === true, hold: waiting.hold === true,
  standing: typeof waiting.standing === 'string' ? waiting.standing : null,
});

/**
 * What a terminal asked this window to confirm over the pipe: read as the page mounts, for a window opened or reloaded
 * while one waits, then kept by the shell's `CONFIRMATIONS` at each change. None in a browser, which has no pipe.
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

/** What waits at the service's door as the shell tells it, numbered so the newest telling is kept. */
export type ServiceConfirmationsTold = { seq: number | null; waiting: ServiceConfirmation[] };

/** A telling or a read of what waits at the service, or null where it is of no shape this reads. */
function serviceOf(told: unknown): ServiceConfirmationsTold | null {
  const shaped = told as { seq?: unknown; waiting?: unknown } | null | undefined;
  if (!Array.isArray(shaped?.waiting)) return null;
  return {
    seq: typeof shaped.seq === 'number' ? shaped.seq : null,
    waiting: (shaped.waiting as ServiceConfirmation[]).filter((each) => typeof each?.id === 'string'),
  };
}

/**
 * What waits at the service for the person's confirmation, asked only while the page holds the
 * person key: its Confirm and Refuse take the key, and a host handed none lists none. **Told, not polled**:
 * the shell holds one wait on the service and tells every window what waits at each change (`SERVICE_CONFIRMATIONS`), so each
 * window counts the one list it is told; read as the page mounts, for a window opened mid-wait. A browser keeps the poll of the
 * service's list. `read` says whether this window has read it at all, so its first read is told apart from an arrival.
 */
export function useServiceConfirmations(held: boolean): { waiting: Confirmation[]; read: boolean } {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  useShenoraEvent<ServiceConfirmationsTold>('DAORIS', 'SERVICE_CONFIRMATIONS', (told) => {
    const now = serviceOf(told);
    const kept = client.getQueryData<ServiceConfirmationsTold>(keys.serviceTold);
    // Kept once this window has read, where it is newer: the bus may deliver one late, and the read may land after a telling.
    if (!now || kept === undefined) return;
    if (now.seq !== null && kept.seq !== null && now.seq <= kept.seq) return;
    client.setQueryData<ServiceConfirmationsTold>(keys.serviceTold, now);
  });
  const told = useQuery({
    queryKey: keys.serviceTold,
    queryFn: async (): Promise<ServiceConfirmationsTold> => {
      const read = serviceOf(await call<ServiceConfirmationsTold>('SERVICE_CONFIRMATIONS')) ?? { seq: null, waiting: [] };
      const kept = client.getQueryData<ServiceConfirmationsTold>(keys.serviceTold);
      return kept && kept.seq !== null && read.seq !== null && kept.seq >= read.seq ? kept : read;
    },
    enabled: isAvailable && held,
    // The telling keeps it current: nothing asks again by itself.
    staleTime: Infinity,
    retry: false,
  });
  const polled = useQuery({
    queryKey: keys.serviceAsks,
    queryFn: ({ signal }) => api.confirmations(signal),
    enabled: held && !isAvailable,
    refetchInterval: 5_000,
    retry: false,
  });
  const list = told.data ? told.data.waiting : polled.data;
  return { waiting: (list ?? []).filter((each) => each.state === 'waiting').map((each): Confirmation => ({
    source: 'service',
    id: each.id,
    route: `${each.method} ${each.route}`,
    act: each.act,
    fields: [...(each.values ?? []), ...(each.fields ?? [])],
    asker: null,
    asked: each.asked ?? null,
  })), read: list !== undefined };
}

/**
 * The person's Confirm or Refuse: a terminal's press by its confirmation's id over the bridge, the service's
 * at its own door with the key. One that no longer waits is answered, never thrown, for the card to say where it is pressed:
 * the bridge's code (`gone`), or the host's own sentence. A terminal's Confirm is answered once its grounds are
 * judged, so it waits as long as the press says that may take: `changed` with the
 * ground's reason and its values, or `unread` where they could not be read just now.
 */
export function useAnswerConfirmation() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async ({ waiting, confirm }: { waiting: Confirmation; confirm: boolean }): Promise<ConfirmationAnswer> => {
      if (waiting.source === 'pipe') {
        const bound = confirmBound(waiting.checkWithin);
        const answer = (await call('PERSON_CONFIRM', { id: waiting.id, confirm }, bound ? { timeoutMs: bound } : undefined)) as {
          answered?: boolean; why?: string | null; message?: string | null; reason?: string | null; values?: Record<string, unknown> | null;
        } | null;
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
 * The window's word that a terminal's press is in front of the person: kept once by the shell, which tells every window
 * by `CONFIRMATIONS`; it moves no clock. A service's confirmation has none: its door counts its
 * own. Answered, never thrown: one no longer waiting is `gone`, which says nothing here, since the telling takes its card away.
 */
export function useConfirmationShown() {
  return useMutation({
    mutationFn: async (waiting: Confirmation): Promise<boolean> => {
      if (waiting.source !== 'pipe') return false;
      const answer = (await call('PERSON_CONFIRM_SHOWN', { id: waiting.id })) as { shown?: boolean } | null;
      return answer?.shown === true;
    },
  });
}

const strings = (value: unknown): string[] =>
  (Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string') : []);

/**
 * `STATE`'s `standingConfirmation`, read field by field: the bridge leaves a null out, so each field is read defensively. Null where
 * the shell says none, or serves no pipe, which draws no row.
 */
export function standingOf(raw: unknown): StandingConfirmationNow | null {
  if (typeof raw !== 'object' || raw === null || Array.isArray(raw)) return null;
  const shaped = raw as Record<string, unknown>;
  return {
    holds: holdsOf(shaped.holds),
    holders: holdersOf(shaped.holders),
    stewardActs: stewardActsOf(shaped.stewardActs),
    // A missing desk reads as empty.
    stewardDesk: stewardDeskOf(shaped.stewardDesk),
    stewardLands: stewardLandsOf(shaped.stewardLands),
  };
}

/** The allowed lands waiting their window, each read field by field; one with no id, act, session or time is left out. */
function stewardLandsOf(raw: unknown): StewardLandNow[] {
  if (!Array.isArray(raw)) return [];
  return raw.flatMap((each): StewardLandNow[] => {
    if (typeof each !== 'object' || each === null) return [];
    const shaped = each as Record<string, unknown>;
    const ask = text(shaped.ask);
    const act = text(shaped.act);
    const landsAt = text(shaped.landsAt);
    if (ask === null || act === null || landsAt === null) return [];
    return [{ ask, act, landsAt, repository: text(shaped.repository), quest: text(shaped.quest), session: text(shaped.session) }];
  });
}

const text = (value: unknown): string | null => (typeof value === 'string' && value.length > 0 ? value : null);

const DESK_STATES: readonly DeskRequestNow['state'][] = ['open', 'answered', 'declined'];

/**
 * The steward's desk, in the order sent, each read field by field; one with no id, act, target, why or time,
 * or with a state this page does not know, is left out.
 */
function stewardDeskOf(raw: unknown): DeskRequestNow[] {
  if (!Array.isArray(raw)) return [];
  return raw.flatMap((each): DeskRequestNow[] => {
    if (typeof each !== 'object' || each === null) return [];
    const shaped = each as Record<string, unknown>;
    const id = text(shaped.id);
    const act = text(shaped.act);
    const target = text(shaped.target);
    const why = text(shaped.why);
    const filed = text(shaped.filed);
    const state = DESK_STATES.find((known) => known === shaped.state);
    if (id === null || act === null || target === null || why === null || filed === null || state === undefined) return [];
    return [{ id, act, target, why, filed, state, closed: text(shaped.closed), answeredBy: text(shaped.answeredBy), reason: text(shaped.reason) }];
  });
}

/** This run's steward acts, in the order sent, each read field by field; one with no id, act or time is left out. */
function stewardActsOf(raw: unknown): StewardActNow[] {
  if (!Array.isArray(raw)) return [];
  return raw.flatMap((each): StewardActNow[] => {
    if (typeof each !== 'object' || each === null) return [];
    const shaped = each as Record<string, unknown>;
    const id = text(shaped.id);
    const act = text(shaped.act);
    const at = text(shaped.at);
    if (id === null || act === null || at === null) return [];
    return [{
      id, act, at,
      repository: text(shaped.repository),
      quest: text(shaped.quest),
      session: text(shaped.session),
      standing: shaped.standing === true,
      waiting: shaped.waiting === true,
      allowed: shaped.allowed === true,
    }];
  });
}

/** The roles a terminal holds this run, each read field by field; one with no role, kind or holder is left out. */
function holdsOf(raw: unknown): RoleHoldNow[] {
  if (!Array.isArray(raw)) return [];
  return raw.flatMap((each): RoleHoldNow[] => {
    if (typeof each !== 'object' || each === null) return [];
    const shaped = each as Record<string, unknown>;
    const role = text(shaped.role);
    const kind = text(shaped.kind);
    const holder = text(shaped.holder);
    if (role === null || kind === null || holder === null) return [];
    return [{
      role, kind, holder,
      since: text(shaped.since),
      answers: typeof shaped.answers === 'number' && Number.isFinite(shaped.answers) ? shaped.answers : 0,
    }];
  });
}

/** The living holders, each read field by field; one with no id is left out. */
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
 * The start of the terminal that holds a role the catalogue staffs with the terminal: the window starts a holder's tab,
 * Daoris's own pinned harness with the key in its environment, or adds the role to a living holder; `share` is the choice while a
 * holder lives, `own` or that holder's id. Staffing a role, and turning its terminal off, is the role door's (`SET_ROLE`). Sent from
 * the window alone (the census refuses it to a terminal). The answer is read field by field, so a refusal keeps its code; it never
 * carries a key. The route declares `sessions` and `driver`, so the state hub reads both again.
 */
export function useStartRoleTerminal() {
  return useMutation({
    mutationFn: async ({ role, share }: { role: string; share?: string }): Promise<RoleTerminalAnswer> => {
      const answer = (await call('START_ROLE_TERMINAL', { role, ...(share ? { share } : {}) })) as
        { done?: boolean; why?: string | null; message?: string | null } | null;
      return { done: answer?.done === true, why: answer?.why ?? null, message: answer?.message ?? '' };
    },
  });
}

/**
 * The person key the shell holds for its host, asked as the page loads and held in `data/api.ts`'s
 * memory, never in the query cache, the page's storage or a URL; a reload asks again. Answers whether one is held, and whether the
 * ask has settled. None in a browser, or where the shell holds none.
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
 * Whether the window should say the host gates the person's acts with a key this page does not hold (the
 * adoption notice): asked once the page's own ask for the key has settled empty, of the host's proof of possession. A host that
 * proves a key this window is never handed is started by something other than this window's shell.
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

import { type ReactNode, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { canBeAsked, type Quest } from './api';
import { linksOf, toUpload } from './attachments';
import { useAsksPart } from './asks/AsksPart';
import { sentence } from './format';
import { buildChain } from './map/chain';
import { askItem, questsItem } from './opener';
import {
  useAcceptQuest, useDeleteQuest, useDismissConflict, usePersonDone, usePublishQuest, useQuests, useRegistry, useRespondQuest,
  useSessions,
} from './queries';
import { EMPTY_QUEST, QuestComposer, type QuestDraft } from './quests/QuestComposer';
import { QuestList } from './quests/QuestList';
import {
  answered, freshest, keptFilters, latestSessions, type QuestFilters, questFilters, questionOf, questStanding,
} from './quests/records';
import { QuestPage, QuestsMainNotice } from './quests/QuestPage';
import { namer } from './settings/namer';
import {
  retryNotice, useConsidered, useDriver, useHarnesses, useHistoryPlan, useNudge, useRemotes, useRetryQuest, useSetHold, useTrace,
  useTrustFolder, useUntrusted, useWorkflowRun, useWorkPlan,
} from './shell';
import { sittingBecause } from './signals';
import { failure, type Notify, useErrorNotify } from './ui';
import type { HistoryDoor } from './work/history';
import { useHistoryActs } from './work/historyActs';
import { stepState } from './work/review';
import { useReviewActs } from './work/reviewActs';
import type { Answered } from './work/InlineConfirm';
import { ListMore } from './work/ListPane';
import { type AbandonAnswer, wiredFor, type WorkDoor, type WorkTarget } from './work/pausing';
import type { TraceDoor } from './work/HowItCameToBe';
import type { ViewLayout } from './work/ViewFrame';
import { useWorkActs } from './work/workActs';

/** A radio item cannot carry an empty value, so "every receiver" travels as a sentinel. */
const EVERYONE = '*';

/**
 * **The Quests view** (D38, D40; FRAME1d, D118 §2): what it hands the frame (D118 §5), its list pane and its main
 * area. The list holds the asks, then the quests by where they are in their life, with *Receiver* and *Include
 * closed* in its ⋯; the main area holds the chosen quest's page or an ask's, with its acts in its header. A record
 * is the main area and a form a drawer (§3d): the two composers stay drawers.
 *
 * @remarks
 * **A hook, because a view hands the frame a value** (`ViewLayout`), as the Plugins view's is: the list and the main
 * area are drawn in two places the frame decides. The application holds it on every view, and says its errors only
 * while it is in front.
 *
 * **What it remembers is its list's** (`listPanes.ts`, §3f): the chosen item, a quest's id or `ask:<id>`, which every
 * door into the view names through the opener (§3i), and the filters. A record the list leaves out — a closed quest,
 * or one to another receiver — is still found for its page, among every quest.
 *
 * Publish and respond go through the same endpoints and the same `QuestExchange` judgement as every other door,
 * refusals surfaced verbatim — the service's sentence is the contract, so it is never translated or rephrased here.
 */
export function useQuestsView({
  active, chosen, onChoose, filters: kept, onFilters, notify, onAttend, onOpenRun, opening, onOpened, asking, onAsked,
}: {
  /**
   * The door into a run (WORKFLOW1c, the workflow design §7): its session attended, the side bar on its *Workflow*. Absent where
   * Sessions is not, a browser, which reads no run.
   */
  onOpenRun?: (session: string) => void;
  /** The view is in front: only then are its errors said. */
  active: boolean;
  /** The list's chosen item, which the application remembers (`daoris.list.quests.chosen`). */
  chosen: string | null;
  onChoose: (item: string | null) => void;
  /** The list's filters as kept (`daoris.list.quests.filters`). */
  filters: Record<string, unknown>;
  /** Null forgets them. */
  onFilters: (filters: Record<string, unknown> | null) => void;
  notify: Notify;
  /**
   * The door into Sessions (design §3): this view keeps the record summary and hands the session over rather than
   * growing a second console. Absent where Sessions is not — a browser has no frame to open.
   */
  onAttend?: (session: string) => void;
  /**
   * A draft handed in by a door — SURF6b's "send it back as a quest" arrives with the repository the work came from
   * already named. The composer opens on it; the person writes the rest, because the ask and its reason are the
   * part that has to travel (`repository-owns-its-work`). An event, consumed by identity.
   */
  opening?: { from?: string; to?: string } | null;
  onOpened?: () => void;
  /** The palette asked for the ask composer (INT4c) — an event like `opening`. */
  asking?: boolean;
  onAsked?: () => void;
}): ViewLayout {
  const { t } = useTranslation();
  const filters = questFilters(kept);
  const setFilters = (next: QuestFilters) => onFilters(keptFilters(next));
  const item = chosen ? questsItem(chosen) : null;
  const [composing, setComposing] = useState(false);
  const [draft, setDraft] = useState<QuestDraft>(EMPTY_QUEST);
  const [reading, setReading] = useState(false);
  const [askComposing, setAskComposing] = useState(false);
  // The door's last answer about a quest, kept until the list catches up with it.
  const [held, setHeld] = useState<Quest | null>(null);
  // The quest whose trust question is open (D73): only on the press, for the quest it was asked about.
  const [trustingFor, setTrustingFor] = useState<string | null>(null);

  // A door asked for the composer, pre-filled. Consumed on arrival: this is an event, not a state.
  // 🔴 Consumed by IDENTITY, not by the parent clearing it. A state update during render makes React re-run this
  // component at once, with the SAME props — so "if (opening) set…" saw the draft still there on every pass and
  // looped until React gave up ("Too many re-renders"): the door crashed the view it opened. The parent is told
  // from an effect, because updating another component during this one's render is its own warning.
  const [arrived, setArrived] = useState<typeof opening>(null);
  if (opening && opening !== arrived) {
    setArrived(opening);
    setDraft({ ...EMPTY_QUEST, from: opening.from ?? '', to: opening.to ?? '' });
    setComposing(true);
  }
  useEffect(() => { if (opening) onOpened?.(); }, [opening, onOpened]);

  // The ask composer, opened by the list's ＋ or by the palette's event — consumed by identity for `opening`'s
  // reason, and forgotten once the holder clears it.
  const [askSeen, setAskSeen] = useState(false);
  if (asking && !askSeen) {
    setAskSeen(true);
    setAskComposing(true);
  }
  if (!asking && askSeen) setAskSeen(false);
  useEffect(() => { if (asking) onAsked?.(); }, [asking, onAsked]);

  const quests = useQuests(filters.to, filters.closed);
  // Every quest, closed ones included: a page for one the list leaves out, the chain a page shows (MAP1), whose
  // step before this one is usually closed and very often somebody else's, and the question a quest waits on.
  const everything = useQuests(null, true);
  const registry = useRegistry();
  const sessions = useSessions(null, true);
  const driver = useDriver();
  // What a person calls an account (ACCTNAME1, D152 §4.2): the roster's one namer, for a quest's session line, its chain and
  // its trace. In a browser there is no roster, and each record's id is said.
  const harnesses = useHarnesses();
  const nameOf = namer(t, Array.isArray(harnesses.data?.harnesses) ? harnesses.data.harnesses : []);
  // A quest just published is looked at now, not at the driver's next poll.
  const nudge = useNudge();
  const considered = useConsidered().data ?? [];
  const setHold = useSetHold();
  // A start the driver is holding for the agent's trust (D73), and the person's grant of it.
  const untrusted = useUntrusted().data ?? [];
  const trust = useTrustFolder();
  // A quest parked by its strikes (DRV6), started again — `daoris driver retry`'s screen twin (RETRY1).
  const retry = useRetryQuest();
  const publish = usePublishQuest();
  const respond = useRespondQuest();
  const dismiss = useDismissConflict();
  const remove = useDeleteQuest();
  // The person's yes to a done's departure (DRIFT1d2): the service's accept door, `daoris-driver quest accept`'s twin.
  const accept = useAcceptQuest();
  // The person's done (QUESTCLOSE1): the service's own door for it, `daoris-driver quest done`'s twin.
  const personDone = usePersonDone();
  // The chosen quest's work on this machine (PAUSE1e, D132 §7.1): its plan while Quests is in front, and the three presses.
  const work = useWorkPlan(item && 'quest' in item ? { scope: 'quest', id: item.quest } : null, { enabled: active });
  const wiring = useRemotes().data;
  const workActs = useWorkActs({ notify });
  // How a quest came to be (TRACE1b): read only while its section is open, for the quest it was opened on, since the read
  // takes every session record and quest. Another quest's page opens folded.
  const [tracing, setTracing] = useState<string | null>(null);
  const trace = useTrace('quest', tracing);
  // The last abandon's answer, for the quest it was of, said on its page before the record catches up.
  const [abandonedNow, setAbandonedNow] = useState<{ id: string; answer: AbandonAnswer; at: string } | null>(null);
  // The chosen quest as its page shows it: the list's copy, or the door's last answer while the list catches up.
  const shownQuest = item && 'quest' in item
    ? freshest(everything.data?.find((quest) => quest.id === item.quest), held?.id === item.quest ? held : null)
    : undefined;
  // Its clears (HIST1e, D153 §6.1): the plans of its work and its failed sessions, asked only while Quests is in front and the
  // quest is closed, since a clear never takes work in progress; and the second press.
  // Where the chosen quest's chain's work here stands in its workflow (WORKFLOW1c): asked while Quests is in front and a door can
  // open the run, and again at every tick.
  const questRun = useWorkflowRun(shownQuest ? { quest: shownQuest.id } : null, { enabled: active && onOpenRun !== undefined });
  const closedQuest = shownQuest?.status === 'Done' || shownQuest?.status === 'Declined';
  const historyOf = useHistoryPlan(shownQuest ? { scope: 'quest', id: shownQuest.id } : null, { enabled: active && closedQuest });
  const failedOf = useHistoryPlan(shownQuest ? { scope: 'failed', id: shownQuest.id } : null, { enabled: active && closedQuest });
  const historyActs = useHistoryActs({ notify });
  // A set-up step's review (REVIEWENV1g): the presses' one owner, and whether Daoris still serves each waiting set-up's tab here.
  const reviewActs = useReviewActs({ notify });
  const inReview = driver.data?.inReview;
  // Every query this view renders from — a session surface or driver bridge that fails silently is
  // indistinguishable from a family with no driver attached. Said once, while the view is in front (D118 §3h).
  useErrorNotify(active ? quests.error ?? registry.error ?? sessions.error ?? driver.error : null, notify);

  const asks = useAsksPart({
    active,
    closed: filters.closed,
    chosen: item && 'ask' in item ? item.ask : null,
    quests: everything.data,
    onChoose,
    notify,
    onAttend,
    onOpenRun,
    composing: askComposing,
    onComposingChange: setAskComposing,
  });

  const latest = latestSessions(sessions.data ?? []);
  // Only what the host says can be asked is offered (D70: an adopter, or a repository registered with a root) —
  // anything else would invite an ask the service refuses. The service still holds the judgement.
  const adopters = (registry.data ?? []).filter(canBeAsked).map((row) => row.repository);
  // Known to be nobody, not merely not loaded yet: a composer that flashed "nobody" would be a lie.
  const nobody = registry.data !== undefined && adopters.length === 0;
  const target = (registry.data ?? []).find((row) => row.repository === draft.to);
  const busy = publish.isPending || respond.isPending || remove.isPending || accept.isPending || personDone.isPending || reading;
  // A quest's lanes as its repository names them (D115 §2.2): the id, and its title where the registration gives
  // one. The ids are the repository's words, so they are shown as it spells them.
  const laneNames = (quest: Quest) => {
    const declared = (registry.data ?? []).find((row) => row.repository === quest.to)?.lanes ?? [];
    return (quest.lanes ?? []).map((id) => {
      const title = declared.find((lane) => lane.id === id)?.title;
      return title ? `${id} (${title})` : id;
    }).join(', ');
  };

  const onPublish = async () => {
    // Read whole only now — a file chosen and then removed was never read at all.
    setReading(true);
    let attachments;
    try {
      attachments = await Promise.all(draft.files.map(toUpload));
    } catch (e) {
      notify(sentence(e), 'error');
      return;
    } finally {
      setReading(false);
    }

    const { from, to, title, body } = draft;
    const then = draft.step ? [draft.step] : [];
    // The person's short title (SESSUX1j), sent only when written: none leaves the service to name it from its words.
    const short = draft.short?.trim();
    publish.mutate({ from, to, title, body, links: linksOf(draft.links), attachments, then, ...(short ? { short } : {}) }, {
      onSuccess: (result) => {
        notify(result.message);
        setDraft(EMPTY_QUEST);
        setComposing(false);
        // The quest just published opens on its page, as an ask's record opens on its answer.
        setHeld(result.quest);
        onChoose(result.quest.id);
        nudge();
      },
      onError: failure(notify),
    });
  };

  // The page stays on the quest as it now stands: a take or a done moves it, and its page says so.
  const onRespond = (quest: Quest, action: 'take' | 'done' | 'decline', why: string | null = null, answered?: Answered) => {
    // The person's done (QUESTCLOSE1) goes to its own door with their words, told back to its ask (UXFIX2): a refusal is said
    // inside it, and the page stays on the quest, since nothing happened to it.
    if (action === 'done') {
      personDone.mutate({ id: quest.id, note: why }, {
        onSuccess: (result) => {
          notify(result.message);
          answered?.done();
          setHeld(result.quest);
        },
        onError: (error) => (answered ? answered.refused(sentence(error)) : failure(notify)(error)),
      });
      return;
    }

    respond.mutate({ id: quest.id, action, reason: why }, {
      onSuccess: (result) => {
        notify(result.message);
        setHeld(result.quest);
      },
      onError: failure(notify),
    });
  };

  // A person's dismissal (SYNC6c): the quest itself did not move — only what it was waiting on.
  const onDismiss = (quest: Quest, machine: string, sequence: number) =>
    dismiss.mutate({ id: quest.id, machine, sequence }, {
      onSuccess: (result) => {
        notify(result.message);
        setHeld(result.quest);
      },
      onError: failure(notify),
    });

  // A quest made by mistake, deleted (D95): the list chooses nothing, on the service's sentence. A refusal is its
  // sentence too, said inside the ask that was pressed (UXFIX2), and the page stays on the quest, because nothing happened
  // to it.
  const onDelete = (quest: Quest, answered: Answered) =>
    remove.mutate(quest.id, {
      onSuccess: (result) => {
        notify(result.message);
        answered.done();
        setHeld(null);
        onChoose(null);
      },
      onError: (error) => answered.refused(sentence(error)),
    });

  // The person's yes to a departure (DRIFT1d2, D133 §4): the page stays on the quest as the answer left it, released, and
  // a refusal (nothing waits for a yes) is the service's sentence, the quest unmoved.
  const onAccept = (quest: Quest) =>
    accept.mutate(quest.id, {
      onSuccess: (result) => {
        notify(result.message);
        setHeld(result.quest);
      },
      onError: failure(notify),
    });

  // The ＋'s kinds (D118 §2): asking first, since the regular task enters at the workspace (D65), then a quest to a
  // repository the person already knows.
  const kinds = [{ id: 'ask', label: t('asks.ask') }, { id: 'quest', label: t('quests.new') }];
  const make = (kind?: string) => {
    if (kind === 'ask') setAskComposing(true);
    else setComposing(true);
  };

  // What the page is handed of this machine's driver for the quest: nothing in a browser, which has none (D47 §4).
  const workDoor = (quest: Quest): WorkDoor | undefined => {
    if (!work.available) return undefined;
    const target: WorkTarget = { scope: 'quest', id: quest.id };
    return {
      // Asked by the chosen quest's id, which is the page's.
      plan: work.plan,
      wired: wiredFor(wiring, quest.workspace),
      busy: workActs.busy,
      outcome: abandonedNow?.id === quest.id ? abandonedNow : null,
      onPause: (done) => workActs.pause(target, () => done()),
      onResume: () => workActs.resume(target),
      onAbandon: (reason, pieces, done) => workActs.abandon(target, reason, pieces, (answer) => {
        setAbandonedNow({ id: quest.id, answer, at: new Date().toISOString() });
        done();
      }),
    };
  };

  // What the page is handed of its clears: nothing in a browser, which has no driver and no home (D47 §4). A quest's clear
  // takes its record, so the page closes and the list it came from is shown (§6.1); its failed sessions' leaves it.
  const historyDoor = (quest: Quest): HistoryDoor | undefined => {
    if (!historyOf.available) return undefined;
    return {
      plan: historyOf.plan?.id === quest.id ? historyOf.plan : null,
      failed: failedOf.plan?.id === quest.id ? failedOf.plan : null,
      busy: historyActs.busy,
      onClear: (target, units, answered) => historyActs.clear(target, units, {
        done: () => {
          answered.done();
          if (target.scope === 'quest') {
            setHeld(null);
            onChoose(null);
          }
        },
        refused: answered.refused,
      }),
    };
  };

  const pageOf = (quest: Quest): ReactNode => {
    const session = latest.get(quest.id) ?? null;
    const hold = untrusted.find((candidate) => candidate.quest === quest.id) ?? null;
    // A door into Sessions only where Sessions exists and a driver answers (D46 §6).
    const attend = onAttend && driver.data ? onAttend : undefined;
    return (
      <QuestPage
        key={quest.id}
        quest={quest}
        lanes={quest.lanes?.length ? laneNames(quest) : undefined}
        question={questionOf(quest, everything.data ?? [])}
        sitting={sittingBecause(considered, quest.id)}
        hold={hold}
        chain={buildChain(quest.id, everything.data ?? quests.data ?? [quest], sessions.data ?? [])}
        session={session}
        nameOf={nameOf}
        busy={busy}
        retrying={retry.isPending}
        trusting={trustingFor === quest.id}
        granting={trust.isPending}
        dismissing={dismiss.isPending}
        accepting={accept.isPending}
        onRespond={(action, reason, answered) => onRespond(quest, action, reason ?? null, answered)}
        // The service's own door, so a browser on this machine says yes as the desktop does (D50).
        onAccept={() => onAccept(quest)}
        onDelete={(answered) => onDelete(quest, answered)}
        onDismiss={(machine, sequence) => onDismiss(quest, machine, sequence)}
        // A driver's verdicts and holds reach only a shell, so in a browser neither act is ever offered. Said as the
        // driver answered which it did (D126 §3.4), in the one sentence a session's *Try again* says too.
        onRetry={() => retry.mutate({ quest: quest.id }, {
          onSuccess: (state) => notify(t(...retryNotice(quest.id, state))),
          onError: failure(notify),
        })}
        onTrusting={(open) => setTrustingFor(open ? quest.id : null)}
        onGrant={(granted) => trust.mutate(granted, {
          onSuccess: (answer) => {
            notify(answer.message, answer.verified ? 'ok' : 'error');
            setTrustingFor(null);
          },
          onError: failure(notify),
        })}
        onOpenQuest={(id) => onChoose(id)}
        onAttend={attend}
        onOpenAsk={(id) => onChoose(askItem(id))}
        work={workDoor(quest)}
        history={historyDoor(quest)}
        trace={traceDoor(quest)}
        // A set-up step's review (REVIEWENV1g): its verdict goes to the step, its skip there, and *Set it up* is the work's,
        // never this page's, since a set-up step follows the work it shows.
        review={quest.setUpIn ? {
          acts: reviewActs.actsFor({ state: stepState(quest), step: quest.id, record: quest }),
          busy: reviewActs.busy,
          served: inReview ? inReview.find((row) => row.quest === quest.id)?.served ?? false : null,
        } : undefined}
        // A shell older than the route answers something else, which is no run.
        workflow={Array.isArray(questRun.data?.runs) && onOpenRun
          ? { runs: questRun.data.runs, onOpen: (run) => { if (run.session) onOpenRun(run.session); } }
          : undefined}
      />
    );
  };

  // What the page is handed of the trace: nothing in a browser, whose page has no driver to read this machine's records.
  const traceDoor = (quest: Quest): TraceDoor | undefined => {
    if (!trace.available) return undefined;
    const open = tracing === quest.id;
    return {
      open,
      onToggle: () => setTracing(open ? null : quest.id),
      answer: open ? trace.query.data : undefined,
      reading: open && trace.query.isPending,
      refusal: open && trace.query.error ? sentence(trace.query.error) : null,
    };
  };

  const main = item && 'ask' in item
    ? asks.page
    : item
      ? shownQuest
        ? pageOf(shownQuest)
        : everything.data === undefined && everything.error
          ? <QuestsMainNotice state="unanswered" sentence={sentence(everything.error)} />
          : <QuestsMainNotice state={everything.data === undefined ? 'loading' : 'gone'} gone="quest" />
      : <QuestsMainNotice state="none" actions={kinds} onAct={make} />;

  const rows = (quests.data ?? []).map((quest) => {
    const question = questionOf(quest, everything.data ?? []);
    return {
      quest,
      session: latest.get(quest.id)?.state ?? null,
      // Once its question is answered the wait is no longer the news (D79).
      waits: question && !answered(question.quest) ? question.id : null,
      sitting: quest.status === 'Open' ? sittingBecause(considered, quest.id) : null,
    };
  });
  // A receiver kept from before stays choosable, so the filter that names it can be seen and undone.
  const receivers = [...adopters, ...(filters.to && !adopters.includes(filters.to) ? [filters.to] : [])];
  const listed = quests.data !== undefined;
  const headline = filters.to
    ? t('quests.empty.headlineFor', { repository: filters.to })
    : t('quests.empty.headlineAll');

  return {
    list: {
      view: 'quests',
      name: t('nav.quests'),
      labels: { open: t('quests.list.open'), close: t('quests.list.close'), resize: t('quests.list.resize') },
      make: { label: t('quests.list.add'), kinds, onMake: make },
      // The list's ⋯ (D118 §2): whose quests it holds, and whether closed ones are among them, remembered (§3f).
      more: (
        <ListMore
          label={t('quests.list.more')}
          choice={{
            label: t('quests.addressedTo'),
            value: filters.to ?? EVERYONE,
            options: [{ value: EVERYONE, label: t('quests.everyone') }, ...receivers.map((name) => ({ value: name, label: name }))],
            onChoose: (value) => setFilters({ ...filters, to: value === EVERYONE ? null : value }),
          }}
          items={[{ id: 'closed', label: t('quests.includeClosed'), checked: filters.closed }]}
          onChoose={(id) => { if (id === 'closed') setFilters({ ...filters, closed: !filters.closed }); }}
        />
      ),
      loading: quests.isPending || asks.loading,
      empty: listed && rows.length === 0 && asks.rows.length === 0
        ? { headline, body: t('quests.empty.body') }
        : undefined,
      chosen,
      // A remembered record reopens only while it still waits (UX6b): yesterday's done quest opens nothing chosen.
      standing: item && 'ask' in item ? asks.standing : item ? questStanding(shownQuest, everything.data !== undefined) : undefined,
      body: (
        <QuestList
          asks={asks.rows}
          quests={rows}
          closed={filters.closed}
          filteredTo={filters.to}
          empty={headline}
          unanswered={!listed && quests.error ? sentence(quests.error) : null}
          chosen={chosen}
          resuming={setHold.isPending}
          onChoose={onChoose}
          // A hold is this machine's driver's, so only a shell's list ever says one to lift.
          onResume={(repository) => setHold.mutate({ repository, held: false }, { onError: failure(notify) })}
        />
      ),
    },
    main: (
      <>
        {main}
        {composing && (
          <QuestComposer
            draft={draft}
            onChange={setDraft}
            receivers={adopters}
            nobody={nobody}
            caution={target && !target.registered ? target.repository : null}
            busy={busy}
            onPublish={() => void onPublish()}
            onCancel={() => setComposing(false)}
          />
        )}
        {asks.composer}
      </>
    ),
  };
}

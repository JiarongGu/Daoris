import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { type Ask, canBeAsked, type Quest } from '../api';
import { linksOf, toUpload } from '../attachments';
import { NO_CARRY } from '../compose/carry';
import { sentence } from '../format';
import { askItem } from '../opener';
import {
  useAnswerGoAhead, useAsk, useAsks, useChooseAskReview, useCloseAsk, useDeleteAsk, usePublishAsk, useRegistry, useSessions,
} from '../queries';
import { useScope } from '../scope';
import { namer } from '../settings/namer';
import { useConsidered, useDriver, useHarnesses, useHistoryPlan, useNudge, useRemotes, useWorkflowRun, useWorkPlan } from '../shell';
import { workspaceOf, workspacesOf } from '../workspaces';
import { failure, type Notify, useErrorNotify } from '../ui';
import { askStanding, freshest } from '../quests/records';
import type { ChoiceStanding } from '../work/listPanes';
import type { AskRowFacts } from '../quests/QuestList';
import { QuestsMainNotice } from '../quests/QuestPage';
import type { HistoryDoor } from '../work/history';
import { useHistoryActs } from '../work/historyActs';
import type { Answered } from '../work/InlineConfirm';
import { type AbandonAnswer, wiredFor, type WorkDoor, type WorkTarget } from '../work/pausing';
import { useWorkActs } from '../work/workActs';
import { questName } from '../work/identity';
import { AskComposer, type AskDraft } from './AskComposer';
import { AskPage } from './AskPage';
import { asksInOrder } from './AskRow';

const EMPTY_DRAFT: AskDraft = { circle: '', sentence: '', to: '', review: '', reviewWords: '', ...NO_CARRY };

/** What the asks hand Quests' view: their rows, the chosen ask's page, and the composer while it is open. */
export type AsksPart = {
  rows: AskRowFacts[];
  /** The asks' first load. */
  loading: boolean;
  /** The chosen ask's page, or the main area's notice for one loading or gone; null where no ask is chosen. */
  page: ReactNode | null;
  /** Whether the chosen ask still waits on something (UX6b), so a remembered one reopens; absent where none is chosen. */
  standing?: ChoiceStanding;
  composer: ReactNode | null;
};

/**
 * **The asks' half of Quests** (INT4c; FRAME1d, D118 §2): the screen twin of `daoris-driver ask` (D50) — the
 * composer that makes one, the rows at the head of the list, and the chosen one's page in the main area with its
 * two acts. A hook, since the rows and the page are drawn where the frame decides (`useQuestsView` holds it).
 *
 * @remarks
 * **An ask is made in ONE circle**: the one the page is scoped to, or the only one the machine holds.
 * Scoped to none of several, the composer asks which — "every circle" names nobody, and assuming
 * `default` would ask somewhere the person did not choose.
 *
 * **The page shows the freshest record**: the list's copy, or the door's last answer about it while the list has
 * not caught up, so a move made elsewhere shows and the person's own press is not undone. A closed or done ask the
 * list leaves out is still found for its page, among every ask.
 *
 * Every answer is the service's sentence, verbatim (frontend §4a). A refused publish still leaves the
 * ask kept with its proposal (INT4a), which is why the list is asked again whatever the door said.
 *
 * **Its work is paused, resumed and abandoned on its page** (PAUSE1e, D132 §7.1): the chosen ask's plan is asked of this
 * machine's driver while Quests is in front, and each press goes to the one owner (`workActs.ts`). An abandon's answer is
 * kept for the page until the record says the same. A browser has no driver, and its page names the terminal's commands.
 * The page lists that work with the driver's last look at each quest (the tick's `considered`), which says why one sits
 * (PAUSE1h).
 *
 * The organism: it holds the hooks so the row, the page and the composer below it hold none (components §2).
 */
export function useAsksPart({
  active, closed, chosen, quests, onChoose, notify, onAttend, onOpenRun, composing, onComposingChange,
}: {
  /**
   * The door into a run (WORKFLOW1c, the workflow design §7): its session attended, the side bar on its *Workflow*. Absent where
   * Sessions is not, a browser, which reads no run.
   */
  onOpenRun?: (session: string) => void;
  /** Quests is in front: only then is an error said. */
  active: boolean;
  /** Closed and done asks are listed too, as closed quests are. */
  closed: boolean;
  /** The ask the list has chosen, by its id. */
  chosen: string | null;
  /** Every quest the page holds, closed ones too: an ask's page names what it became by its title. */
  quests: readonly Quest[] | undefined;
  /** The list's choice: an ask's item after asking, a quest the ask became, or nothing after a delete. */
  onChoose: (item: string | null) => void;
  notify: Notify;
  /** The door into Sessions for an ask's intake session — absent where Sessions is (a browser). */
  onAttend?: (session: string) => void;
  /** Whether the composer is open — the list's `＋` and the palette open it; this closes it. */
  composing: boolean;
  onComposingChange: (composing: boolean) => void;
}): AsksPart {
  const scope = useScope();
  const asks = useAsks(closed);
  // Every ask, closed and done ones too, for the page of one the list leaves out.
  const every = useAsks(true);
  // Whether this machine sets an intake agent (UX5 U34): "" is off, and a door with no driver, or a
  // shell older than the intake, cannot say, so the composer promises neither.
  const driverState = useDriver().data;
  const intakeAdapter = driverState?.intakeAdapter;
  const intakeSet = intakeAdapter === undefined ? null : intakeAdapter !== '';
  // Scoped like every cross-repository read: scoped, its rows are that circle's and the circle is fixed;
  // unscoped, they are every circle's, which is exactly the list the composer asks the person to pick from.
  const family = useRegistry();
  const sessions = useSessions(null, true);
  // What a person calls an account (ACCTNAME1, D152 §4.2): an intake's line names its account as the agent's page does.
  const { t } = useTranslation();
  const harnesses = useHarnesses();
  const nameOf = namer(t, Array.isArray(harnesses.data?.harnesses) ? harnesses.data.harnesses : []);
  const ask = useAsk();
  const nudge = useNudge();
  const publish = usePublishAsk();
  const close = useCloseAsk();
  const remove = useDeleteAsk();
  const answerGoAhead = useAnswerGoAhead();
  // The person's review choice for an ask's work (REVIEWENV1g, design §1.5): the service's door, the latest standing.
  const chooseReview = useChooseAskReview();
  useErrorNotify(active ? asks.error : null, notify);

  const [draft, setDraft] = useState<AskDraft>(EMPTY_DRAFT);
  const [reading, setReading] = useState(false);
  // The door's last answer about an ask, kept until the list catches up with it.
  const [held, setHeld] = useState<Ask | null>(null);
  // The chosen ask's work on this machine (PAUSE1e): its plan while Quests is in front, and the three presses.
  const work = useWorkPlan(chosen ? { scope: 'ask', id: chosen } : null, { enabled: active });
  // Where the chosen ask's work stands in its workflow (WORKFLOW1c): its runs, one per chain and repository, while Quests is in
  // front and a door can open one.
  const runs = useWorkflowRun(chosen ? { ask: chosen } : null, { enabled: active && onOpenRun !== undefined });
  const wiring = useRemotes().data;
  const workActs = useWorkActs({ notify });
  // The driver's last look at each quest, which says why one of the chosen ask's work sits (PAUSE1h).
  const considered = useConsidered().data ?? [];
  // The last abandon's answer, for the ask it was of, said on its page before the record catches up.
  const [abandonedNow, setAbandonedNow] = useState<{ id: string; answer: AbandonAnswer; at: string } | null>(null);
  const shown = chosen
    ? freshest(every.data?.find((candidate) => candidate.id === chosen), held?.id === chosen ? held : null)
    : undefined;
  // Its clear (HIST1e, D153 §6.1): the plan of its work, asked only while Quests is in front and the ask is done or closed,
  // since a clear never takes work in progress; and the second press.
  const historyOf = useHistoryPlan(
    shown ? { scope: 'ask', id: shown.id } : null,
    { enabled: active && (shown?.state === 'Done' || shown?.state === 'Closed') },
  );
  const historyActs = useHistoryActs({ notify });

  const circles = workspacesOf(family.data ?? []);
  const fixed = scope.workspace ?? (circles.length === 1 ? circles[0] : null);
  const receiversIn = (circle: string) => (family.data ?? [])
    .filter((row) => canBeAsked(row) && workspaceOf(row) === circle)
    .map((row) => row.repository)
    .sort();
  // The review environments a circle's rules declare (REVIEWENV1a), by name: its own rule's and each of its repositories', as
  // this machine's driver reads them. A browser has no driver, and offers only `on` and `off`.
  const environmentsIn = (circle: string): string[] => {
    const repositories = new Set((family.data ?? []).filter((row) => workspaceOf(row) === circle).map((row) => row.repository));
    const rules = [
      ...(driverState?.workspaceReviews ?? []).filter((row) => row.workspace === circle).map((row) => row.rule),
      ...(driverState?.reviews ?? []).filter((row) => repositories.has(row.repository)).map((row) => row.rule),
    ];
    return [...new Set(rules.flatMap((rule) => (rule.none ? [] : rule.environments.map((each) => each.name))))].sort();
  };
  // Each quest the page holds by its name (SESSUX1j): the short title, else the title.
  const questTitles = Object.fromEntries((quests ?? []).map((quest) => [quest.id, questName(quest)]));
  const busy = ask.isPending || publish.isPending || close.isPending || remove.isPending || answerGoAhead.isPending
    || chooseReview.isPending || reading;
  const intakeOf = (item: Ask) => (item.intake ? sessions.data?.find((session) => session.id === item.intake) ?? null : null);

  const onAsk = async () => {
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

    const workspace = fixed ?? draft.circle;
    ask.mutate({
      workspace,
      sentence: draft.sentence.trim(),
      links: linksOf(draft.links),
      attachments,
      // Unnamed, the declarations propose and nothing is published; named, it is published at once.
      ...(draft.to ? { to: draft.to } : {}),
      // The person's review choice for its work (REVIEWENV1g, design §1.5), with their words; none leaves each rule to decide.
      ...(draft.review ? { review: draft.review, ...(draft.reviewWords?.trim() ? { reviewWords: draft.reviewWords.trim() } : {}) } : {}),
    }, {
      onSuccess: (result) => {
        notify(result.message);
        setDraft(EMPTY_DRAFT);
        onComposingChange(false);
        // The record opens on the answer, in the main area, before the list has caught up with it.
        setHeld(result.ask);
        onChoose(askItem(result.ask.id));
        // An intake or a published quest is the driver's next move: looked at now, not at the poll.
        nudge();
      },
      onError: failure(notify),
    });
  };

  const onPublish = (id: string, to: string) => publish.mutate({ id, to }, {
    onSuccess: (result) => { notify(result.message); setHeld(result.ask); nudge(); },
    onError: failure(notify),
  });

  const onClose = (id: string, reason: string) => close.mutate({ id, reason }, {
    onSuccess: (result) => { notify(result.message); setHeld(result.ask); },
    onError: failure(notify),
  });

  // The person's yes or no to a go-ahead (KNOWUSE1a): the record as the door answers it, and a nudge, since a session that
  // waits on it is the driver's next move once the person answers it too.
  const onAnswerGoAhead = (id: string, number: number, approved: boolean, words?: string) =>
    answerGoAhead.mutate({ id, number, approved, words }, {
      onSuccess: (result) => { notify(result.message); setHeld(result.ask); nudge(); },
      onError: failure(notify),
    });

  // Deleted with every quest asked by it (D95): the record is gone, so the list chooses nothing, on the service's
  // sentence. A refusal is the service's sentence too, said inside the ask that was pressed (UXFIX2), and the page stays on
  // the ask as it was.
  const onDelete = (id: string, answered: Answered) => remove.mutate(id, {
    onSuccess: (result) => { notify(result.message); answered.done(); setHeld(null); onChoose(null); },
    onError: (error) => answered.refused(sentence(error)),
  });

  // What the page is handed of its clear: nothing in a browser, which has no driver and no home (D47 §4). The clear takes
  // the ask's record, so the page closes and the list it came from is shown (§6.1).
  const historyDoor = (item: Ask): HistoryDoor | undefined => (historyOf.available
    ? {
        plan: historyOf.plan?.id === item.id ? historyOf.plan : null,
        busy: historyActs.busy,
        onClear: (target, units, answered) => historyActs.clear(target, units, {
          done: () => {
            answered.done();
            setHeld(null);
            onChoose(null);
          },
          refused: answered.refused,
        }),
      }
    : undefined);

  // What the page is handed of this machine's driver for the ask: nothing in a browser, which has none (D47 §4).
  const workDoor = (item: Ask): WorkDoor | undefined => {
    if (!work.available) return undefined;
    const target: WorkTarget = { scope: 'ask', id: item.id };
    return {
      // Asked by the chosen ask's id, which is the page's.
      plan: work.plan,
      wired: wiredFor(wiring, item.workspace),
      busy: workActs.busy,
      outcome: abandonedNow?.id === item.id ? abandonedNow : null,
      onPause: (done) => workActs.pause(target, () => done()),
      onResume: () => workActs.resume(target),
      onAbandon: (reason, pieces, done) => workActs.abandon(target, reason, pieces, (answer) => {
        setAbandonedNow({ id: item.id, answer, at: new Date().toISOString() });
        done();
      }),
    };
  };

  const page = !chosen
    ? null
    : shown
      ? (
        <AskPage
          key={shown.id}
          ask={shown}
          receivers={receiversIn(shown.workspace)}
          questTitles={questTitles}
          intake={intakeOf(shown)}
          onAttend={onAttend}
          busy={busy}
          onPublish={(to) => onPublish(shown.id, to)}
          onClose={(reason) => onClose(shown.id, reason)}
          onDelete={(answered) => onDelete(shown.id, answered)}
          onOpenQuest={(id) => onChoose(id)}
          onAnswerGoAhead={(number, approved, words) => onAnswerGoAhead(shown.id, number, approved, words)}
          environments={environmentsIn(shown.workspace)}
          // The record as the door answers it, so the page shows the choice before the list catches up; a refusal (a
          // repository that declares no environment) is the service's sentence.
          onChooseReview={(choice, words) => chooseReview.mutate({ id: shown.id, choice, words }, {
            onSuccess: (result) => { notify(result.message); setHeld(result.ask); },
            onError: failure(notify),
          })}
          work={workDoor(shown)}
          history={historyDoor(shown)}
          considered={considered}
          nameOf={nameOf}
          // A shell older than the route answers something else, which is no run.
          workflow={Array.isArray(runs.data?.runs) && onOpenRun
            ? { runs: runs.data.runs, onOpen: (run) => { if (run.session) onOpenRun(run.session); } }
            : undefined}
        />
      )
      : <QuestsMainNotice state={every.data === undefined && !every.error ? 'loading' : 'gone'} gone="ask" />;

  return {
    rows: asksInOrder(asks.data ?? []).map((item) => ({ ask: item, intake: intakeOf(item)?.state ?? null })),
    loading: asks.isPending,
    page,
    standing: chosen ? askStanding(shown, every.data !== undefined) : undefined,
    composer: composing && (
      <AskComposer
        draft={draft}
        onChange={setDraft}
        fixed={fixed}
        circles={circles}
        receivers={receiversIn(fixed ?? draft.circle)}
        environments={environmentsIn(fixed ?? draft.circle)}
        busy={busy}
        onSubmit={() => void onAsk()}
        onCancel={() => onComposingChange(false)}
        intake={intakeSet}
      />
    ),
  };
}

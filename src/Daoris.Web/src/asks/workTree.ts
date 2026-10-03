import type { Consideration } from '../signals';
import type { Line, PausedByFact, WorkPlan, WorkPlanQuest, WorkPlanSession } from '../work/pausing';

// An ask's work as its page lists it (PAUSE1h, D132 §7.1, `docs/2026-10-02-pause-and-clean-up-design.md` §1): each quest of
// `WORK_PLAN`'s answer with its sessions, and under it the questions those sessions asked of other repositories, each listed
// the same way; and why each sits. Pure, so the page draws what a pause or an abandon reaches from one rule, and every plan
// the driver could answer is a test's argument.

/** One quest of the work as the page lists it: the sessions that worked on it, and the questions they asked. */
export type WorkItem = {
  quest: WorkPlanQuest;
  /** Its sessions in the plan's order; the ask's intake is never one, since the page names it in a section of its own. */
  sessions: WorkPlanSession[];
  /** The quests its sessions published (D79's questions), each a work item of its own. */
  questions: WorkItem[];
};

/**
 * Whether a session's record is the one a quest names as its publisher. A teammate's record is keyed `origin/id` (SYNC4) and
 * the quest it published names `id`, so both are matched, as the driver's reader matches them (PAUSE1a).
 */
const publishedBy = (session: WorkPlanSession, by: string) => session.session === by || session.session.endsWith(`/${by}`);

/**
 * The work as a tree, in the plan's order (design §1): the quests the ask asked, or the one quest named, at the top; under each
 * quest the questions its sessions published, applied again to what each question adds. A question whose asker the plan does
 * not place is listed at the top, and every quest once, even where the records ask in a circle: nothing of the work is
 * hidden, since what is listed is what a pause or an abandon reaches.
 */
export function workTree(plan: WorkPlan): WorkItem[] {
  const sessionsOf = (quest: string) => plan.sessions.filter((session) => session.quest === quest && !session.intake);
  // The quest whose session published each question, where the plan holds that session and that quest.
  const askerOf = new Map<string, string>();
  for (const quest of plan.quests) {
    if (quest.joined !== 'published' || !quest.by) continue;
    const asker = plan.sessions.find((session) => publishedBy(session, quest.by!))?.quest;
    if (asker && asker !== quest.quest && plan.quests.some((other) => other.quest === asker)) askerOf.set(quest.quest, asker);
  }

  const placed = new Set<string>();
  const itemOf = (quest: WorkPlanQuest): WorkItem => {
    placed.add(quest.quest);
    const questions = plan.quests.filter((other) => askerOf.get(other.quest) === quest.quest && !placed.has(other.quest));
    // Claimed before the walk below, so a question a sibling's session also names is listed once, under the first.
    for (const question of questions) placed.add(question.quest);
    return { quest, sessions: sessionsOf(quest.quest), questions: questions.map(itemOf) };
  };

  const items = plan.quests.filter((quest) => !askerOf.has(quest.quest)).map(itemOf);
  // What a circle of askers left unreached is listed at the top, each once.
  for (const quest of plan.quests) if (!placed.has(quest.quest)) items.push(itemOf(quest));
  return items;
}

/** Why a quest of the work sits: a line of the page's own, or the driver's sentence from its last look. */
export type WhyItSits = { line: Line } | { sitting: Consideration } | null;

/** Whose pause holds a quest, said from where the page stands: this ask's own, the quest's own, or another's by its id. */
function pauseLine(pause: PausedByFact, quest: string, ask: string): Line {
  if (pause.scope === 'ask') return pause.id === ask ? { key: 'asks.work.pausedHere' } : { key: 'asks.work.pausedAsk', values: { id: pause.id } };
  return pause.id === quest ? { key: 'asks.work.pausedOwn' } : { key: 'asks.work.pausedQuest', values: { id: pause.id } };
}

/**
 * Why a quest of the work sits (design §2.1, §2.3), for an open or taken one, in order:
 * - **taken where a pause here does not reach it** (a take no session of this machine's worked), by the machine where the
 *   plan names one;
 * - **a pause**, the tick's where it has a `Paused` verdict, else the plan's, for a quest whose session runs or waits on you
 *   and so has no verdict (§2.1): this ask's said briefly, since its header says it and offers *Resume*; a quest's own said as
 *   what resuming the ask does not release (§2.4); another's by its id;
 * - **the driver's sentence** from its last look, a wait said as its question below where the page lists that question under
 *   it (QuestPage says a wait by its question's row for the same reason).
 *
 * Null for a closed quest, one the driver is starting, and one it says nothing of: its sessions' states say where it is.
 */
export function whyItSits(item: WorkItem, sitting: Consideration | null, ask: string): WhyItSits {
  const { quest } = item;
  if (quest.status !== 'Open' && quest.status !== 'Taken') return null;
  if (quest.pause === 'elsewhere') {
    if (quest.machine) return { line: { key: 'asks.work.elsewhere', values: { machine: quest.machine } } };
    return { line: { key: quest.kept === 'taken-outside' ? 'asks.work.outside' : 'asks.work.elsewhereUnnamed' } };
  }
  const pause = sitting?.verdict === 'Paused' && sitting.pausedBy ? sitting.pausedBy : quest.pausedBy ?? null;
  if (pause) return { line: pauseLine(pause, quest.quest, ask) };
  if (!sitting || sitting.verdict === 'Start') return null;
  if (sitting.verdict === 'Waiting' && item.questions.length > 0) return { line: { key: 'asks.work.waits' } };
  return { sitting };
}

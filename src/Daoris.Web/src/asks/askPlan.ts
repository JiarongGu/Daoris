/** One part of an ask's plan: where it is done, what a person sees working when it is, and the quest it became (null until published). */
export type AskPlanPart = { id: string; repository: string; quest: string | null; outcome: string; lanes: string[]; claims: string[] };

/** An order between two parts: `after` is a build dependency, `orders` a landing order only. */
export type AskPlanEdge = { part: string; on: string; kind: string; why: string };

/** What the plan's judge found; `kind` names it and `text` is the sentence the supervisor reads. */
export type AskPlanFlag = { kind: string; part: string | null; text: string };

/** One plan at its newest revision: the ask's own (`scope` null) or a worker's split of one part (`depth` 2 and below). */
export type AskPlanRevision = {
  scope: string | null; revision: number; depth: number; why: string; parts: AskPlanPart[]; edges: AskPlanEdge[]; flags: AskPlanFlag[];
};

/** The plans an ask holds, the ask's own first. */
export type AskPlan = { plans: AskPlanRevision[] };

const text = (value: unknown, fallback = '') => (typeof value === 'string' ? value : fallback);
const words = (value: unknown): string[] => (Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string') : []);
const rows = (value: unknown): Record<string, unknown>[] =>
  Array.isArray(value) ? value.filter((each): each is Record<string, unknown> => typeof each === 'object' && each !== null) : [];

/**
 * The shell's answer for an ask's plan (`ASK_PLAN`), or null where it is not one (a shell from before the route answers something
 * else, and then no section is drawn). A part without its id is passed over; an ask with no plan is an answer with none.
 */
export function askPlanOf(raw: unknown): AskPlan | null {
  if (typeof raw !== 'object' || raw === null || !Array.isArray((raw as { plans?: unknown }).plans)) return null;
  return {
    plans: rows((raw as { plans: unknown }).plans).map((plan) => ({
      scope: typeof plan.scope === 'string' ? plan.scope : null,
      revision: typeof plan.revision === 'number' ? plan.revision : 1,
      depth: typeof plan.depth === 'number' ? plan.depth : 1,
      why: text(plan.why),
      parts: rows(plan.parts).filter((part) => typeof part.id === 'string').map((part) => ({
        id: text(part.id), repository: text(part.repository), quest: typeof part.quest === 'string' ? part.quest : null,
        outcome: text(part.outcome), lanes: words(part.lanes), claims: words(part.claims),
      })),
      edges: rows(plan.edges).map((edge) => ({ part: text(edge.part), on: text(edge.on), kind: text(edge.kind), why: text(edge.why) })),
      flags: rows(plan.flags).map((flag) => ({ kind: text(flag.kind), part: typeof flag.part === 'string' ? flag.part : null, text: text(flag.text) })),
    })),
  };
}

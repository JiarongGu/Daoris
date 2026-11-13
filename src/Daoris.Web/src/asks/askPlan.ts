/** One part of an ask's plan: where it is done, what a person sees working when it is, and the quest it became (null until published). */
export type AskPlanPart = { id: string; repository: string; quest: string | null; outcome: string; lanes: string[]; claims: string[] };

/** An order between two parts: `after` is a build dependency, `orders` a landing order only. */
export type AskPlanEdge = { part: string; on: string; kind: string; why: string };

/** What the plan's judge found; `kind` names it and `text` is the sentence the supervisor reads. */
export type AskPlanFlag = { kind: string; part: string | null; text: string };

/** A release order: `deployable` after `on` (each `repository/id`), the `source` it stands on and the evidence's place, null for an order with none. */
export type AskPlanRelease = { deployable: string; on: string; why: string; source: string; evidence: string | null };

/** A question only the person can settle about the delivery, asked once under its `key`. */
export type AskPlanQuestion = { key: string; kind: string; text: string; yes: string; blocks: string; default: string };

/** One plan at its newest revision: the ask's own (`scope` null) or a worker's split of one part (`depth` 2 and below). */
export type AskPlanRevision = {
  scope: string | null; revision: number; depth: number; why: string; parts: AskPlanPart[]; edges: AskPlanEdge[]; flags: AskPlanFlag[];
  releases: AskPlanRelease[]; questions: AskPlanQuestion[];
};

/** A session on the ask's team: its `role` (`lead`), the part it serves (null for the ask's own plan) and the newest revision it wrote. */
export type AskPlanMember = { part: string | null; role: string; session: string; revision: number };

/** A choice taken about the plan at a revision, by whom, and what followed. */
export type AskPlanDecision = { scope: string | null; revision: number; decision: string; reason: string; author: string; act: string };

/** The plans an ask holds, the ask's own first, who serves it and the choices taken, oldest first. */
export type AskPlan = { plans: AskPlanRevision[]; team: AskPlanMember[]; decisions: AskPlanDecision[] };

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
  const answer = raw as { plans: unknown; team?: unknown; decisions?: unknown };
  return {
    team: rows(answer.team).filter((member) => typeof member.session === 'string').map((member) => ({
      part: typeof member.part === 'string' ? member.part : null, role: text(member.role), session: text(member.session),
      revision: typeof member.revision === 'number' ? member.revision : 1,
    })),
    decisions: rows(answer.decisions).map((decided) => ({
      scope: typeof decided.scope === 'string' ? decided.scope : null, revision: typeof decided.revision === 'number' ? decided.revision : 1,
      decision: text(decided.decision), reason: text(decided.reason), author: text(decided.author), act: text(decided.act),
    })),
    plans: rows(answer.plans).map((plan) => ({
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
      releases: rows(plan.releases).map((release) => ({
        deployable: text(release.deployable), on: text(release.on), why: text(release.why), source: text(release.source),
        evidence: typeof release.evidence === 'string' ? release.evidence : null,
      })),
      questions: rows(plan.questions).map((question) => ({
        key: text(question.key), kind: text(question.kind), text: text(question.text), yes: text(question.yes),
        blocks: text(question.blocks), default: text(question.default),
      })),
    })),
  };
}

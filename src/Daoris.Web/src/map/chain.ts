import type { Quest, QuestStep, Session } from '../api';

/**
 * One step of how a piece of work was carried (MAP1): the ask it began as, a quest and every
 * session that ran it, a parent this page cannot see, or a step its close will still publish.
 */
export type ChainStep =
  | { kind: 'ask'; id: string }
  | { kind: 'unseen'; id: string }
  | { kind: 'quest'; quest: Quest; sessions: Session[]; current: boolean }
  | { kind: 'pending'; step: QuestStep };

/** The sender every quest an ask becomes is published by (the service's `AskDesk.SenderOf`). */
const ASK = /^ask #(\S+)$/;

/**
 * The chain one quest belongs to, in order (MAP1, `docs/2026-09-23-map-design.md` §2).
 *
 * @remarks
 * A chain is linear by construction (D65 §4): closing a quest done publishes its first step with
 * `parent` set to it and the rest of the list carried on. So the walk is up through `parent`, down
 * through the one quest that names the last as its parent, and then whatever the last published
 * quest will still publish — taken from IT, because the quest before it still lists the step that
 * has already become a quest.
 *
 * Records are data other machines wrote, so a parent loop stops the walk rather than hanging the
 * page, and a parent this page does not hold is named rather than dropped.
 */
export function buildChain(questId: string, quests: Quest[], sessions: Session[]): ChainStep[] {
  const byId = new Map(quests.map((quest) => [quest.id, quest]));
  const current = byId.get(questId);
  if (!current) return [];

  const seen = new Set<string>([current.id]);
  const before: ChainStep[] = [];
  let first = current;
  while (first.parent) {
    const parent = byId.get(first.parent);
    if (!parent) {
      before.unshift({ kind: 'unseen', id: first.parent });
      break;
    }
    if (seen.has(parent.id)) break;
    seen.add(parent.id);
    before.unshift(questStep(parent, sessions, false));
    first = parent;
  }

  const asked = ASK.exec(first.from);
  if (asked && !first.parent) before.unshift({ kind: 'ask', id: asked[1]! });

  const after: ChainStep[] = [];
  let last = current;
  for (;;) {
    const child = quests
      .filter((quest) => quest.parent === last.id && !seen.has(quest.id))
      .sort((a, b) => a.filed.localeCompare(b.filed))[0];
    if (!child) break;
    seen.add(child.id);
    after.push(questStep(child, sessions, false));
    last = child;
  }

  const pending: ChainStep[] = (last.then ?? []).map((step) => ({ kind: 'pending', step }));

  return [...before, questStep(current, sessions, true), ...after, ...pending];
}

function questStep(quest: Quest, sessions: Session[], current: boolean): ChainStep {
  return {
    kind: 'quest',
    quest,
    current,
    sessions: sessions
      .filter((session) => session.quest === quest.id)
      .sort((a, b) => a.created.localeCompare(b.created)),
  };
}

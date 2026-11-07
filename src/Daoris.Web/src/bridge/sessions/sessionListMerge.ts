import { type Session, SESSION_ACTIVE } from '../../data/api';

/** The service's order of its records (`Sessions.ListAsync`): the open ones first, then by when each began. */
export const inServiceOrder = (a: Session, b: Session) => {
  const open = Number(!SESSION_ACTIVE.has(a.state)) - Number(!SESSION_ACTIVE.has(b.state));
  if (open !== 0) return open;
  const began = (Date.parse(a.created) || 0) - (Date.parse(b.created) || 0);
  return began !== 0 ? began : a.id < b.id ? -1 : a.id > b.id ? 1 : 0;
};

const isRecord = (value: unknown): value is Session =>
  typeof value === 'object' && value !== null && typeof (value as { id?: unknown }).id === 'string';

type Part = Record<string, unknown>;
type Held = { parts: Map<string, Part>; list: Session[] };

const partOf = (value: unknown): Part | undefined =>
  typeof value === 'object' && value !== null && !Array.isArray(value) ? (value as Part) : undefined;

/** Past this share of the list moved, the whole is sorted again: a merge is a win only for a change. */
const WHOLE_AT = 0.5;

const sorted = (parts: ReadonlyMap<string, Part>): Session[] =>
  [...parts.values()].flatMap((part) => Object.values(part).filter(isRecord)).sort(inServiceOrder);

/**
 * The widest session list composed from the workspaces' partitions (`mergeKeyed` writes a new partition object for a change and
 * keeps every unmoved record's own object), kept from one call to the next: a call finds the records whose object is not the one
 * it composed last time and merges those into the list it holds, in the service's order, rather than sorting the whole list again at
 * every change. The list is the same as sorting the whole, which the page's test holds.
 */
export function sessionsComposer(): (partitions: ReadonlyMap<string, unknown>, judged: boolean) => Session[] | undefined {
  let held: Held | undefined;

  return (partitions, judged) => {
    if (!judged) {
      held = undefined;
      return undefined;
    }

    const parts = new Map<string, Part>();
    for (const [name, data] of partitions) {
      const part = partOf(data);
      if (part) parts.set(name, part);
    }

    const list = (held && merged(held, parts)) ?? sorted(parts);
    held = { parts, list };
    return list;
  };
}

// The held list with the records that moved laid in; null where too many moved or a record sits in two partitions.
function merged(held: Held, parts: Map<string, Part>): Session[] | null {
  const gone = new Set<string>();
  const changed = new Map<string, Session>();
  for (const [name, part] of parts) {
    const before = held.parts.get(name);
    if (before === part) continue;
    for (const [id, record] of Object.entries(part)) {
      if (before?.[id] === record) continue;
      if (!isRecord(record) || changed.has(id)) return null;
      for (const [other, beside] of parts) if (other !== name && id in beside) return null;
      changed.set(id, record);
    }
  }

  for (const [name, before] of held.parts) {
    const part = parts.get(name);
    if (part === before) continue;
    for (const id of Object.keys(before)) {
      if (part && id in part) continue;
      // A record that left one partition for another is in changed; one still held by a third is a duplicate the whole sort keeps.
      if (!changed.has(id) && [...parts.values()].some((each) => id in each)) return null;
      gone.add(id);
    }
  }

  if (changed.size + gone.size > held.list.length * WHOLE_AT) return null;
  for (const id of changed.keys()) gone.add(id);
  const kept = held.list.filter((each) => !gone.has(each.id));
  const list: Session[] = [];
  let at = 0;
  for (const record of [...changed.values()].sort(inServiceOrder)) {
    while (at < kept.length && inServiceOrder(kept[at], record) < 0) list.push(kept[at++]);
    list.push(record);
  }

  while (at < kept.length) list.push(kept[at++]);
  return list;
}

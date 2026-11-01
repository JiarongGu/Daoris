import type { TFunction } from 'i18next';
import { type ReactNode, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ActorRow, RoleCatalogue, RoleChangePress, RoleChoice, RoleRow, RoleUse, StaffingFound, SuggestedRole } from '../../data/wiring';
import { Button, Card, CardHeader, Icon, Inline, Prose, SelectField, SettingList, SettingRow } from '../../design/ui';
import { authoredEntries, authoredText, composeAuthored } from '../../locales/authored';
import { type Answered, InlineConfirm, Refused } from '../../work/InlineConfirm';
import type { SettingsChoices } from '../tools';
import { type Draft, type Editing, RoleEditor } from './RoleEditor';
import { RowMore } from './RowMore';
import { SuggestedChoicesCard } from './SuggestedChoicesCard';

/** An entry as `SET_ACTOR` takes it: an agent, and its model and effort in the tool's own words, absent for account default. */
export type ActorChoice = { agent: string; model?: string | null; effort?: string | null };

/** How far a role's place reaches: a wider place is what a narrower one falls back to. */
const REACH: Record<string, number> = { machine: 0, workspace: 1, repository: 2 };

/** Which of All roles' rows are shown, by where the role is placed. */
export type RoleScope = 'all' | 'machine' | 'workspace' | 'repository';
const SCOPES: readonly RoleScope[] = ['all', 'machine', 'workspace', 'repository'];

/** An id made from a name, unique among the ids the catalogue holds; a name with no letters falls back to its work. */
export function roleIdFor(name: string, work: string, taken: readonly string[]): string {
  const base = name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || work;
  let id = base;
  for (let n = 2; taken.includes(id); n++) id = `${base}-${n}`;
  return id;
}

/** The roles whose staffing editor over `SET_ACTOR` this build offers (MODELROLE1c, 1d; the rest come with MODELROLE1e). */
const EDITABLE = new Set(['build', 'intake', 'ask-daoris', 'chat']);

/** The roles whose running conversation goes on with the next choice at its agent's limit (D205 §3.5(e)). */
const CONVERSING = new Set(['ask-daoris', 'chat']);

/** The work a role may be off for, and an actor's own words for off (D205 §2.1: they keep their cards' words on the folded rows). */
const OFF_WORKS = new Set(['intake', 'ask-daoris']);
const OFF_WORDS: Record<string, string> = { intake: 'settings.ai.intake.off', 'ask-daoris': 'settings.ai.helper.off' };

export type { Editing } from './RoleEditor';

/** A new role's editor, its place filled where one is given (*Add a role here*); its one empty choice starts on the first agent. */
export function newRoleEditing(
  works: readonly string[], agents: readonly { value: string }[], place: { kind: 'workspace' | 'repository'; id: string } | null = null, key = 0,
): Editing {
  return {
    id: '', mode: 'role', isNew: true, revision: null, name: '', nameIn: [], takes: [], work: works[0] ?? 'build',
    place: place?.kind ?? 'machine', placeId: place?.id ?? '', drafts: [{ key, agent: agents[0]?.value ?? '', model: '', effort: '' }],
  };
}

/** One row: the catalogue's role where there is one, and the staffing `ACTORS` answers for the same id. */
export type Line = { id: string; row: RoleRow | null; actor: ActorRow | null };

/** A row whose record is not this home's to change keeps the order editor alone. */
const staffingOnlyLine = (line: Line, kept: boolean) => !line.row || (!kept && line.row.seeded);

/** Whether a role's editor is offered: a catalogue role this home keeps, or a staffing's order where its editor is built. */
export const mayEditLine = (line: Line, kept: boolean): boolean => (line.row
  ? (line.row.state === 'seed' || line.row.state === 'set') && (staffingOnlyLine(line, kept) ? !!line.actor && EDITABLE.has(line.id) : kept)
  : !!line.actor && EDITABLE.has(line.id));

/** A role's order as drafts: the staffing's where it has one, else the record's, else one empty choice on the first agent. */
function draftsOf(line: Line, agents: readonly { value: string }[], key: () => number): Draft[] {
  const draft = (entry?: RoleChoice): Draft => ({ key: key(), agent: entry?.agent ?? agents[0]?.value ?? '', model: entry?.model ?? '', effort: entry?.effort ?? '' });
  const own = line.actor && line.actor.choices.length > 0 ? line.actor.choices : line.row?.choices ?? [];
  return own.length > 0 ? own.map(draft) : [draft()];
}

/** A name as the editor holds it: the one text as written (a map's `en`), and the other languages beside it. */
export function nameFields(authored: RoleRow['name'], shown: string): Pick<Editing, 'name' | 'nameIn'> {
  if (authored === null || authored === undefined) return { name: shown, nameIn: [] };
  if (typeof authored === 'string') return { name: authored, nameIn: [] };
  const entries = authoredEntries(authored);
  return { name: entries.find(([tag]) => tag.toLowerCase() === 'en')?.[1] ?? '', nameIn: entries.filter(([tag]) => tag.toLowerCase() !== 'en') };
}

/** The editor an existing role opens with: its record and its order, in the catalogue's mode or the order editor's alone. */
export function editingOfLine(
  line: Line, { kept, works, agents, name, key }: { kept: boolean; works: readonly string[]; agents: readonly { value: string }[]; name: string; key: () => number },
): Editing {
  const row = line.row;
  return {
    id: line.id, mode: staffingOnlyLine(line, kept) ? 'staffing' : 'role', isNew: false, revision: row?.revision ?? null,
    ...nameFields(row?.name, name), takes: row?.takes ?? [],
    work: row?.takes[0] ?? works[0] ?? 'build', place: row?.scopeKind ?? 'machine', placeId: row?.scopeId ?? '', drafts: draftsOf(line, agents, key),
  };
}

/** An entry's words: agent, model and effort, each in the tool's own words, account default where it names none. */
export function entryText(t: TFunction, entry: ActorChoice): string {
  return t('settings.ai.actors.entry', {
    agent: entry.agent,
    model: entry.model || t('settings.ai.actors.accountDefault'),
    effort: entry.effort || t('settings.ai.actors.accountDefault'),
  });
}

/**
 * A role's staffing in its folded words (D205 §7): its first choice and how many more, *as before* where it reads today's settings,
 * *Off*, a fenced role's own words, or the driver's sentence where its stored entry does not read.
 */
export function actorSummary(t: TFunction, row: ActorRow): string {
  if (row.state === 'unreadable') return t('settings.ai.actors.unreadable', { error: row.error ?? '' });
  if (row.state === 'off') return t(OFF_WORDS[row.id] ?? 'settings.ai.actors.off');
  const before = row.state === 'before' ? ` · ${t('settings.ai.actors.asBefore')}` : '';
  if (row.choices.length === 0) {
    if (row.id === 'opinion') return t('settings.ai.actors.fencedRule', { state: t('settings.ai.actors.asBefore') });
    if (row.id === 'partner') return t('settings.ai.actors.fencedAsk', { state: t('settings.ai.actors.asBefore') });
    return t('settings.ai.actors.asBefore');
  }
  const first = entryText(t, row.choices[0]!);
  const more = row.choices.length > 1 ? ` ${t('settings.ai.actors.more', { count: row.choices.length - 1 })}` : '';
  return row.id === 'brainstorm' ? t('settings.ai.actors.allAtOnce', { order: `${first}${more}` }) + before : `${first}${more}${before}`;
}

/** A role's name as authored, in the page's language where it has one; a seeded role with no record is its actor's name. */
export function roleName(t: TFunction, language: string, row: Pick<RoleRow, 'id' | 'seeded' | 'name'>): string {
  return authoredText(row.name, language) || (row.seeded ? t(`settings.ai.actors.name.${row.id}`) : row.id);
}

/** Where a role works, in the page's words. */
export function placeText(t: TFunction, kind?: string | null, id?: string | null): string {
  return kind === 'workspace' || kind === 'repository' ? t(`settings.ai.roles.place.${kind}`, { id: id ?? '' }) : t('settings.ai.roles.place.machine');
}

/** A role's folded line: the work it takes, where, and its first choice and how many more (the staffing's, where it is read). */
export function roleSummary(t: TFunction, row: RoleRow, actor?: ActorRow | null): string {
  if (row.state === 'removed') return t('settings.ai.roles.removed');
  if (row.state === 'unreadable') return t('settings.ai.roles.unreadable', { error: row.error ?? '' });
  const works = row.takes.map((work) => t(`settings.ai.roles.work.${work}`)).join(t('settings.ai.actors.and'));
  const choices = row.choices ?? [];
  const order = actor
    ? ` · ${actorSummary(t, actor)}`
    : choices.length === 0
      ? row.choices ? ` · ${t('settings.ai.actors.off')}` : ''
      : ` · ${entryText(t, choices[0]!)}${choices.length > 1 ? ` ${t('settings.ai.actors.more', { count: choices.length - 1 })}` : ''}`;
  return `${works} · ${placeText(t, row.scopeKind, row.scopeId)}${order}`;
}

/**
 * A role's last days as one quiet line (MODELROLE1f): starts, where they landed, and how many fell to a later choice with the walk's codes.
 * A log that holds fewer than seven days says its span; names and counts only, never a price.
 */
export function useLine(t: TFunction, use: RoleUse, days: number): string {
  const choiceName = (choice: string) => t(
    choice === 'before' ? 'settings.ai.roles.use.choiceBefore' : choice === 'rule-default' ? 'settings.ai.roles.use.choiceRuleDefault' : 'settings.ai.roles.use.choice',
    { choice },
  );
  const span = t(days >= 7 ? 'settings.ai.roles.use.week' : 'settings.ai.roles.use.kept', { count: Math.max(1, days) });
  const parts = [
    t('settings.ai.roles.use.starts', { count: use.starts }),
    use.choices.map((each) => `${choiceName(each.choice)} ×${each.starts}`).join(', '),
  ].filter((part) => part !== '');
  if (use.fell > 0) {
    const why = use.why.map((each) => `${t(`settings.ai.roles.use.why.${each.code}`, { defaultValue: each.code })} ×${each.starts}`).join(', ');
    parts.push(`${t('settings.ai.roles.use.fell', { count: use.fell })}${why ? ` (${why})` : ''}`);
  }

  return `${span}: ${parts.join(' · ')}`;
}

/** A suggestion's press: its record and choices set, as `SET_ROLE` takes them, against the revision 0 of an id the catalogue lacks. */
function suggestionPress(role: SuggestedRole): RoleChangePress {
  return {
    change: 'set', id: role.id, revision: 0,
    role: { name: role.name, takes: role.takes, scope: { kind: role.scopeKind, id: null } },
    choices: role.choices.map((choice) => ({ agent: choice.agent, model: choice.model ?? null, effort: choice.effort ?? null })),
  };
}

/**
 * **Roles** (FOUNDATION1y-i, MODELROLE1c, COMPANYVIEW1a): every role that takes Daoris's work, a row each, the seed's and the person's:
 * its name as authored, the work it takes, where, and its choices folded to one line. A row's editor holds its names, work, place and one
 * to four choices of agent, model and effort with *Move up*, *Move down* and *Remove*; *Save* and, where the work allows it (intake and
 * Ask Daoris), *Off* go out over `SET_ROLE`, and *Clear order* (a seeded role's order back to as before) over `SET_ACTOR` alone, which an
 * added role never has. *Add a role*, *Remove* and *Back to the default* (a seeded role's names, work and place) are the catalogue's.
 * A home whose `driver.json` is the file's own keeps each seeded row's order editor over `SET_ACTOR` and holds the rest; a shell that
 * answers the staffing and no catalogue keeps one order editor per role. Under the rows, the roles Daoris suggests and each change of
 * the staffing another program made, as the driver kept it, with *Take it* and *Dismiss*. The page judges nothing: every press goes out
 * as its route takes it, and a refusal is the driver's sentence under its press.
 *
 * A molecule: every state is reached by its props, and every press goes out.
 */
export function RolesCard({
  catalogue, read = catalogue ? 'ok' : 'notOffered', staffing = [], agents, about = {}, busy = false, onSet, onSetAll, onSetActor, found = [], onAnswerFound,
  editor: held, onEditor, only, editorOnly = false, onDiagram, offers = {}, onOpenPlace, scope: heldScope, onScope, consequence,
}: {
  /** The one sentence of consequence before *Save*, where the page that owns the editor knows more than the editor does. */
  consequence?: string;
  /** The open editor alone, no card, rows or adding: a place's page opens it from its own rows (AGENTSPLACE1g1). */
  editorOnly?: boolean;
  /** What each agent offers for a model and an effort, by agent; absent or null, the field is a typed name. */
  offers?: Readonly<Record<string, SettingsChoices | null | undefined>>;
  /** *Open at \<place\>* on a role placed elsewhere: the place's own page, where it is edited. */
  onOpenPlace?: (kind: 'workspace' | 'repository', id: string) => void;
  /** Which roles All roles shows by where they are placed, held by the page that owns the section; with none given the card holds its own. */
  scope?: RoleScope;
  onScope?: (scope: RoleScope) => void;
  /** One role's row alone, as the diagram's detail draws it (COMPANYVIEW1f): no card, no adding, no suggestion. */
  only?: string;
  /** A row's ⋯ *On the diagram*, its role chosen there. */
  onDiagram?: (role: string) => void;
  /** The open editor, where the place that owns the section holds it (D168 point 3); with none given the card holds its own. */
  editor?: Editing | null;
  onEditor?: (next: Editing | null) => void;
  /** The catalogue as `ROLES` answers it; null where the shell offers none or the read failed. */
  catalogue: RoleCatalogue | null;
  /** Why there is no catalogue: not offered by this shell, or a read that failed; `loading` says nothing. */
  read?: 'ok' | 'loading' | 'notOffered' | 'failed';
  /** Each role's order as `ACTORS` answers it. */
  staffing?: readonly ActorRow[];
  /** The agents this machine has, to name in a choice. */
  agents: readonly { value: string; label: string }[];
  /** What a row says beside its order, by role: its ⓘ, and the lines under it (Intake's and Ask Daoris's). */
  about?: Partial<Record<string, { why?: string; below?: ReactNode }>>;
  busy?: boolean;
  /** A role's change over `SET_ROLE`; the answer says whether it landed or the driver's sentence. */
  onSet: (press: RoleChangePress, answered: Answered) => void;
  /** Several roles' changes as one over `SET_ROLES`, judged whole by the driver; where none is given there is no suggested-choices card. */
  onSetAll?: (presses: RoleChangePress[], answered: Answered) => void;
  /** A role's whole order over `SET_ACTOR`, or null back to as before. */
  onSetActor: (actor: string, choices: ActorChoice[] | null, answered: Answered) => void;
  /** The changes the driver kept for the person, oldest first. */
  found?: readonly StaffingFound[];
  /** A found change taken or dismissed; the answer says whether it landed or the driver's sentence. */
  onAnswerFound?: (found: number, answer: 'take' | 'dismiss', answered: Answered) => void;
}) {
  const { t, i18n } = useTranslation();
  const next = useRef(0);
  const [own, setOwn] = useState<Editing | null>(null);
  const controlled = onEditor !== undefined;
  const editing = controlled ? held ?? null : own;
  const latest = useRef(editing);
  latest.current = editing;
  const setEditing = (change: Editing | null | ((now: Editing | null) => Editing | null)) => {
    const next = typeof change === 'function' ? change(latest.current) : change;
    latest.current = next;
    if (controlled) onEditor(next);
    else setOwn(next);
  };
  const [ownScope, setOwnScope] = useState<RoleScope>('all');
  const scope = heldScope ?? ownScope;
  const setScope = (next: RoleScope) => (onScope ?? setOwnScope)(next);
  const [removing, setRemoving] = useState<string | null>(null);
  const [refused, setRefused] = useState<{ at: string; sentence: string } | null>(null);
  const [foundRefused, setFoundRefused] = useState<{ id: number; sentence: string } | null>(null);
  const name = (row: Pick<RoleRow, 'id' | 'seeded' | 'name'>) => roleName(t, i18n.language, row);
  const kept = catalogue?.kept === true;
  const all: Line[] = catalogue
    ? catalogue.roles.map((row) => ({ id: row.id, row, actor: staffing.find((each) => each.id === row.id) ?? null }))
    : staffing.map((actor) => ({ id: actor.id, row: null, actor }));
  const lines = only === undefined ? all : all.filter((line) => line.id === only);
  const lineOf = (id: string) => all.find((line) => line.id === id);
  const nameOfLine = (line: Line) => name(line.row ?? { id: line.id, seeded: true });
  const press = (at: string, change: RoleChangePress, done?: () => void) => {
    setRefused(null);
    onSet(change, { done: () => done?.(), refused: (sentence) => setRefused({ at, sentence }) });
  };
  const pressActor = (at: string, actor: string, choices: ActorChoice[] | null) => {
    setRefused(null);
    onSetActor(actor, choices, { done: () => setEditing(null), refused: (sentence) => setRefused({ at, sentence }) });
  };
  const open = (line: Line | null) => {
    setRefused(null);
    if (!line) {
      setEditing(newRoleEditing(catalogue?.works ?? [], agents, null, next.current++));
      return;
    }
    setEditing(editingOfLine(line, { kept, works: catalogue?.works ?? [], agents, name: nameOfLine(line), key: () => next.current++ }));
  };
  const entries = (now: Editing): ActorChoice[] =>
    now.drafts.map((draft) => ({ agent: draft.agent.trim(), model: draft.model.trim() || null, effort: draft.effort.trim() || null }));
  /** *Save* (the editor's choices) or *Off* (`[]`): over `SET_ROLE` from the catalogue's editor, over `SET_ACTOR` from the order editor. */
  const save = (now: Editing, off = false) => {
    if (now.mode === 'staffing') {
      pressActor(now.id, now.id, off ? [] : entries(now));
      return;
    }
    const id = now.id.trim() || roleIdFor(now.name.trim(), now.work, catalogue?.roles.map((row) => row.id) ?? []);
    press(now.isNew ? '' : now.id, {
      change: 'set', id, revision: now.revision,
      role: {
        name: composeAuthored(now.name, now.nameIn) ?? '',
        takes: now.takes[0] === now.work ? now.takes : [now.work],
        scope: { kind: now.place, id: now.place === 'machine' ? null : now.placeId.trim() || null },
      },
      choices: off ? [] : now.drafts.length > 0 ? entries(now) : now.isNew ? [] : null,
    }, () => setEditing(null));
  };
  const answer = (id: number, said: 'take' | 'dismiss') => {
    setFoundRefused(null);
    onAnswerFound?.(id, said, { done: () => {}, refused: (sentence) => setFoundRefused({ id, sentence }) });
  };
  const works = (catalogue?.works ?? []).map((work) => ({ value: work, label: t(`settings.ai.roles.work.${work}`) }));
  /** The catalogue's own presses, held where this home's file is its own. */
  const locked = busy || !kept;
  /** The role a removed one's work falls back to: the nearest wider place that has one taking the same work. */
  const fallbackOf = (row: RoleRow) => {
    const reach = REACH[row.scopeKind ?? 'machine'] ?? 0;
    return (catalogue?.roles ?? [])
      .filter((each) => each.id !== row.id && (each.state === 'seed' || each.state === 'set') && each.takes[0] === row.takes[0] && (REACH[each.scopeKind ?? 'machine'] ?? 0) < reach)
      .sort((a, b) => (REACH[b.scopeKind ?? 'machine'] ?? 0) - (REACH[a.scopeKind ?? 'machine'] ?? 0))[0] ?? null;
  };

  const editor = (now: Editing) => {
    const line = lineOf(now.id);
    const named = now.isNew ? t('settings.ai.roles.newTitle') : line ? nameOfLine(line) : now.id;
    const title = now.mode === 'staffing' ? t('settings.ai.actors.editorTitle', { actor: named }) : named;
    const actor = line?.actor;
    const seeded = line?.row ? line.row.seeded : true;
    // *Off* where the work allows it, for a seeded or an added role; not where it is already off.
    const mayOff = now.mode === 'staffing' ? now.id in OFF_WORDS : OFF_WORKS.has(now.work);
    const isOff = actor ? actor.state === 'off' : line?.row?.choices?.length === 0;
    const mayClear = !now.isNew && seeded && !!actor && actor.state !== 'before';
    const at = now.isNew ? '' : now.id;
    return (
      <RoleEditor
        editing={now} onEditing={(change) => setEditing(typeof change === 'function' ? (was) => was && change(was) : change)}
        title={title} agents={agents} offers={offers} works={works} busy={busy} held={!kept}
        placed={t('settings.ai.roles.fixedPlace', { place: placeText(t, now.place, now.placeId) })}
        consequence={consequence ?? t('settings.ai.roles.editor.then')} conversing={CONVERSING.has(now.id)}
        mayOff={mayOff} isOff={!!isOff} mayClear={mayClear} refused={refused && refused.at === at ? refused.sentence : null}
        onSave={(off) => save(now, off)} onClear={() => pressActor(at, now.id, null)} onCancel={() => { setRefused(null); setEditing(null); }}
      />
    );
  };

  const said = (
    <>
      {catalogue && !kept && <p className="m-0 mb-2 text-small text-ink-soft">{t('settings.ai.roles.unkept')}</p>}
      {read === 'notOffered' && <p className="m-0 mb-2 text-small text-ink-soft">{t('settings.ai.roles.notOffered')}</p>}
      {read === 'failed' && <p role="alert" className="m-0 mb-2 text-small text-ink-soft">{t('settings.ai.roles.readFailed')}</p>}
    </>
  );
  /** A role placed at a workspace or a repository, which All roles shows and its own place's page edits. */
  const placedElsewhere = (line: Line) => line.row?.scopeKind === 'workspace' || line.row?.scopeKind === 'repository';
  const rowsOf = (list: readonly Line[]) => (
    <SettingList>
        {list.map((line) => {
          const { row, actor } = line;
          const named = nameOfLine(line);
          const used = catalogue?.use?.roles.find((each) => each.role === line.id);
          const open_ = editing?.id === line.id && !editing.isNew;
          // One editor per role: in the catalogue a role placed elsewhere is read, and opened at its place.
          const readOnly = only === undefined && placedElsewhere(line);
          const mayEdit = !readOnly && mayEditLine(line, kept);
          const asking = removing === line.id && row;
          const placeId_ = row?.scopeId ?? '';
          const fallback = row ? fallbackOf(row) : null;
          const work = t(`settings.ai.roles.work.${row?.takes[0] ?? line.id}`, { defaultValue: row?.takes[0] ?? line.id });
          return (
            <SettingRow
              key={line.id}
              label={named}
              why={about[line.id]?.why}
              control={!open_ && !asking && (
                <>
                  <span className="text-body text-ink wrap-anywhere">{row ? roleSummary(t, row, actor) : actorSummary(t, actor!)}</span>
                  {mayEdit && (
                    <Button variant="ghost" disabled={busy || editing !== null} aria-label={t('settings.ai.roles.editOf', { role: named })} onClick={() => open(line)}>
                      {t('settings.ai.actors.edit')}
                    </Button>
                  )}
                  {readOnly && onOpenPlace && row && (
                    <Button
                      variant="ghost"
                      aria-label={t('settings.ai.roles.openAtOf', { role: named, place: placeId_ })}
                      onClick={() => onOpenPlace(row.scopeKind as 'workspace' | 'repository', placeId_)}
                    >
                      {t('settings.ai.roles.openAt', { place: placeId_ })}
                    </Button>
                  )}
                  {!readOnly && row && row.state !== 'removed' && (
                    <Button
                      variant="ghost"
                      disabled={locked || editing !== null}
                      aria-label={t('settings.ai.roles.removeOf', { role: named })}
                      onClick={() => { setRefused(null); setRemoving(line.id); }}
                    >
                      {t('settings.ai.roles.remove')}
                    </Button>
                  )}
                  {!readOnly && row && row.seeded && row.state !== 'seed' && (
                    <Button
                      variant="ghost"
                      disabled={locked || editing !== null}
                      aria-label={t('settings.ai.roles.defaultOf', { role: named })}
                      onClick={() => press(line.id, { change: 'default', id: line.id, revision: row.revision })}
                    >
                      {t('settings.ai.roles.default')}
                    </Button>
                  )}
                  {onDiagram && row && row.state !== 'removed' && <RowMore name={named} onDiagram={() => onDiagram(line.id)} />}
                </>
              )}
            >
              {asking && row && (
                <InlineConfirm
                  label={t('settings.ai.roles.removeAsk', { role: named })}
                  says={fallback
                    ? t('settings.ai.roles.removeFallback', {
                      role: named, work, fallback: name(fallback), place: placeText(t, fallback.scopeKind, fallback.scopeId),
                    })
                    : t('settings.ai.roles.removeNone', { role: named, work })}
                  meanIt={t('settings.ai.roles.removeMove')}
                  busy={busy}
                  onConfirm={(answered) => onSet({ change: 'remove', id: line.id, revision: row.revision }, answered)}
                  onClose={() => setRemoving(null)}
                />
              )}
              {!open_ && used && <p className="m-0 text-small text-ink-faint wrap-anywhere">{useLine(t, used, catalogue!.use!.days)}</p>}
              {!open_ && about[line.id]?.below}
              {open_ && editing && editor(editing)}
              {!open_ && refused?.at === line.id && <Refused sentence={refused.sentence} />}
            </SettingRow>
          );
        })}
    </SettingList>
  );
  if (editorOnly) return editing ? <div className="min-w-0">{editor(editing)}</div> : null;
  const rows = rowsOf(lines);
  if (only !== undefined) return <div className="min-w-0">{said}{rows}</div>;

  const shownLines = scope === 'all' ? lines : lines.filter((line) => (line.row?.scopeKind ?? 'machine') === scope);
  const machineWide = shownLines.filter((line) => !placedElsewhere(line));
  const elsewhere = shownLines.filter(placedElsewhere);
  const group = (title: string, list: readonly Line[]) => list.length > 0 && (
    <section aria-label={title} className="mt-2">
      <p className="m-0 mb-1 text-meta font-medium text-ink-faint">{title}</p>
      {rowsOf(list)}
    </section>
  );

  return (
    <Card>
      <CardHeader title={t('settings.ai.roles.all.title')} />
      <Prose className="mb-3 mt-0 text-small text-ink-soft">{t('settings.ai.roles.body')}</Prose>
      {catalogue && onSetAll && (
        <SuggestedChoicesCard
          catalogue={catalogue} staffing={staffing} agents={agents.map((agent) => agent.value)} busy={busy}
          name={(row) => name(row)} entry={(choice) => entryText(t, choice)} onSetAll={(presses, done) => { setRefused(null); onSetAll(presses, done); }}
        />
      )}
      {said}
      {catalogue && catalogue.roles.length === 0 && <p className="m-0 mb-2 text-small text-ink-soft">{t('settings.ai.roles.none')}</p>}
      {catalogue && (
        <div className="mb-1 flex flex-wrap items-center gap-2">
          <span className="text-small font-medium text-ink-soft">{t('settings.ai.roles.all.filter')}</span>
          <SelectField
            value={scope}
            onChange={(value) => setScope(value as RoleScope)}
            ariaLabel={t('settings.ai.roles.all.filter')}
            options={SCOPES.map((each) => ({ value: each, label: each === 'all' ? t('settings.ai.roles.all.every') : t(`settings.ai.roles.where.${each}`) }))}
          />
        </div>
      )}
      {catalogue ? (
        <>
          {group(t('settings.ai.roles.all.machineWide'), machineWide)}
          {group(t('settings.ai.roles.all.elsewhere'), elsewhere)}
          {shownLines.length === 0 && catalogue.roles.length > 0 && <p className="m-0 text-small text-ink-soft">{t('settings.ai.roles.all.nothing')}</p>}
        </>
      ) : rows}
      {catalogue && (editing?.isNew ? <div className="mt-3">{editor(editing)}</div> : (
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <Button variant="ghost" disabled={locked || editing !== null} onClick={() => open(null)}>
            <Icon name="plus" size={12} />
            {t('settings.ai.roles.add')}
          </Button>
          {kept && catalogue.roles.some((each) => each.seeded && each.state !== 'seed') && (
            <Button
              variant="ghost"
              disabled={locked || editing !== null}
              onClick={() => press('*', { change: 'default-all', id: '*' })}
            >
              {t('settings.ai.roles.defaultAll')}
            </Button>
          )}
        </div>
      ))}
      {catalogue?.suggested.map((role) => {
        const named = name({ id: role.id, seeded: false, name: role.name });
        const taken = role.takes.map((work) => t(`settings.ai.roles.work.${work}`)).join(t('settings.ai.actors.and'));
        return (
          <section key={role.id} aria-label={t('settings.ai.roles.suggested.title', { role: named })} className="mt-3 rounded-control border border-line p-2">
            <p className="m-0 text-body font-medium text-ink">{t('settings.ai.roles.suggested.title', { role: named })}</p>
            <p className="m-0 text-small text-ink-soft wrap-anywhere">
              {t('settings.ai.roles.suggested.body', { role: named, work: taken, choice: role.choices[0] ? entryText(t, role.choices[0]) : '' })}
            </p>
            <div className="mt-2 flex gap-2">
              <Button disabled={locked || editing !== null} onClick={() => press(role.id, suggestionPress(role))}>
                {t('settings.ai.roles.suggested.add', { role: named })}
              </Button>
            </div>
            {refused?.at === role.id && <Refused sentence={refused.sentence} />}
          </section>
        );
      })}
      {found.length > 0 && (
        <section className="mt-3">
          <p className="m-0 text-body font-medium text-ink">{t('settings.ai.actors.found.title')}</p>
          <Prose className="mb-2 mt-0 text-small text-ink-soft">{t('settings.ai.actors.found.body')}</Prose>
          <ul aria-label={t('settings.ai.actors.found.title')} className="m-0 flex list-none flex-col gap-2 p-0">
            {found.map((each) => (
              <li key={each.id} className="rounded-control border border-line p-2">
                {each.changes.map((moved) => (
                  <p key={moved.role} className="m-0 text-small text-ink wrap-anywhere">
                    {t(moved.applied ? 'settings.ai.actors.found.changeApplied' : 'settings.ai.actors.found.change', {
                      role: name(lineOf(moved.role)?.row ?? { id: moved.role, seeded: true }), was: actorSummary(t, moved.was), now: actorSummary(t, moved.now),
                    })}
                  </p>
                ))}
                <div className="mt-2 flex gap-2">
                  <Button disabled={busy} onClick={() => answer(each.id, 'take')}>{t('settings.ai.actors.found.take')}</Button>
                  <Button variant="ghost" disabled={busy} onClick={() => answer(each.id, 'dismiss')}>{t('settings.ai.actors.found.dismiss')}</Button>
                </div>
                {foundRefused?.id === each.id && <Refused sentence={foundRefused.sentence} />}
              </li>
            ))}
          </ul>
        </section>
      )}
      <p className="m-0 mt-3 text-small text-ink-faint"><Inline text={t('settings.ai.roles.footnote')} /></p>
    </Card>
  );
}

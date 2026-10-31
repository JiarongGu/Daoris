import type { TFunction } from 'i18next';
import { type ReactNode, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ActorRow, RoleCatalogue, RoleChangePress, RoleChoice, RoleRow, StaffingFound, SuggestedRole } from '../../data/wiring';
import { Button, Card, CardHeader, FormField, FormGrid, FormRow, Icon, Inline, Prose, SelectField, SettingList, SettingRow } from '../../design/ui';
import { type Answered, Refused } from '../../work/InlineConfirm';

/** An entry as `SET_ACTOR` takes it: an agent, and its model and effort in the tool's own words, absent for account default. */
export type ActorChoice = { agent: string; model?: string | null; effort?: string | null };

/** The most choices a role holds (D205 §3.1, `ActorChoices.Most`). */
const MOST = 4;

/** The places a role works in (§2.7's scope kinds). */
const PLACES = ['machine', 'workspace', 'repository'] as const;

/** The roles whose staffing editor over `SET_ACTOR` this build offers (MODELROLE1c, 1d; the rest come with MODELROLE1e). */
const EDITABLE = new Set(['build', 'intake', 'ask-daoris', 'chat']);

/** The roles whose running conversation goes on with the next choice at its agent's limit (D205 §3.5(e)). */
const CONVERSING = new Set(['ask-daoris', 'chat']);

/** The work a role may be off for, and an actor's own words for off (D205 §2.1: they keep their cards' words on the folded rows). */
const OFF_WORKS = new Set(['intake', 'ask-daoris']);
const OFF_WORDS: Record<string, string> = { intake: 'settings.ai.intake.off', 'ask-daoris': 'settings.ai.helper.off' };

/** What the efforts' field suggests: the pipe door's words (D205 §3.1); a protocol agent's own come with its offers. */
const EFFORTS = ['low', 'medium', 'high', 'xhigh', 'max'];

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

type Draft = { key: number; agent: string; model: string; effort: string };

/** `role` is the catalogue's editor over `SET_ROLE`; `staffing` holds a role's order alone, over `SET_ACTOR`. */
export type Editing = {
  id: string; mode: 'role' | 'staffing'; isNew: boolean; revision: number | null; nameEn: string; nameZh: string; takes: string[]; work: string;
  place: string; placeId: string; drafts: Draft[];
};

/** One row: the catalogue's role where there is one, and the staffing `ACTORS` answers for the same id. */
type Line = { id: string; row: RoleRow | null; actor: ActorRow | null };

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
export function roleName(t: TFunction, language: string, row: Pick<RoleRow, 'id' | 'seeded' | 'nameEn' | 'nameZh'>): string {
  const authored = language.startsWith('zh') ? row.nameZh || row.nameEn : row.nameEn || row.nameZh;
  return authored || (row.seeded ? t(`settings.ai.actors.name.${row.id}`) : row.id);
}

/** Where a role works, in the page's words. */
function placeText(t: TFunction, kind?: string | null, id?: string | null): string {
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

/** A suggestion's press: its record and choices set, as `SET_ROLE` takes them, against the revision 0 of an id the catalogue lacks. */
function suggestionPress(role: SuggestedRole): RoleChangePress {
  return {
    change: 'set', id: role.id, revision: 0,
    role: { name: { en: role.nameEn, zh: role.nameZh }, takes: role.takes, scope: { kind: role.scopeKind, id: null } },
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
  catalogue, read = catalogue ? 'ok' : 'notOffered', staffing = [], agents, about = {}, busy = false, onSet, onSetActor, found = [], onAnswerFound,
  editor: held, onEditor,
}: {
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
  const [refused, setRefused] = useState<{ at: string; sentence: string } | null>(null);
  const [foundRefused, setFoundRefused] = useState<{ id: number; sentence: string } | null>(null);
  const name = (row: Pick<RoleRow, 'id' | 'seeded' | 'nameEn' | 'nameZh'>) => roleName(t, i18n.language, row);
  const kept = catalogue?.kept === true;
  const lines: Line[] = catalogue
    ? catalogue.roles.map((row) => ({ id: row.id, row, actor: staffing.find((each) => each.id === row.id) ?? null }))
    : staffing.map((actor) => ({ id: actor.id, row: null, actor }));
  const lineOf = (id: string) => lines.find((line) => line.id === id);
  const nameOfLine = (line: Line) => name(line.row ?? { id: line.id, seeded: true });
  const draftOf = (entry?: RoleChoice): Draft => ({
    key: next.current++, agent: entry?.agent ?? agents[0]?.value ?? '', model: entry?.model ?? '', effort: entry?.effort ?? '',
  });
  const orderOf = (line: Line): Draft[] => {
    const own = line.actor && line.actor.choices.length > 0 ? line.actor.choices : line.row?.choices ?? [];
    return own.length > 0 ? own.map(draftOf) : [draftOf()];
  };
  const press = (at: string, change: RoleChangePress, done?: () => void) => {
    setRefused(null);
    onSet(change, { done: () => done?.(), refused: (sentence) => setRefused({ at, sentence }) });
  };
  const pressActor = (at: string, actor: string, choices: ActorChoice[] | null) => {
    setRefused(null);
    onSetActor(actor, choices, { done: () => setEditing(null), refused: (sentence) => setRefused({ at, sentence }) });
  };
  /** A seeded row whose record is not this home's to change keeps the order editor alone. */
  const staffingOnly = (line: Line) => !line.row || (!kept && line.row.seeded);
  const open = (line: Line | null) => {
    setRefused(null);
    if (!line) {
      setEditing({
        id: '', mode: 'role', isNew: true, revision: null, nameEn: '', nameZh: '', takes: [], work: catalogue?.works[0] ?? 'build',
        place: 'machine', placeId: '', drafts: [draftOf()],
      });
      return;
    }
    const row = line.row;
    setEditing({
      id: line.id, mode: staffingOnly(line) ? 'staffing' : 'role', isNew: false, revision: row?.revision ?? null,
      nameEn: row?.nameEn ?? nameOfLine(line), nameZh: row?.nameZh ?? nameOfLine(line), takes: row?.takes ?? [],
      work: row?.takes[0] ?? catalogue?.works[0] ?? 'build', place: row?.scopeKind ?? 'machine', placeId: row?.scopeId ?? '', drafts: orderOf(line),
    });
  };
  const set = (patch: Partial<Editing>) => setEditing((now) => now && { ...now, ...patch });
  const change = (key: number, field: 'agent' | 'model' | 'effort', value: string) =>
    setEditing((now) => now && { ...now, drafts: now.drafts.map((draft) => (draft.key === key ? { ...draft, [field]: value } : draft)) });
  const move = (from: number, to: number) => setEditing((now) => {
    if (!now || to < 0 || to >= now.drafts.length) return now;
    const moved = [...now.drafts];
    [moved[from], moved[to]] = [moved[to]!, moved[from]!];
    return { ...now, drafts: moved };
  });
  const entries = (now: Editing): ActorChoice[] =>
    now.drafts.map((draft) => ({ agent: draft.agent.trim(), model: draft.model.trim() || null, effort: draft.effort.trim() || null }));
  /** *Save* (the editor's choices) or *Off* (`[]`): over `SET_ROLE` from the catalogue's editor, over `SET_ACTOR` from the order editor. */
  const save = (now: Editing, off = false) => {
    if (now.mode === 'staffing') {
      pressActor(now.id, now.id, off ? [] : entries(now));
      return;
    }
    const id = now.id.trim();
    press(now.isNew ? '' : now.id, {
      change: 'set', id, revision: now.revision,
      role: {
        name: { en: now.nameEn.trim(), zh: now.nameZh.trim() },
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
  const places = PLACES.map((place) => ({ value: place, label: t(`settings.ai.roles.where.${place}`) }));
  /** The catalogue's own presses, held where this home's file is its own. */
  const locked = busy || !kept;

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
      <form
        aria-label={title}
        className="grid gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          if (busy || (now.mode === 'role' && !kept)) return;
          save(now);
        }}
      >
        <p className="m-0 text-small font-medium text-ink">{title}</p>
        {now.mode === 'role' && (
          <FormGrid columns={['name', 'name']}>
            {now.isNew && (
              <FormField label={t('settings.ai.roles.field.id')}>
                <input aria-label={t('settings.ai.roles.field.id')} value={now.id} spellCheck={false} onChange={(event) => set({ id: event.target.value })} className={FIELD} />
              </FormField>
            )}
            <FormField label={t('settings.ai.roles.field.nameEn')}>
              <input aria-label={t('settings.ai.roles.field.nameEn')} value={now.nameEn} onChange={(event) => set({ nameEn: event.target.value })} className={FIELD} />
            </FormField>
            <FormField label={t('settings.ai.roles.field.nameZh')}>
              <input aria-label={t('settings.ai.roles.field.nameZh')} value={now.nameZh} onChange={(event) => set({ nameZh: event.target.value })} className={FIELD} />
            </FormField>
            <FormField label={t('settings.ai.roles.field.work')}>
              <SelectField value={now.work} onChange={(value) => set({ work: value })} options={works} ariaLabel={t('settings.ai.roles.field.work')} />
            </FormField>
            <FormField label={t('settings.ai.roles.field.where')}>
              <SelectField value={now.place} onChange={(value) => set({ place: value })} options={places} ariaLabel={t('settings.ai.roles.field.where')} />
            </FormField>
            {now.place !== 'machine' && (
              <FormField label={t('settings.ai.roles.field.placeId')}>
                <input
                  aria-label={t('settings.ai.roles.field.placeId')}
                  value={now.placeId}
                  spellCheck={false}
                  onChange={(event) => set({ placeId: event.target.value })}
                  className={FIELD}
                />
              </FormField>
            )}
          </FormGrid>
        )}
        <FormGrid columns={['name', 'name', 'short', 'acts']} floor="40rem">
          <FormRow head>
            <span className="text-meta text-ink-faint">{t('settings.ai.actors.field.agent')}</span>
            <span className="text-meta text-ink-faint">{t('settings.ai.actors.field.model')}</span>
            <span className="text-meta text-ink-faint">{t('settings.ai.actors.field.effort')}</span>
            <span />
          </FormRow>
          {now.drafts.map((draft, index) => {
            const n = index + 1;
            // What is in effect is shown as itself: a name typed at a terminal stays named.
            const options = draft.agent && !agents.some((agent) => agent.value === draft.agent) ? [...agents, { value: draft.agent, label: draft.agent }] : agents;
            return (
              <FormRow key={draft.key}>
                <FormField label={t('settings.ai.actors.field.agent')} when="stacked">
                  <SelectField value={draft.agent} onChange={(value) => change(draft.key, 'agent', value)} options={[...options]} ariaLabel={t('settings.ai.actors.agentOf', { n })} />
                </FormField>
                <FormField label={t('settings.ai.actors.field.model')} when="stacked">
                  <input
                    aria-label={t('settings.ai.actors.modelOf', { n })}
                    value={draft.model}
                    spellCheck={false}
                    placeholder={t('settings.ai.actors.accountDefault')}
                    onChange={(event) => change(draft.key, 'model', event.target.value)}
                    className={FIELD}
                  />
                </FormField>
                <FormField label={t('settings.ai.actors.field.effort')} when="stacked">
                  <input
                    aria-label={t('settings.ai.actors.effortOf', { n })}
                    value={draft.effort}
                    spellCheck={false}
                    list="role-efforts"
                    placeholder={t('settings.ai.actors.accountDefault')}
                    onChange={(event) => change(draft.key, 'effort', event.target.value)}
                    className={FIELD}
                  />
                </FormField>
                <span className="flex items-center">
                  <Button variant="ghost" disabled={busy || index === 0} aria-label={t('settings.ai.actors.up', { n })} onClick={() => move(index, index - 1)}>
                    <Icon name="chevronUp" size={12} />
                  </Button>
                  <Button
                    variant="ghost"
                    disabled={busy || index === now.drafts.length - 1}
                    aria-label={t('settings.ai.actors.down', { n })}
                    onClick={() => move(index, index + 1)}
                  >
                    <Icon name="chevronDown" size={12} />
                  </Button>
                  <Button
                    variant="ghost"
                    disabled={busy || now.drafts.length === 1}
                    aria-label={t('settings.ai.actors.remove', { n })}
                    onClick={() => setEditing((was) => was && { ...was, drafts: was.drafts.filter((each) => each.key !== draft.key) })}
                  >
                    <Icon name="x" size={12} />
                  </Button>
                </span>
              </FormRow>
            );
          })}
        </FormGrid>
        <datalist id="role-efforts">
          {EFFORTS.map((effort) => <option key={effort} value={effort} />)}
        </datalist>
        <p className="m-0 text-small text-ink-soft">{t('settings.ai.actors.later')}</p>
        <p className="m-0 text-small text-ink-soft">{t('settings.ai.actors.outranks')}</p>
        {/* What a running conversation does, said where the order it follows is set (D205 §7, round 4). */}
        <p className="m-0 text-small text-ink-soft">{t(CONVERSING.has(now.id) ? 'settings.ai.actors.goesOn' : 'settings.ai.actors.neverMoves')}</p>
        <p className="m-0 text-small text-ink-soft">{t('settings.ai.roles.choicesNote')}</p>
        <div className="flex flex-wrap gap-2">
          <Button variant="ghost" disabled={busy || now.drafts.length >= MOST} onClick={() => set({ drafts: [...now.drafts, draftOf()] })}>
            <Icon name="plus" size={12} />
            {t('settings.ai.actors.add')}
          </Button>
          <Button type="submit" disabled={busy || (now.mode === 'role' && !kept)}>{t('settings.ai.actors.save')}</Button>
          {mayOff && !isOff && !now.isNew && (
            <Button variant="ghost" disabled={busy || (now.mode === 'role' && !kept)} onClick={() => save(now, true)}>{t('settings.ai.actors.off')}</Button>
          )}
          {mayClear && (
            <Button variant="ghost" disabled={busy} onClick={() => pressActor(at, now.id, null)}>{t('settings.ai.roles.clear')}</Button>
          )}
          <Button variant="ghost" disabled={busy} onClick={() => { setRefused(null); setEditing(null); }}>{t('common.cancel')}</Button>
        </div>
        <p className="m-0 text-small text-ink-faint">{t('settings.ai.actors.most', { count: MOST })}</p>
        {refused && refused.at === at && <Refused sentence={refused.sentence} />}
      </form>
    );
  };

  return (
    <Card>
      <CardHeader title={t('settings.ai.roles.title')} />
      <Prose className="mb-3 mt-0 text-small text-ink-soft">{t('settings.ai.roles.body')}</Prose>
      {catalogue && !kept && <p className="m-0 mb-2 text-small text-ink-soft">{t('settings.ai.roles.unkept')}</p>}
      {read === 'notOffered' && <p className="m-0 mb-2 text-small text-ink-soft">{t('settings.ai.roles.notOffered')}</p>}
      {read === 'failed' && <p role="alert" className="m-0 mb-2 text-small text-ink-soft">{t('settings.ai.roles.readFailed')}</p>}
      {catalogue && catalogue.roles.length === 0 && <p className="m-0 mb-2 text-small text-ink-soft">{t('settings.ai.roles.none')}</p>}
      <SettingList>
        {lines.map((line) => {
          const { row, actor } = line;
          const named = nameOfLine(line);
          const open_ = editing?.id === line.id && !editing.isNew;
          const mayEdit = row
            ? (row.state === 'seed' || row.state === 'set') && (staffingOnly(line) ? !!actor && EDITABLE.has(line.id) : kept)
            : !!actor && EDITABLE.has(line.id);
          return (
            <SettingRow
              key={line.id}
              label={named}
              why={about[line.id]?.why}
              control={!open_ && (
                <>
                  <span className="text-body text-ink wrap-anywhere">{row ? roleSummary(t, row, actor) : actorSummary(t, actor!)}</span>
                  {mayEdit && (
                    <Button variant="ghost" disabled={busy || editing !== null} aria-label={t('settings.ai.roles.editOf', { role: named })} onClick={() => open(line)}>
                      {t('settings.ai.actors.edit')}
                    </Button>
                  )}
                  {row && row.state !== 'removed' && (
                    <Button
                      variant="ghost"
                      disabled={locked || editing !== null}
                      aria-label={t('settings.ai.roles.removeOf', { role: named })}
                      onClick={() => press(line.id, { change: 'remove', id: line.id, revision: row.revision })}
                    >
                      {t('settings.ai.roles.remove')}
                    </Button>
                  )}
                  {row && row.seeded && row.state !== 'seed' && (
                    <Button
                      variant="ghost"
                      disabled={locked || editing !== null}
                      aria-label={t('settings.ai.roles.defaultOf', { role: named })}
                      onClick={() => press(line.id, { change: 'default', id: line.id, revision: row.revision })}
                    >
                      {t('settings.ai.roles.default')}
                    </Button>
                  )}
                </>
              )}
            >
              {!open_ && about[line.id]?.below}
              {open_ && editing && editor(editing)}
              {!open_ && refused?.at === line.id && <Refused sentence={refused.sentence} />}
            </SettingRow>
          );
        })}
      </SettingList>
      {catalogue && (editing?.isNew ? <div className="mt-3">{editor(editing)}</div> : (
        <div className="mt-3">
          <Button variant="ghost" disabled={locked || editing !== null} onClick={() => open(null)}>
            <Icon name="plus" size={12} />
            {t('settings.ai.roles.add')}
          </Button>
        </div>
      ))}
      {catalogue?.suggested.map((role) => {
        const named = name({ id: role.id, seeded: false, nameEn: role.nameEn, nameZh: role.nameZh });
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

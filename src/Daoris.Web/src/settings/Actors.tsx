import type { TFunction } from 'i18next';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ActorRow } from '../bridge/driver';
import { Button, Card, CardHeader, FormField, FormGrid, FormRow, Icon, Inline, Prose, SelectField, SettingList, SettingRow } from '../design/ui';
import { type Answered, Refused } from '../work/InlineConfirm';

/** An entry as `SET_ACTOR` takes it: an agent, and its model and effort in the tool's own words, absent for account default. */
export type ActorChoice = { agent: string; model?: string | null; effort?: string | null };

/** An entry as the editor holds it: every field text, and a key that stays with it as it moves. */
type Draft = { key: number; agent: string; model: string; effort: string };

/** The most choices an actor holds (D205 §3.1, `ActorChoices.Most`). */
const MOST = 4;

/** The actors whose editor this build offers (MODELROLE1c: Build's; rows d and e add the others). */
const EDITABLE = new Set(['build']);

/** What the efforts' field suggests: the pipe door's words (D205 §3.1); a protocol agent's own come with its offers. */
const EFFORTS = ['low', 'medium', 'high', 'xhigh', 'max'];

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

/** An entry's words: agent, model and effort, each in the tool's own words, account default where it names none. */
export function entryText(t: TFunction, entry: ActorChoice): string {
  return t('settings.ai.actors.entry', {
    agent: entry.agent,
    model: entry.model || t('settings.ai.actors.accountDefault'),
    effort: entry.effort || t('settings.ai.actors.accountDefault'),
  });
}

/**
 * One actor's row in its folded words (D205 §7): its first choice and how many more, *as before* where it reads today's settings,
 * *Off*, a fenced actor's own words, or the driver's sentence where its stored entry does not read.
 */
export function actorSummary(t: TFunction, row: ActorRow): string {
  if (row.state === 'unreadable') return t('settings.ai.actors.unreadable', { error: row.error ?? '' });
  if (row.state === 'off') return t('settings.ai.actors.off');
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

/**
 * **Actors** (MODELROLE1c, D205 §7): each kind of work Daoris starts, a row each, in the screen's order, its order of choices folded
 * to one line; and Build's editor, one choice a row of agent, model and effort, *Move up*, *Move down* and *Remove*, *Add a choice*
 * up to four, *Save* and *Clear* (back to as before). One editor is open at a time, where *Edit* was pressed (D185 R3); a refusal is
 * the driver's sentence under its *Save*, the editor still open as it was. The page judges nothing: the driver's one judge does.
 *
 * A molecule: every state is reached by its props, and every press goes out.
 */
export function ActorsCard({ rows, agents, busy = false, onSave }: {
  rows: readonly ActorRow[];
  /** The agents this machine has, to name in a choice. */
  agents: readonly { value: string; label: string }[];
  busy?: boolean;
  /** An actor's whole order, or null back to as before; the answer says whether it landed or the driver's sentence. */
  onSave: (actor: string, choices: ActorChoice[] | null, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const next = useRef(0);
  const [editing, setEditing] = useState<{ actor: string; drafts: Draft[] } | null>(null);
  const [refused, setRefused] = useState<string | null>(null);
  const draftOf = (entry?: ActorChoice): Draft => ({
    key: next.current++, agent: entry?.agent ?? agents[0]?.value ?? '', model: entry?.model ?? '', effort: entry?.effort ?? '',
  });
  const open = (row: ActorRow) => {
    setRefused(null);
    setEditing({ actor: row.id, drafts: row.choices.length > 0 ? row.choices.map(draftOf) : [draftOf()] });
  };
  const change = (key: number, field: 'agent' | 'model' | 'effort', value: string) =>
    setEditing((now) => now && { ...now, drafts: now.drafts.map((draft) => (draft.key === key ? { ...draft, [field]: value } : draft)) });
  const move = (from: number, to: number) => setEditing((now) => {
    if (!now || to < 0 || to >= now.drafts.length) return now;
    const moved = [...now.drafts];
    [moved[from], moved[to]] = [moved[to]!, moved[from]!];
    return { ...now, drafts: moved };
  });
  const save = (actor: string, choices: ActorChoice[] | null) => {
    setRefused(null);
    onSave(actor, choices, { done: () => setEditing(null), refused: setRefused });
  };
  const name = (id: string) => t(`settings.ai.actors.name.${id}`);

  return (
    <Card>
      <CardHeader title={t('settings.ai.actors.title')} />
      <Prose className="mb-3 mt-0 text-small text-ink-soft">{t('settings.ai.actors.body')}</Prose>
      <SettingList>
        {rows.map((row) => {
          const open_ = editing?.actor === row.id;
          return (
            <SettingRow
              key={row.id}
              label={name(row.id)}
              control={!open_ && (
                <>
                  <span className="text-body text-ink wrap-anywhere">{actorSummary(t, row)}</span>
                  {EDITABLE.has(row.id) && (
                    <Button variant="ghost" disabled={busy || editing !== null} onClick={() => open(row)}>{t('settings.ai.actors.edit')}</Button>
                  )}
                </>
              )}
            >
              {open_ && editing && (
                <form
                  aria-label={t('settings.ai.actors.editorTitle', { actor: name(row.id) })}
                  className="grid gap-2"
                  onSubmit={(event) => {
                    event.preventDefault();
                    if (busy) return;
                    save(row.id, editing.drafts.map((draft) => ({
                      agent: draft.agent.trim(),
                      model: draft.model.trim() || null,
                      effort: draft.effort.trim() || null,
                    })));
                  }}
                >
                  <p className="m-0 text-small font-medium text-ink">{t('settings.ai.actors.editorTitle', { actor: name(row.id) })}</p>
                  <FormGrid columns={['name', 'name', 'short', 'acts']} floor="40rem">
                    <FormRow head>
                      <span className="text-meta text-ink-faint">{t('settings.ai.actors.field.agent')}</span>
                      <span className="text-meta text-ink-faint">{t('settings.ai.actors.field.model')}</span>
                      <span className="text-meta text-ink-faint">{t('settings.ai.actors.field.effort')}</span>
                      <span />
                    </FormRow>
                    {editing.drafts.map((draft, at) => {
                      const n = at + 1;
                      // What is in effect is shown as itself, as the intake's agent is: a name typed at a terminal stays named.
                      const options = draft.agent && !agents.some((agent) => agent.value === draft.agent)
                        ? [...agents, { value: draft.agent, label: draft.agent }]
                        : agents;
                      return (
                        <FormRow key={draft.key}>
                          <FormField label={t('settings.ai.actors.field.agent')} when="stacked">
                            <SelectField
                              value={draft.agent}
                              onChange={(value) => change(draft.key, 'agent', value)}
                              options={[...options]}
                              ariaLabel={t('settings.ai.actors.agentOf', { n })}
                            />
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
                              list="actor-efforts"
                              placeholder={t('settings.ai.actors.accountDefault')}
                              onChange={(event) => change(draft.key, 'effort', event.target.value)}
                              className={FIELD}
                            />
                          </FormField>
                          <span className="flex items-center">
                            <Button variant="ghost" disabled={busy || at === 0} aria-label={t('settings.ai.actors.up', { n })} onClick={() => move(at, at - 1)}>
                              <Icon name="chevronUp" size={12} />
                            </Button>
                            <Button
                              variant="ghost"
                              disabled={busy || at === editing.drafts.length - 1}
                              aria-label={t('settings.ai.actors.down', { n })}
                              onClick={() => move(at, at + 1)}
                            >
                              <Icon name="chevronDown" size={12} />
                            </Button>
                            <Button
                              variant="ghost"
                              disabled={busy || editing.drafts.length === 1}
                              aria-label={t('settings.ai.actors.remove', { n })}
                              onClick={() => setEditing((now) => now && { ...now, drafts: now.drafts.filter((each) => each.key !== draft.key) })}
                            >
                              <Icon name="x" size={12} />
                            </Button>
                          </span>
                        </FormRow>
                      );
                    })}
                  </FormGrid>
                  <datalist id="actor-efforts">
                    {EFFORTS.map((effort) => <option key={effort} value={effort} />)}
                  </datalist>
                  <p className="m-0 text-small text-ink-soft">{t('settings.ai.actors.later')}</p>
                  <p className="m-0 text-small text-ink-soft">{t('settings.ai.actors.outranks')}</p>
                  <p className="m-0 text-small text-ink-soft">{t('settings.ai.actors.neverMoves')}</p>
                  <div className="flex flex-wrap gap-2">
                    <Button
                      variant="ghost"
                      disabled={busy || editing.drafts.length >= MOST}
                      onClick={() => setEditing((now) => now && { ...now, drafts: [...now.drafts, draftOf()] })}
                    >
                      <Icon name="plus" size={12} />
                      {t('settings.ai.actors.add')}
                    </Button>
                    <Button type="submit" disabled={busy}>{t('settings.ai.actors.save')}</Button>
                    {row.state !== 'before' && (
                      <Button variant="ghost" disabled={busy} onClick={() => save(row.id, null)}>{t('settings.ai.actors.clear')}</Button>
                    )}
                    <Button variant="ghost" disabled={busy} onClick={() => { setRefused(null); setEditing(null); }}>{t('common.cancel')}</Button>
                  </div>
                  <p className="m-0 text-small text-ink-faint">{t('settings.ai.actors.most', { count: MOST })}</p>
                  {refused && <Refused sentence={refused} />}
                </form>
              )}
            </SettingRow>
          );
        })}
      </SettingList>
      <p className="m-0 mt-3 text-small text-ink-faint"><Inline text={t('settings.ai.actors.footnote')} /></p>
    </Card>
  );
}

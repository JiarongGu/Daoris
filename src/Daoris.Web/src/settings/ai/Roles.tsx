import type { TFunction } from 'i18next';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { RoleCatalogue, RoleChangePress, RoleChoice, RoleRow, SuggestedRole } from '../../data/wiring';
import { Button, Card, CardHeader, FormField, FormGrid, FormRow, Icon, Inline, Prose, SelectField, SettingList, SettingRow } from '../../design/ui';
import { type Answered, Refused } from '../../work/InlineConfirm';
import { entryText } from './Actors';

/** The most choices a role holds (D205 §3.1, `ActorChoices.Most`). */
const MOST = 4;

/** The places a role works in (§2.7's scope kinds). */
const PLACES = ['machine', 'workspace', 'repository'] as const;

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

type Draft = { key: number; agent: string; model: string; effort: string };

type Editing = {
  id: string; isNew: boolean; revision: number | null; nameEn: string; nameZh: string; takes: string[]; work: string;
  place: string; placeId: string; drafts: Draft[];
};

/** A role's name as authored, in the page's language where it has one; a seeded role with no record is its actor's name. */
export function roleName(t: TFunction, language: string, row: Pick<RoleRow, 'id' | 'seeded' | 'nameEn' | 'nameZh'>): string {
  const authored = language.startsWith('zh') ? row.nameZh || row.nameEn : row.nameEn || row.nameZh;
  return authored || (row.seeded ? t(`settings.ai.actors.name.${row.id}`) : row.id);
}

/** Where a role works, in the page's words. */
function placeText(t: TFunction, kind?: string | null, id?: string | null): string {
  return kind === 'workspace' || kind === 'repository' ? t(`settings.ai.roles.place.${kind}`, { id: id ?? '' }) : t('settings.ai.roles.place.machine');
}

/** A role's folded line: the work it takes, where, and its first choice and how many more. */
export function roleSummary(t: TFunction, row: RoleRow): string {
  if (row.state === 'removed') return t('settings.ai.roles.removed');
  if (row.state === 'unreadable') return t('settings.ai.roles.unreadable', { error: row.error ?? '' });
  const works = row.takes.map((work) => t(`settings.ai.roles.work.${work}`)).join(t('settings.ai.actors.and'));
  const choices = row.choices ?? [];
  const order = choices.length === 0
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
 * **The role catalogue** (FOUNDATION1y-i, the foundation design §2.7): every role that takes Daoris's work, the seed's and the person's,
 * its name as authored, the work it takes, where, and its choices; *Add a role*, and on each row *Edit*, *Remove* and, for a seeded role
 * the person changed or removed, *Back to the default*. One editor is open at a time: its id (a new role's), its names in English and in
 * Chinese, its work, its place, and one to four choices of agent, model and effort. A removed role's work waits until a role takes it, and
 * running work goes on. Under them, the roles Daoris suggests, each with *Add*, whose words say it can spend more. The page judges
 * nothing: every press goes out as `SET_ROLE` takes it, and a refusal is the driver's sentence under its press.
 *
 * A molecule: every state is reached by its props, and every press goes out.
 */
export function RolesCard({ catalogue, agents, busy = false, onSet }: {
  catalogue: RoleCatalogue;
  /** The agents this machine has, to name in a choice. */
  agents: readonly { value: string; label: string }[];
  busy?: boolean;
  /** A role's change; the answer says whether it landed or the driver's sentence. */
  onSet: (press: RoleChangePress, answered: Answered) => void;
}) {
  const { t, i18n } = useTranslation();
  const next = useRef(0);
  const [editing, setEditing] = useState<Editing | null>(null);
  const [refused, setRefused] = useState<{ at: string; sentence: string } | null>(null);
  const name = (row: Pick<RoleRow, 'id' | 'seeded' | 'nameEn' | 'nameZh'>) => roleName(t, i18n.language, row);
  const draftOf = (entry?: RoleChoice): Draft => ({
    key: next.current++, agent: entry?.agent ?? agents[0]?.value ?? '', model: entry?.model ?? '', effort: entry?.effort ?? '',
  });
  const press = (at: string, change: RoleChangePress, done?: () => void) => {
    setRefused(null);
    onSet(change, { done: () => done?.(), refused: (sentence) => setRefused({ at, sentence }) });
  };
  const open = (row: RoleRow | null) => {
    setRefused(null);
    setEditing(row
      ? {
        id: row.id, isNew: false, revision: row.revision, nameEn: row.nameEn ?? name(row), nameZh: row.nameZh ?? name(row), takes: row.takes,
        work: row.takes[0] ?? catalogue.works[0] ?? 'build', place: row.scopeKind ?? 'machine', placeId: row.scopeId ?? '',
        drafts: (row.choices ?? []).map(draftOf),
      }
      : { id: '', isNew: true, revision: null, nameEn: '', nameZh: '', takes: [], work: catalogue.works[0] ?? 'build', place: 'machine', placeId: '', drafts: [draftOf()] });
  };
  const set = (patch: Partial<Editing>) => setEditing((now) => now && { ...now, ...patch });
  const change = (key: number, field: 'agent' | 'model' | 'effort', value: string) =>
    setEditing((now) => now && { ...now, drafts: now.drafts.map((draft) => (draft.key === key ? { ...draft, [field]: value } : draft)) });
  const save = (now: Editing) => {
    const id = now.id.trim();
    press(now.isNew ? '' : now.id, {
      change: 'set', id, revision: now.revision,
      role: {
        name: { en: now.nameEn.trim(), zh: now.nameZh.trim() },
        takes: now.takes[0] === now.work ? now.takes : [now.work],
        scope: { kind: now.place, id: now.place === 'machine' ? null : now.placeId.trim() || null },
      },
      choices: now.drafts.length > 0
        ? now.drafts.map((draft) => ({ agent: draft.agent.trim(), model: draft.model.trim() || null, effort: draft.effort.trim() || null }))
        : now.isNew ? [] : null,
    }, () => setEditing(null));
  };
  const works = catalogue.works.map((work) => ({ value: work, label: t(`settings.ai.roles.work.${work}`) }));
  const places = PLACES.map((place) => ({ value: place, label: t(`settings.ai.roles.where.${place}`) }));
  const locked = busy || !catalogue.kept;

  const editor = (now: Editing) => {
    const title = now.isNew ? t('settings.ai.roles.newTitle') : name(catalogue.roles.find((row) => row.id === now.id) ?? { id: now.id, seeded: false });
    return (
      <form
        aria-label={title}
        className="grid gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          if (!locked) save(now);
        }}
      >
        <p className="m-0 text-small font-medium text-ink">{title}</p>
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
        <FormGrid columns={['name', 'name', 'short', 'acts']} floor="40rem">
          <FormRow head>
            <span className="text-meta text-ink-faint">{t('settings.ai.actors.field.agent')}</span>
            <span className="text-meta text-ink-faint">{t('settings.ai.actors.field.model')}</span>
            <span className="text-meta text-ink-faint">{t('settings.ai.actors.field.effort')}</span>
            <span />
          </FormRow>
          {now.drafts.map((draft, at) => {
            const n = at + 1;
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
                    placeholder={t('settings.ai.actors.accountDefault')}
                    onChange={(event) => change(draft.key, 'effort', event.target.value)}
                    className={FIELD}
                  />
                </FormField>
                <span className="flex items-center">
                  <Button
                    variant="ghost"
                    disabled={busy}
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
        <p className="m-0 text-small text-ink-soft">{t('settings.ai.roles.choicesNote')}</p>
        <div className="flex flex-wrap gap-2">
          <Button variant="ghost" disabled={busy || now.drafts.length >= MOST} onClick={() => set({ drafts: [...now.drafts, draftOf()] })}>
            <Icon name="plus" size={12} />
            {t('settings.ai.actors.add')}
          </Button>
          <Button type="submit" disabled={locked}>{t('settings.ai.actors.save')}</Button>
          <Button variant="ghost" disabled={busy} onClick={() => { setRefused(null); setEditing(null); }}>{t('common.cancel')}</Button>
        </div>
        {refused && refused.at === (now.isNew ? '' : now.id) && <Refused sentence={refused.sentence} />}
      </form>
    );
  };

  return (
    <Card>
      <CardHeader title={t('settings.ai.roles.title')} />
      <Prose className="mb-3 mt-0 text-small text-ink-soft">{t('settings.ai.roles.body')}</Prose>
      {!catalogue.kept && <p className="m-0 mb-2 text-small text-ink-soft">{t('settings.ai.roles.unkept')}</p>}
      <SettingList>
        {catalogue.roles.map((row) => {
          const named = name(row);
          const open_ = editing?.id === row.id && !editing.isNew;
          return (
            <SettingRow
              key={row.id}
              label={named}
              control={!open_ && (
                <>
                  <span className="text-body text-ink wrap-anywhere">{roleSummary(t, row)}</span>
                  {(row.state === 'seed' || row.state === 'set') && (
                    <Button variant="ghost" disabled={locked || editing !== null} aria-label={t('settings.ai.roles.editOf', { role: named })} onClick={() => open(row)}>
                      {t('settings.ai.actors.edit')}
                    </Button>
                  )}
                  {row.state !== 'removed' && (
                    <Button
                      variant="ghost"
                      disabled={locked || editing !== null}
                      aria-label={t('settings.ai.roles.removeOf', { role: named })}
                      onClick={() => press(row.id, { change: 'remove', id: row.id, revision: row.revision })}
                    >
                      {t('settings.ai.roles.remove')}
                    </Button>
                  )}
                  {row.seeded && row.state !== 'seed' && (
                    <Button
                      variant="ghost"
                      disabled={locked || editing !== null}
                      aria-label={t('settings.ai.roles.defaultOf', { role: named })}
                      onClick={() => press(row.id, { change: 'default', id: row.id, revision: row.revision })}
                    >
                      {t('settings.ai.roles.default')}
                    </Button>
                  )}
                </>
              )}
            >
              {open_ && editing && editor(editing)}
              {!open_ && refused?.at === row.id && <Refused sentence={refused.sentence} />}
            </SettingRow>
          );
        })}
      </SettingList>
      {editing?.isNew ? <div className="mt-3">{editor(editing)}</div> : (
        <div className="mt-3">
          <Button variant="ghost" disabled={locked || editing !== null} onClick={() => open(null)}>
            <Icon name="plus" size={12} />
            {t('settings.ai.roles.add')}
          </Button>
        </div>
      )}
      {catalogue.suggested.map((role) => {
        const named = name({ id: role.id, seeded: false, nameEn: role.nameEn, nameZh: role.nameZh });
        const works = role.takes.map((work) => t(`settings.ai.roles.work.${work}`)).join(t('settings.ai.actors.and'));
        return (
          <section key={role.id} aria-label={t('settings.ai.roles.suggested.title', { role: named })} className="mt-3 rounded-control border border-line p-2">
            <p className="m-0 text-body font-medium text-ink">{t('settings.ai.roles.suggested.title', { role: named })}</p>
            <p className="m-0 text-small text-ink-soft wrap-anywhere">
              {t('settings.ai.roles.suggested.body', { role: named, work: works, choice: role.choices[0] ? entryText(t, role.choices[0]) : '' })}
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
      <p className="m-0 mt-3 text-small text-ink-faint"><Inline text={t('settings.ai.roles.footnote')} /></p>
    </Card>
  );
}

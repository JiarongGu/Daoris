import { useTranslation } from 'react-i18next';
import { Button, FormField, FormGrid, FormRow, Icon, SelectField, WhyGlyph } from '../../design/ui';
import { Refused } from '../../work/InlineConfirm';
import type { SettingsChoices } from '../tools';
import { OfferedField } from './OfferedField';

/** The most choices a role holds (D205 §3.1, `ActorChoices.Most`). */
const MOST = 4;

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

export type Draft = { key: number; agent: string; model: string; effort: string };

/** `role` is the catalogue's editor over `SET_ROLE`; `staffing` holds a role's order alone, over `SET_ACTOR`. */
export type Editing = {
  id: string; mode: 'role' | 'staffing'; isNew: boolean; revision: number | null; name: string; nameIn: [string, string][]; takes: string[]; work: string;
  place: string; placeId: string; drafts: Draft[];
};

export type RoleEditorProps = {
  editing: Editing;
  /** The editor's changes, as a value or from the state it had: two edits in one tick both land. */
  onEditing: (change: Editing | ((now: Editing) => Editing)) => void;
  /** The form's name, also its heading. */
  title: string;
  agents: readonly { value: string; label: string }[];
  /** What each agent offers for a model and an effort, by agent; absent or null, the field is a typed name. */
  offers: Readonly<Record<string, SettingsChoices | null | undefined>>;
  /** The works a role may take, worded. */
  works: readonly { value: string; label: string }[];
  busy: boolean;
  /** The catalogue's own record is not this home's to change, so Save is held. */
  held: boolean;
  /** Where the role is placed, in the page's words. */
  placed: string;
  /** The one sentence of consequence before Save. */
  consequence: string;
  /** Whether a running conversation goes on with the next choice, which the ⓘ says. */
  conversing: boolean;
  mayOff: boolean;
  isOff: boolean;
  mayClear: boolean;
  /** The driver's sentence for a refused press. */
  refused: string | null;
  onSave: (off: boolean) => void;
  onClear: () => void;
  onCancel: () => void;
};

/**
 * **The role editor** (SETUPUX1l, the Setup design §2.4): a role's choices of agent, model and effort first, each model and effort from what
 * the agent offers or typed after *other…*; *Edit role*, folded, holds its names, work and place; the prose of how choices run stands
 * behind one ⓘ and one sentence of consequence before *Save*. A role that takes several works names them and changes none.
 *
 * A molecule shared by All roles, a place's Roles and, later, a step's: it reads no bridge, and every press goes out.
 */
export function RoleEditor({
  editing: now, onEditing, title, agents, offers, works, busy, held, placed, consequence, conversing, mayOff, isOff, mayClear, refused, onSave, onClear, onCancel,
}: RoleEditorProps) {
  const { t } = useTranslation();
  const set = (patch: Partial<Editing>) => onEditing((was) => ({ ...was, ...patch }));
  const change = (key: number, field: 'agent' | 'model' | 'effort', value: string) =>
    onEditing((was) => ({ ...was, drafts: was.drafts.map((draft) => (draft.key === key ? { ...draft, [field]: value } : draft)) }));
  const move = (from: number, to: number) => onEditing((was) => {
    if (to < 0 || to >= was.drafts.length) return was;
    const moved = [...was.drafts];
    [moved[from], moved[to]] = [moved[to]!, moved[from]!];
    return { ...was, drafts: moved };
  });
  const add = () => onEditing((was) => ({
    ...was, drafts: [...was.drafts, { key: Math.max(0, ...was.drafts.map((draft) => draft.key)) + 1, agent: agents[0]?.value ?? '', model: '', effort: '' }],
  }));
  const locked = busy || (now.mode === 'role' && held);
  const shared = now.takes.length > 1;
  const labelOf = (work: string) => works.find((each) => each.value === work)?.label ?? work;
  const how = [
    t('settings.ai.actors.later'), t('settings.ai.actors.outranks'),
    t(conversing ? 'settings.ai.actors.goesOn' : 'settings.ai.actors.neverMoves'),
    t('settings.ai.roles.choicesNote'), t('settings.ai.actors.most', { count: MOST }),
  ].join(' ');

  return (
    <form
      aria-label={title}
      className="grid gap-2"
      onSubmit={(event) => {
        event.preventDefault();
        if (!locked) onSave(false);
      }}
    >
      <p className="m-0 text-small font-medium text-ink">{title}</p>
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
                <OfferedField
                  key={draft.agent}
                  value={draft.model}
                  offered={offers[draft.agent]?.models}
                  label={t('settings.ai.actors.modelOf', { n })}
                  otherLabel={t('settings.ai.roles.offers.modelOther', { n })}
                  onChange={(value) => change(draft.key, 'model', value)}
                />
              </FormField>
              <FormField label={t('settings.ai.actors.field.effort')} when="stacked">
                <OfferedField
                  key={draft.agent}
                  value={draft.effort}
                  offered={offers[draft.agent]?.efforts}
                  label={t('settings.ai.actors.effortOf', { n })}
                  otherLabel={t('settings.ai.roles.offers.effortOther', { n })}
                  onChange={(value) => change(draft.key, 'effort', value)}
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
                  onClick={() => onEditing((was) => ({ ...was, drafts: was.drafts.filter((each) => each.key !== draft.key) }))}
                >
                  <Icon name="x" size={12} />
                </Button>
              </span>
            </FormRow>
          );
        })}
      </FormGrid>
      <div className="flex flex-wrap items-center gap-2">
        <Button variant="ghost" disabled={busy || now.drafts.length >= MOST} onClick={add}>
          <Icon name="plus" size={12} />
          {t('settings.ai.actors.add')}
        </Button>
        <span className="inline-flex items-center gap-1 text-small text-ink-soft">
          <WhyGlyph why={how} />
          {t('settings.ai.roles.editor.how')}
        </span>
      </div>
      {now.mode === 'role' && (
        <details open={now.isNew} className="grid gap-2">
          <summary className="cursor-pointer text-small font-medium text-ink-soft">{t('settings.ai.roles.editRole')}</summary>
          <p className="m-0 mb-2 text-small text-ink-soft">{placed}</p>
          <FormGrid columns={['name', 'name']}>
            <FormField label={t('settings.ai.roles.field.name')}>
              <input aria-label={t('settings.ai.roles.field.name')} value={now.name} onChange={(event) => set({ name: event.target.value })} className={FIELD} />
            </FormField>
            {now.nameIn.map(([tag, text], at) => (
              <FormField key={at} label={t('settings.ai.roles.field.nameIn')}>
                <span className="flex min-w-0 gap-1.5">
                  <input
                    aria-label={t('settings.ai.roles.field.nameInTag')} value={tag} placeholder="fr" className={`${FIELD} !w-20 shrink-0`}
                    onChange={(event) => set({ nameIn: now.nameIn.map((each, i) => (i === at ? [event.target.value, each[1]] : each)) })}
                  />
                  <input
                    aria-label={t('settings.ai.roles.field.nameInText')} value={text} className={FIELD}
                    onChange={(event) => set({ nameIn: now.nameIn.map((each, i) => (i === at ? [each[0], event.target.value] : each)) })}
                  />
                  <Button variant="ghost" onClick={() => set({ nameIn: now.nameIn.filter((_, i) => i !== at) })}>{t('settings.ai.roles.field.nameInRemove')}</Button>
                </span>
              </FormField>
            ))}
            <Button variant="ghost" onClick={() => set({ nameIn: [...now.nameIn, ['', '']] })}>{t('settings.ai.roles.field.nameInAdd')}</Button>
            {shared ? (
              <p className="m-0 text-small text-ink-soft">
                {t('settings.ai.roles.field.takesAll', { works: now.takes.map(labelOf).join(t('settings.ai.actors.and')) })}
              </p>
            ) : (
              <FormField label={t('settings.ai.roles.field.work')}>
                <SelectField value={now.work} onChange={(value) => set({ work: value })} options={[...works]} ariaLabel={t('settings.ai.roles.field.work')} />
              </FormField>
            )}
          </FormGrid>
        </details>
      )}
      <p className="m-0 text-small text-ink-soft">{consequence}</p>
      <div className="flex flex-wrap gap-2">
        <Button type="submit" disabled={locked}>{t('settings.ai.actors.save')}</Button>
        {mayOff && !isOff && !now.isNew && (
          <Button variant="ghost" disabled={locked} onClick={() => onSave(true)}>{t('settings.ai.actors.off')}</Button>
        )}
        {mayClear && <Button variant="ghost" disabled={busy} onClick={onClear}>{t('settings.ai.roles.clear')}</Button>}
        <Button variant="ghost" disabled={busy} onClick={onCancel}>{t('common.cancel')}</Button>
      </div>
      {refused && <Refused sentence={refused} />}
    </form>
  );
}

import { useTranslation } from 'react-i18next';
import { Button, FormField, FormGrid, FormRow, Icon, SelectField, WhyGlyph } from '../../design/ui';
import { Refused } from '../../work/InlineConfirm';
import type { RoleNext, StaffCondition } from '../../data/wiring';
import type { SettingsChoices } from '../tools';
import { type HoldDraft, holdReads } from './appointedHold';
import { HoldFields } from './HoldFields';
import { OfferedField } from './OfferedField';
import { HowItChooses, LimitsFold, MovesFold, WeightField } from './RolePolicy';
import { aimOf, conversational, type PolicyDraft, type PolicyFields, type PolicyMove, type Preset, withPreset } from './staffPolicyDraft';

/** The most choices a role holds (D205 §3.1, `ActorChoices.Most`). */
const MOST = 4;

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

/** A choice as drafted: its tuple, and the policy fields typed beside it (weight, share, budget, works). */
export type Draft = { key: number; agent: string; model: string; effort: string } & PolicyFields;

/**
 * A role in the catalogue's one editor, over `SET_ROLE` (ONEWAY1a: its record and its choices together). `policy` is the role's staffing
 * policy (STAFFPOLICY1f), absent where the role keeps none: a new role, a brainstorm or a driver before policies.
 */
export type Editing = {
  id: string; isNew: boolean; revision: number | null; name: string; nameIn: [string, string][]; takes: string[]; work: string;
  place: string; placeId: string; drafts: Draft[]; policy?: PolicyDraft | null;
  /** An appointed role's acts, holder and window, which the editor sets in place of its choices. */
  hold?: HoldDraft;
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
  /** *Use the default*: a seeded role given its seed over `SET_ROLE`'s `default`, record and choices both. */
  mayClear: boolean;
  /** The driver's sentence for a refused press. */
  refused: string | null;
  /** The role's next new start as its saved policy would take it (a probe); null or absent, none is said. */
  next?: RoleNext | null;
  /** The policy fields this build enforces (`ROLES`' `policyFields`); absent, every move the editor knows. */
  fields?: readonly string[];
  onSave: (off: boolean) => void;
  onClear: () => void;
  onCancel: () => void;
};

const MOVE_FIELDS = ['moves.limit', 'moves.transient', 'moves.failed', 'moves.quiet'];

/**
 * **The role editor** (SETUPUX1l, the Setup design §2.4): a role's choices of agent, model and effort first, each model and effort from what
 * the agent offers or typed after *other…*; *Edit role*, folded, holds its names, work and place; the prose of how choices run stands
 * behind one ⓘ and one sentence of consequence before *Save*. A role that takes several works names them and changes none.
 *
 * A molecule shared by All roles, a place's Roles and, later, a step's: it reads no bridge, and every press goes out.
 */
export function RoleEditor({
  editing: now, onEditing, title, agents, offers, works, busy, held, placed, consequence, conversing, mayOff, isOff, mayClear, refused, next, fields = MOVE_FIELDS,
  onSave, onClear, onCancel,
}: RoleEditorProps) {
  const { t } = useTranslation();
  const set = (patch: Partial<Editing>) => onEditing((was) => ({ ...was, ...patch }));
  const change = (key: number, field: 'agent' | 'model' | 'effort', value: string) =>
    onEditing((was) => ({ ...was, drafts: was.drafts.map((draft) => (draft.key === key ? { ...draft, [field]: value } : draft)) }));
  const policy = now.policy ?? null;
  const rule = policy && policy.preset !== 'first' ? 'weight' : 'order';
  const roleWorks = now.takes.length > 0 ? now.takes : [now.work];
  const conversation = conversational(roleWorks);
  const policyLocked = busy || held;
  const patchDraft = (key: number, patch: Partial<PolicyFields>) =>
    onEditing((was) => ({ ...was, drafts: was.drafts.map((draft) => (draft.key === key ? { ...draft, ...patch } : draft)) }));
  const patchPolicy = (patch: (was: PolicyDraft) => Partial<PolicyDraft>) => onEditing((was) => (was.policy ? { ...was, policy: { ...was.policy, ...patch(was.policy) } } : was));
  const weigh = (key: number, weight: string) => onEditing((was) => ({
    ...was, drafts: was.drafts.map((draft) => (draft.key === key ? { ...draft, weight } : draft)),
    policy: was.policy && was.policy.preset === 'even' ? { ...was.policy, preset: 'weighted' } : was.policy,
  }));
  const setMove = (condition: StaffCondition, move: PolicyMove | null) => patchPolicy((was) => {
    const moves = { ...was.moves };
    if (move) moves[condition] = move;
    else delete moves[condition];
    return { moves };
  });
  const move = (from: number, to: number) => onEditing((was) => {
    if (to < 0 || to >= was.drafts.length) return was;
    const moved = [...was.drafts];
    [moved[from], moved[to]] = [moved[to]!, moved[from]!];
    return { ...was, drafts: moved };
  });
  const add = () => onEditing((was) => ({
    ...was, drafts: [...was.drafts, { key: Math.max(0, ...was.drafts.map((draft) => draft.key)) + 1, agent: agents[0]?.value ?? '', model: '', effort: '', weight: '1' }],
  }));
  const locked = busy || held;
  const shared = now.takes.length > 1;
  const labelOf = (work: string) => works.find((each) => each.value === work)?.label ?? work;
  const moving = policy !== null && Object.keys({ ...policy.moves, ...policy.carried }).length > 0;
  const how = [
    t(rule === 'weight' ? 'settings.ai.policy.later' : 'settings.ai.role.later'), t('settings.ai.role.outranks'),
    t(moving ? 'settings.ai.policy.movesNote' : conversing ? 'settings.ai.role.goesOn' : 'settings.ai.role.neverMoves'),
    t(rule === 'weight' ? 'settings.ai.policy.choicesNote' : 'settings.ai.roles.choicesNote'), t('settings.ai.role.most', { count: MOST }),
    ...(policy && rule === 'order' ? [t('settings.ai.policy.lighter')] : []),
  ].join(' ');
  const columns: readonly ('name' | 'short' | 'number' | 'long' | 'acts')[] = policy
    ? rule === 'weight' ? ['name', 'name', 'short', 'number', 'short', 'long', 'acts'] : ['name', 'name', 'short', 'long', 'acts']
    : ['name', 'name', 'short', 'acts'];

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
      {policy?.state === 'stale' && <p role="status" className="m-0 text-small text-ink-soft">{t('settings.ai.policy.stale')}</p>}
      {policy && (
        <HowItChooses
          preset={policy.preset} disabled={policyLocked}
          onPreset={(preset: Preset) => onEditing((was) => withPreset(was, preset, conversation))}
        />
      )}
      {now.hold && <HoldFields hold={now.hold} disabled={locked} onChange={(hold) => set({ hold })} />}
      {!now.hold && <FormGrid columns={columns} floor="40rem">
        <FormRow head>
          <span className="text-meta text-ink-faint">{t('settings.ai.role.field.agent')}</span>
          <span className="text-meta text-ink-faint">{t('settings.ai.role.field.model')}</span>
          <span className="text-meta text-ink-faint">{t('settings.ai.role.field.effort')}</span>
          {policy && rule === 'weight' && <span className="text-meta text-ink-faint">{t('settings.ai.policy.field.weight')}</span>}
          {policy && rule === 'weight' && <span className="text-meta text-ink-faint">{t('settings.ai.policy.field.aim')}</span>}
          {policy && <span className="text-meta text-ink-faint">{t('settings.ai.policy.field.limits')}</span>}
          <span />
        </FormRow>
        {now.drafts.map((draft, index) => {
          const n = index + 1;
          const aim = aimOf(now.drafts.map((each) => each.weight), index);
          // What is in effect is shown as itself: a name typed at a terminal stays named.
          const options = draft.agent && !agents.some((agent) => agent.value === draft.agent) ? [...agents, { value: draft.agent, label: draft.agent }] : agents;
          return (
            <FormRow key={draft.key}>
              <FormField label={t('settings.ai.role.field.agent')} when="stacked">
                <SelectField value={draft.agent} onChange={(value) => change(draft.key, 'agent', value)} options={[...options]} ariaLabel={t('settings.ai.role.agentOf', { n })} />
              </FormField>
              <FormField label={t('settings.ai.role.field.model')} when="stacked">
                <OfferedField
                  key={draft.agent}
                  value={draft.model}
                  offered={offers[draft.agent]?.models}
                  label={t('settings.ai.role.modelOf', { n })}
                  otherLabel={t('settings.ai.roles.offers.modelOther', { n })}
                  onChange={(value) => change(draft.key, 'model', value)}
                />
              </FormField>
              <FormField label={t('settings.ai.role.field.effort')} when="stacked">
                <OfferedField
                  key={draft.agent}
                  value={draft.effort}
                  offered={offers[draft.agent]?.efforts}
                  label={t('settings.ai.role.effortOf', { n })}
                  otherLabel={t('settings.ai.roles.offers.effortOther', { n })}
                  onChange={(value) => change(draft.key, 'effort', value)}
                />
              </FormField>
              {policy && rule === 'weight' && (
                <FormField label={t('settings.ai.policy.field.weight')} when="stacked">
                  <WeightField n={n} value={draft.weight ?? '1'} onChange={(weight) => weigh(draft.key, weight)} />
                </FormField>
              )}
              {policy && rule === 'weight' && (
                <FormField label={t('settings.ai.policy.field.aim')} when="stacked">
                  <span className="text-small text-ink-soft">{aim ? t('settings.ai.policy.aim', { w: aim[0], of: aim[1] }) : t('settings.ai.policy.aimNone')}</span>
                </FormField>
              )}
              {policy && (
                <div className="grid min-w-0 gap-1">
                  <span className="hidden text-small text-ink-faint @max-[40rem]:block">{t('settings.ai.policy.field.limits')}</span>
                  <LimitsFold n={n} fields={draft} takes={roleWorks} labelOf={labelOf} disabled={policyLocked} onChange={(patch) => patchDraft(draft.key, patch)} />
                </div>
              )}
              <span className="flex items-center">
                <Button variant="ghost" disabled={busy || index === 0} aria-label={t('settings.ai.role.up', { n })} onClick={() => move(index, index - 1)}>
                  <Icon name="chevronUp" size={12} />
                </Button>
                <Button
                  variant="ghost"
                  disabled={busy || index === now.drafts.length - 1}
                  aria-label={t('settings.ai.role.down', { n })}
                  onClick={() => move(index, index + 1)}
                >
                  <Icon name="chevronDown" size={12} />
                </Button>
                <Button
                  variant="ghost"
                  disabled={busy || now.drafts.length === 1}
                  aria-label={t('settings.ai.role.remove', { n })}
                  onClick={() => onEditing((was) => ({ ...was, drafts: was.drafts.filter((each) => each.key !== draft.key) }))}
                >
                  <Icon name="x" size={12} />
                </Button>
              </span>
            </FormRow>
          );
        })}
      </FormGrid>}
      {!now.hold && (
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="ghost" disabled={busy || now.drafts.length >= MOST} onClick={add}>
            <Icon name="plus" size={12} />
            {t('settings.ai.role.add')}
          </Button>
          <span className="inline-flex items-center gap-1 text-small text-ink-soft">
            <WhyGlyph why={how} />
            {t('settings.ai.roles.editor.how')}
          </span>
        </div>
      )}
      {policy && (
        <MovesFold
          policy={policy} rule={rule} fields={fields} conversation={conversation} disabled={policyLocked}
          onMove={setMove} onMost={(most) => patchPolicy(() => ({ most }))}
        />
      )}
      {policy && next && (
        <p className="m-0 text-small text-ink-soft">
          {next.by === 'first' ? t('settings.ai.policy.nextPlain', { choice: next.choice }) : t('settings.ai.policy.next', { choice: next.choice, said: next.said })}
        </p>
      )}
      {policy && <p className="m-0 text-meta text-ink-faint">{t('settings.ai.policy.older')}</p>}
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
          {now.hold ? null : shared ? (
            <p className="m-0 text-small text-ink-soft">
              {t('settings.ai.roles.field.takesAll', { works: now.takes.map(labelOf).join(t('settings.ai.role.and')) })}
            </p>
          ) : (
            <FormField label={t('settings.ai.roles.field.work')}>
              <SelectField value={now.work} onChange={(value) => set({ work: value })} options={[...works]} ariaLabel={t('settings.ai.roles.field.work')} />
            </FormField>
          )}
        </FormGrid>
      </details>
      <p className="m-0 text-small text-ink-soft">{consequence}</p>
      <div className="flex flex-wrap gap-2">
        <Button type="submit" disabled={locked || (!!now.hold && !holdReads(now.hold))}>{t('settings.ai.role.save')}</Button>
        {mayOff && !isOff && !now.isNew && (
          <Button variant="ghost" disabled={locked} onClick={() => onSave(true)}>{t('settings.ai.role.off')}</Button>
        )}
        {mayClear && <Button variant="ghost" disabled={locked} onClick={onClear}>{t('settings.ai.roles.useDefault')}</Button>}
        <Button variant="ghost" disabled={busy} onClick={onCancel}>{t('common.cancel')}</Button>
      </div>
      {mayClear && <p className="m-0 text-small text-ink-soft">{t('settings.ai.roles.useDefaultSays')}</p>}
      {refused && <Refused sentence={refused} />}
    </form>
  );
}

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Prose, SelectField } from '../../design/ui';
import type { EditorRole, GraphProblem, StepStaffing, WorkflowPaletteAnswer } from './answers';
import { choiceLabel, optionLabel, plain, previewPattern, stepAbout, stepTitle, waySaid } from './editWords';
import {
  addWire, askedInputs, carryOf, choiceOf, type DraftNode, type GraphDraft, givesOf, inputsOf, joinModeOf, joinOf, landOptionApplies, moreWaysOf, movePlan,
  optionOf, removeWire, setCarry, setEnd, setJoinMode, setLandMethod, setOption, sourcesOf, tailOf, triggerTargetsOf,
} from './graphDraft';
import { Refusals } from './Refusals';
import { StepOptions } from './StepOptions';
import type { RolePlace } from './StepRole';
import { type WaysActs, WaysOnTable } from './WaysOnTable';

// The inspector (FLOWCHART2b; the flowchart design §2, §3, §8.2; D221 §2.3): the selected step, beside the chart where the editor's region
// holds both and in a drawer where it does not. Its plugin's words, its refusals, *What happens next*, what it *Carries*, its options and
// role, *More ways* (a trigger, how its ways in join, and any way it holds that no row draws, kept as written), its id under *Details*, and
// its actions: a step after, a move within its chain and a removal each confirmed in place with what they rewire said first.

/** What the inspector confirms in place before it acts: a removal, or a move earlier or later. */
export type Confirming = 'remove' | 'earlier' | 'later' | null;

/** What the inspector asks of the page beyond a row's ways: a change made, a step after, a move, a copy, a duplicate, a removal. */
export type InspectorActs = WaysActs & {
  onChange: (draft: GraphDraft, said: string) => void;
  onAfter: () => void;
  onMove: (dir: 'earlier' | 'later') => void;
  onDuplicate: () => void;
  onCopy: () => void;
  onRemove: (options: { join: boolean; end?: string }) => void;
};

const NONE = ':none';
const ACT = 'min-h-0 border border-line py-0.5 text-small';
const LAND = 'land';
const METHOD = 'method';
const text = (value: unknown): string | null => (typeof value === 'string' && value !== '' ? value : null);

export function Inspector({ palette, draft, roles, place, staffing, lines, node, titleOf, problems, confirming, onConfirm, acts }: {
  palette: WorkflowPaletteAnswer;
  draft: GraphDraft;
  roles: EditorRole[];
  /** The place the editor opened from, which a step's role is given and named for, and the role editor's reads and presses (SETUPUX1t). */
  place?: RolePlace | null;
  staffing?: StepStaffing;
  /** The lines the place declares, which a landing's target is chosen from. */
  lines?: string[];
  node: DraftNode;
  titleOf: (id: string) => string;
  problems: { atStep: GraphProblem[]; atWire: (wire: string) => GraphProblem[] };
  confirming: Confirming;
  onConfirm: (confirm: Confirming) => void;
  acts: InspectorActs;
}) {
  const { t, i18n } = useTranslation();
  const id = node.id;
  const title = titleOf(id);
  const choice = choiceOf(palette, node.kind);
  const about = stepAbout(i18n, palette, node.kind);
  const earlier = movePlan(draft, id, 'earlier');
  const later = movePlan(draft, id, 'later');

  // A landing shows the options its method means (one it holds is shown wherever it is, for the judge to say why), and says under its
  // method the tail that method brings and under its pattern the branch it names.
  const method = node.kind === LAND ? text(optionOf(node, METHOD)) : null;
  const options = (choice?.options ?? []).filter((field) => node.kind !== LAND || landOptionApplies(method, field.name) || optionOf(node, field.name) !== undefined);
  const notes: Record<string, string> = {};
  if (node.kind === LAND && method !== null) {
    const tail = tailOf(method).map((kind) => stepTitle(t, i18n, palette, { kind }));
    notes[METHOD] = tail.length > 0 ? t('workflow.edit.option.land.method.tail', { steps: tail.join(t('workflow.listJoin')) }) : t('workflow.edit.option.land.method.noTail');
  }
  const pattern = text(optionOf(node, 'pattern'));
  if (node.kind === LAND && pattern) {
    const shown = previewPattern(pattern);
    notes.pattern = shown.unknown.length > 0
      ? t('workflow.edit.option.land.pattern.unknown', { names: shown.unknown.join(t('workflow.listJoin')) })
      : t('workflow.edit.option.land.pattern.preview', { name: shown.name });
  }

  return (
    <div className="grid min-w-0 gap-4">
      {(choice?.plugin || about) && (
        <div className="grid min-w-0 gap-1">
          {choice?.plugin && <p className="m-0 text-meta text-ink-soft">{t('workflow.edit.inspector.by', { plugin: choice.plugin })}</p>}
          {about && <Prose className="text-small">{about}</Prose>}
        </div>
      )}
      <Refusals problems={problems.atStep} />

      <WaysOnTable palette={palette} draft={draft} step={id} titleOf={titleOf} problemsAt={problems.atWire} acts={acts} />
      <Carries draft={draft} node={node} titleOf={titleOf} onChange={acts.onChange} />

      {options.length > 0 && choice && (
        <StepOptions node={node} fields={options} roleTakes={choice.roleTakes} roles={roles} place={place} staffing={staffing} lines={lines} notes={notes}
          onSet={(option, value) => {
            if (node.kind === LAND && option === METHOD && typeof value === 'string') {
              const made = setLandMethod(palette, draft, id, value);
              const steps = (kinds: string[]) => kinds.map((kind) => stepTitle(t, i18n, palette, { kind })).join(t('workflow.listJoin'));
              const said = made.added.length > 0 && made.removed.length > 0 ? 'methodBoth' : made.added.length > 0 ? 'methodAdded' : made.removed.length > 0 ? 'methodRemoved' : 'method';
              acts.onChange(made.draft, t(`workflow.edit.said.${said}`, {
                step: title, method: choiceLabel(t, LAND, METHOD, value), added: steps(made.added), removed: steps(made.removed),
              }));
              return;
            }
            acts.onChange(setOption(draft, id, option, value), t('workflow.edit.said.option', { step: title, option: optionLabel(t, node.kind, option) }));
          }} />
      )}

      <MoreWays palette={palette} draft={draft} node={node} titleOf={titleOf} onChange={acts.onChange} />

      <details className="min-w-0 text-small text-ink-soft">
        <summary className="cursor-pointer">{t('workflow.edit.inspector.details')}</summary>
        <p className="m-0 mt-1"><code>{id}</code> · <code>{node.kind}</code>{typeof node.node.version === 'number' ? ` v${node.node.version}` : ''}</p>
        {options.length > 0 && (
          <p className="m-0 mt-1 [overflow-wrap:anywhere]">
            {t('workflow.edit.inspector.keys')}{' '}
            {options.map((field, at) => <span key={field.name}>{at > 0 && ', '}<code>{field.name}</code></span>)}
          </p>
        )}
      </details>

      <div className="flex min-w-0 flex-wrap gap-1.5">
        <Button variant="ghost" className={ACT} onClick={acts.onAfter}>{t('workflow.edit.act.after')}</Button>
        {earlier && <Button variant="ghost" className={ACT} onClick={() => onConfirm('earlier')}>{t('workflow.edit.act.earlier')}</Button>}
        {later && <Button variant="ghost" className={ACT} onClick={() => onConfirm('later')}>{t('workflow.edit.act.later')}</Button>}
        <Button variant="ghost" className={ACT} onClick={acts.onDuplicate}>{t('workflow.edit.act.duplicate')}</Button>
        <Button variant="ghost" className={ACT} onClick={acts.onCopy}>{t('workflow.edit.act.copy')}</Button>
        {draft.end !== id && (
          <Button variant="ghost" className={ACT} onClick={() => acts.onChange(setEnd(draft, id), t('workflow.edit.said.end', { step: title }))}>
            {t('workflow.edit.act.end')}
          </Button>
        )}
        <Button variant="ghost" className={ACT} onClick={() => onConfirm('remove')}>{t('workflow.edit.act.remove')}</Button>
      </div>

      {(confirming === 'earlier' || confirming === 'later') && (
        <MoveConfirm plan={confirming === 'earlier' ? earlier : later} titleOf={titleOf}
          onMove={() => { acts.onMove(confirming); onConfirm(null); }} onCancel={() => onConfirm(null)} />
      )}
      {confirming === 'remove' && (
        <RemoveConfirm palette={palette} draft={draft} node={node} titleOf={titleOf} onRemove={acts.onRemove} onCancel={() => onConfirm(null)} />
      )}
    </div>
  );
}

/** A move's preview: the steps around it in their new order, applied by *Move*. */
function MoveConfirm({ plan, titleOf, onMove, onCancel }: {
  plan: ReturnType<typeof movePlan>;
  titleOf: (id: string) => string;
  onMove: () => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  if (!plan) return <p className="m-0 text-small text-ink-soft">{t('workflow.edit.move.cannot')}</p>;
  const chain = [plan.before, plan.first, plan.second, plan.after].filter((each): each is string => each !== null).map(titleOf)
    .join(t('workflow.edit.move.then'));
  return (
    <div className="grid min-w-0 gap-2 rounded-card border border-line p-3">
      <p className="m-0 text-small text-ink">{t('workflow.edit.move.says', { first: titleOf(plan.first), second: titleOf(plan.second), chain })}</p>
      <div className="flex flex-wrap gap-1.5">
        <Button autoFocus onClick={onMove}>{t('workflow.edit.move.apply')}</Button>
        <Button variant="ghost" className="border border-line" onClick={onCancel}>{t('common.cancel')}</Button>
      </div>
    </div>
  );
}

/**
 * A removal confirmed in place (§8.2): the step's ways go with it and nothing is bridged, unless its one way in and one way on are plain
 * links, when *Remove and join* names the join; removing the end asks where the work is done instead.
 */
function RemoveConfirm({ palette, draft, node, titleOf, onRemove, onCancel }: {
  palette: WorkflowPaletteAnswer;
  draft: GraphDraft;
  node: DraftNode;
  titleOf: (id: string) => string;
  onRemove: (options: { join: boolean; end?: string }) => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const join = joinOf(draft, node.id);
  const ending = draft.end === node.id;
  const mayEnd = draft.nodes.filter((each) => each.id !== node.id && (choiceOf(palette, each.kind)?.mayEnd ?? true));
  const [end, setEndAt] = useState(() => (join && mayEnd.some((each) => each.id === join.from) ? join.from : NONE));
  const endAt = ending ? { end: end === NONE ? '' : end } : {};
  return (
    <div className="grid min-w-0 gap-2 rounded-card border border-line p-3">
      <p className="m-0 text-small text-ink">{t('workflow.edit.remove.says', { step: titleOf(node.id) })}</p>
      {ending && (
        <div className="grid min-w-0 gap-1">
          <span className="text-meta text-ink-soft">{t('workflow.edit.remove.end')}</span>
          <SelectField ariaLabel={t('workflow.edit.remove.end')} value={end} onChange={setEndAt}
            options={[{ value: NONE, label: t('workflow.edit.remove.endNone') }, ...mayEnd.map((each) => ({ value: each.id, label: titleOf(each.id) }))]} />
        </div>
      )}
      <div className="flex flex-wrap gap-1.5">
        {join && (
          <Button onClick={() => onRemove({ join: true, ...endAt })}>
            {t('workflow.edit.remove.join', { from: titleOf(join.from), to: titleOf(join.to) })}
          </Button>
        )}
        <Button variant={join ? 'ghost' : 'primary'} className={join ? 'border border-line' : undefined} onClick={() => onRemove({ join: false, ...endAt })}>
          {t('workflow.edit.remove.only')}
        </Button>
        <Button variant="ghost" className="border border-line" onClick={onCancel}>{t('common.cancel')}</Button>
      </div>
    </div>
  );
}

/** What a step carries (§8.7): each input fed from a step's output of its schema, and what it gives others. */
function Carries({ draft, node, titleOf, onChange }: {
  draft: GraphDraft;
  node: DraftNode;
  titleOf: (id: string) => string;
  onChange: (draft: GraphDraft, said: string) => void;
}) {
  const { t } = useTranslation();
  const inputs = inputsOf(node);
  const gives = givesOf(draft, node.id);
  const asking = new Set(askedInputs(draft, node.id).map((each) => each.input));
  if (inputs.length === 0 && gives.length === 0) return null;
  const title = titleOf(node.id);
  return (
    <section className="grid min-w-0 gap-2">
      <h4 className="m-0 text-small font-semibold text-ink">{t('workflow.edit.carries.title')}</h4>
      {inputs.map((input) => {
        const sources = sourcesOf(draft, node.id, input);
        const fed = carryOf(draft, node.id, input);
        const valueOf = (from: string, port: string) => JSON.stringify([from, port]);
        const label = (from: string, port: string) => t('workflow.edit.carries.from', { step: titleOf(from), output: port });
        const kept = fed && fed.from !== null && !sources.some((each) => each.from === fed.from && each.port === fed.fromPort)
          ? [{ value: valueOf(fed.from, fed.fromPort ?? ''), label: label(fed.from, fed.fromPort ?? '') }] : [];
        return (
          <div key={input} className="grid min-w-0 gap-1">
            <span className="text-meta text-ink-soft">{t('workflow.edit.carries.input', { input })}</span>
            <SelectField
              ariaLabel={t('workflow.edit.carries.label', { step: title, input })}
              value={fed && fed.from !== null ? valueOf(fed.from, fed.fromPort ?? '') : NONE}
              onChange={(value) => {
                if (value === NONE) {
                  onChange(setCarry(draft, node.id, input, null), t('workflow.edit.said.uncarried', { step: title, input }));
                  return;
                }
                const [from, port] = JSON.parse(value) as [string, string];
                onChange(setCarry(draft, node.id, input, { from, port }), t('workflow.edit.said.carried', { step: title, input, from: titleOf(from), output: port }));
              }}
              options={[
                { value: NONE, label: t('workflow.edit.carries.nothing') }, ...kept,
                ...sources.map((source) => ({ value: valueOf(source.from, source.port), label: label(source.from, source.port) })),
              ]}
            />
            {sources.length === 0 && <p className="m-0 text-meta text-ink-faint">{t('workflow.edit.carries.none')}</p>}
            {asking.has(input) && <p className="m-0 text-meta text-ink">{t('workflow.edit.carries.asks')}</p>}
          </div>
        );
      })}
      {gives.length > 0 && (
        <ul className="m-0 grid list-none gap-0.5 p-0 text-small text-ink-soft">
          {gives.map((wire) => (
            <li key={wire.id}>{t('workflow.edit.carries.gives', { output: wire.fromPort ?? '', step: titleOf(wire.to), input: wire.toPort ?? '' })}</li>
          ))}
        </ul>
      )}
    </section>
  );
}

/**
 * *More ways* (§8.1): what `WayForm` made that no row draws, so its removal loses none: a trigger, how the step's ways in join, and any way
 * it holds that no row draws (a kind recorded as written), each removable alone.
 */
function MoreWays({ palette, draft, node, titleOf, onChange }: {
  palette: WorkflowPaletteAnswer;
  draft: GraphDraft;
  node: DraftNode;
  titleOf: (id: string) => string;
  onChange: (draft: GraphDraft, said: string) => void;
}) {
  const { t } = useTranslation();
  const title = titleOf(node.id);
  const more = moreWaysOf(palette, draft, node.id);
  const triggers = triggerTargetsOf(draft, node.id);
  const join = joinModeOf(draft, node.id);
  return (
    <details className="min-w-0">
      <summary className="cursor-pointer text-small font-semibold text-ink">{t('workflow.edit.more.title')}</summary>
      <div className="mt-2 grid min-w-0 gap-2">
        {more.length === 0 && <p className="m-0 text-small text-ink-soft">{t('workflow.edit.more.none')}</p>}
        {more.map((wire) => {
          const way = plain(waySaid(t, wire, titleOf(wire.to)));
          return (
            <div key={wire.id} className="flex min-w-0 flex-wrap items-center justify-between gap-1.5">
              <span className="min-w-0 text-small text-ink [overflow-wrap:anywhere]">{way}</span>
              <Button variant="ghost" className={ACT} aria-label={t('workflow.edit.way.removeNamed', { from: title, way })}
                onClick={() => onChange(removeWire(draft, wire.id), t('workflow.edit.said.wayGone', { from: title, way }))}>
                {t('workflow.edit.way.remove')}
              </Button>
            </div>
          );
        })}
        {triggers.length > 0 && (
          <SelectField
            ariaLabel={t('workflow.edit.more.triggerLabel', { step: title })}
            value=""
            placeholder={t('workflow.edit.more.trigger')}
            onChange={(to) => onChange(addWire(draft, { kind: 'trigger', from: node.id, to }),
              t('workflow.edit.said.trigger', { from: title, to: titleOf(to) }))}
            options={triggers.map((each) => ({ value: each, label: titleOf(each) }))}
          />
        )}
        {join === 'recorded' && <p className="m-0 text-small text-ink-soft">{t('workflow.edit.more.joinRecorded')}</p>}
        {(join === 'all' || join === 'any') && (
          <div className="grid min-w-0 gap-1">
            <span className="text-meta text-ink-soft">{t('workflow.edit.more.join')}</span>
            <SelectField
              ariaLabel={t('workflow.edit.more.join')}
              value={join}
              onChange={(mode) => onChange(setJoinMode(draft, node.id, mode === 'any' ? 'any' : 'all'),
                t(mode === 'any' ? 'workflow.edit.said.joinAny' : 'workflow.edit.said.joinAll', { step: title }))}
              options={[{ value: 'all', label: t('workflow.edit.more.joinAll') }, { value: 'any', label: t('workflow.edit.more.joinAny') }]}
            />
          </div>
        )}
      </div>
    </details>
  );
}

import { type KeyboardEvent, useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, Pill, SkeletonRows } from '../../design/ui';
import { cn } from '../../lib/cn';
import { Refused } from '../../work/InlineConfirm';
import type {
  PresetWorkflow, SavedWorkflow, WorkflowListAnswer, WorkflowSaveAnswer, WorkflowSaveAsk, WorkflowShownAnswer, WorkflowUseAnswer, WorkflowUseAsk,
} from '../edit/answers';
import { presetFor, presetName } from '../edit/presetWords';
import type { CurrentWorkflow, WorkflowEffective } from '../current';
import { ChangePlan, type PlanState, planToken } from './ChangePlan';
import { EffectiveChart } from './EffectiveChart';
import { routeWords } from './effective';

/** The workflow a preset starts a draft from: its graph as a version holds it, and the name it is offered under. */
export type PresetStart = { graph: unknown; name: string };

/** The page's sender for the plan of a choice (`WORKFLOW_USE {plan: true}`), which writes nothing; none, and no plan is read. */
export type WorkflowPlanBridge = { onPlan?: (ask: WorkflowUseAsk) => Promise<WorkflowUseAnswer> };

/** The route the choice is for: its key as `WORKFLOW_CURRENT` takes it, its kind and the kind as the person named it, and whether the place chose it itself. */
export type ChangeRoute = { key: string; kind: string | null; label: string | null; own: boolean };

/** The id `WORKFLOW_USE` takes for the workflow Daoris derives from the rules, which no file holds. */
const CURRENT = 'current';

const TABS = ['presets', 'saved', 'current'] as const;
type Tab = (typeof TABS)[number];

const nameOf = (saved: SavedWorkflow) => saved.name ?? saved.id;

/** An id made from a copy's name, unique among the saved ones; a name with no letter or digit takes the id it was copied from. */
export function copyIdFor(name: string, from: string, taken: readonly string[]): string {
  const base = name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || from || 'workflow';
  let id = base;
  for (let n = 2; taken.includes(id); n++) id = `${base}-${n}`;
  return id;
}

/** The key of what is selected: a preset, a saved workflow, Current, or the clearing of the place's own choice. */
const keyOf = {
  preset: (id: string) => `preset:${id}`,
  saved: (id: string) => `saved:${id}`,
  current: CURRENT,
  clear: 'clear',
} as const;

/** The three tabs, a tablist the arrow keys move along, the chosen tab the one tab stop. */
function Tabs({ tab, onTab, label, ids }: { tab: Tab; onTab: (tab: Tab) => void; label: string; ids: (tab: Tab) => { tab: string; panel: string } }) {
  const { t } = useTranslation();
  const buttons = useRef<(HTMLButtonElement | null)[]>([]);
  const move = (event: KeyboardEvent) => {
    const at = TABS.indexOf(tab);
    const to = event.key === 'ArrowRight' ? (at + 1) % TABS.length
      : event.key === 'ArrowLeft' ? (at + TABS.length - 1) % TABS.length
        : event.key === 'Home' ? 0 : event.key === 'End' ? TABS.length - 1 : -1;
    if (to < 0) return;
    event.preventDefault();
    onTab(TABS[to]!);
    buttons.current[to]?.focus();
  };
  return (
    <div role="tablist" aria-label={label} onKeyDown={move} className="flex min-w-0 flex-wrap gap-0.5 border-b border-line">
      {TABS.map((each, index) => (
        <button
          key={each}
          ref={(node) => { buttons.current[index] = node; }}
          type="button"
          role="tab"
          id={ids(each).tab}
          aria-selected={each === tab}
          aria-controls={ids(each).panel}
          tabIndex={each === tab ? 0 : -1}
          onClick={() => onTab(each)}
          className={cn(
            '-mb-px border-b-2 px-2.5 py-1 text-small',
            each === tab ? 'border-accent font-semibold text-ink' : 'border-transparent text-ink-soft hover:text-ink',
          )}
        >
          {t(`workflow.place.change.${each}`)}
        </button>
      ))}
    </div>
  );
}

/** A copy's name, asked for alone: the id comes from it. */
function CopyForm({ initial, busy, onSave, onCancel }: { initial: string; busy: boolean; onSave: (name: string) => void; onCancel: () => void }) {
  const { t } = useTranslation();
  const [name, setName] = useState(initial);
  const field = useId();
  return (
    <form
      className="flex min-w-0 flex-wrap items-end gap-x-2 gap-y-1.5"
      onSubmit={(event) => { event.preventDefault(); if (name.trim()) onSave(name.trim()); }}
    >
      <label htmlFor={field} className="grid min-w-0 flex-1 basis-48 gap-0.5 text-small text-ink">
        {t('workflow.place.change.copy.name')}
        <input
          id={field}
          value={name}
          onChange={(event) => setName(event.target.value)}
          className="min-w-0 rounded-control border border-line-strong bg-surface px-2 py-1 text-body text-ink"
        />
      </label>
      <Button type="submit" variant="ghost" className="min-h-0 border border-line px-2 py-0.5 text-small" disabled={busy || !name.trim()}>
        {t('workflow.place.change.copy.save')}
      </Button>
      <Button variant="ghost" className="min-h-0 px-2 py-0.5 text-small" onClick={onCancel}>{t('common.cancel')}</Button>
    </form>
  );
}

const small = 'min-h-0 border border-line px-2 py-0.5 text-small';

/**
 * **Change…** (AGENTSPLACE1h, SETUPUX1p; the Setup design's §2.3): what one route of a place follows, chosen from three tabs: the presets
 * the driver ships, the workflows saved here, and *Current*, live and derived. Selecting a candidate draws it through the place's own chart
 * and writes nothing; where `WORKFLOW_LIST` announces `use: ['plan']`, it also reads the plan of choosing it, and *Use here* carries the
 * plan's `expected` token, so a choice whose plan moved is refused `moved` in the driver's words. Unannounced, *Use here* is today's press.
 * A preset is stored by *Save a copy…* (a name alone) before it is used. *Use the workspace's* clears the place's own choice for the route,
 * after a plan of its own that says what the kinds keep.
 *
 * @remarks
 * The page derives nothing: the lowering, the places, what the kinds keep and every refusal are the driver's. A saved version is chosen at
 * the newest the person read (`stale` once it moved).
 *
 * A molecule: the lists arrive as props, and the plan, the save and the choice go out.
 */
export function ChangeWorkflow({
  list, error, current, place, route, effective, shown, onShow, using = false, saving = false, onUse, onPlan, onSave, onStartFrom, onClose,
}: {
  /** The saved workflows and the presets; null while read. */
  list: WorkflowListAnswer | null;
  /** The read was refused. */
  error?: unknown;
  /** The place's Current, for its version. */
  current: CurrentWorkflow;
  /** The place the choice is for. */
  place: { repository: string } | { workspace: string };
  /** The route the choice is for. */
  route: ChangeRoute;
  /** What the route follows now, once answered: Current's drawing is only this one's. */
  effective: WorkflowEffective | null;
  /** The saved version last asked for by `onShow`, once it is answered. */
  shown: WorkflowShownAnswer | null;
  onShow: (id: string | null) => void;
  using?: boolean;
  saving?: boolean;
  onUse: (ask: WorkflowUseAsk) => Promise<WorkflowUseAnswer>;
  onPlan?: WorkflowPlanBridge['onPlan'];
  onSave: (ask: WorkflowSaveAsk) => Promise<WorkflowSaveAnswer>;
  /** Open the step editor from a preset's steps. */
  onStartFrom: (start: PresetStart) => void;
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const headId = useId();
  const tabsId = useId();
  const [picked, setPicked] = useState<Tab | null>(null);
  const [selected, setSelected] = useState<string | null>(null);
  const [copying, setCopying] = useState(false);
  const [said, setSaid] = useState<string | null>(null);
  const [chosen, setChosen] = useState(false);
  const [plan, setPlan] = useState<PlanState | null>(null);
  const [again, setAgain] = useState(0);
  const asked = useRef(0);
  const sender = useRef(onPlan);
  sender.current = onPlan;
  const hideShown = useRef(onShow);
  hideShown.current = onShow;

  const here = 'repository' in place ? place.repository : place.workspace;
  const level = 'repository' in place ? 'repository' : 'workspace';
  const at: WorkflowUseAsk = { ...place, ...(route.kind === null ? {} : { kind: route.kind }) };
  const saved = list?.workflows ?? [];
  const presets = list?.presets ?? [];

  const followed: Tab = effective && effective.workflow !== CURRENT && saved.some((each) => each.id === effective.workflow) ? 'saved' : 'current';
  const tab = picked ?? followed;
  const ids = (each: Tab) => ({ tab: `${tabsId}-tab-${each}`, panel: `${tabsId}-panel-${each}` });

  // A saved version read for the preview stops being read once Change… is left.
  useEffect(() => () => hideShown.current(null), []);

  const chosenOnRoute = (workflow: SavedWorkflow) => workflow.chosen.some((each) =>
    each.level === level && each.name === here && (route.kind === null ? each.role === 'default' : each.role === 'kind' && each.kind === route.kind));
  const chosenAt = (workflow: SavedWorkflow) =>
    workflow.chosen.filter((each) => !(each.level === level && each.name === here)).map((each) => each.name);

  // What selecting asks of the driver: the choice as it would be pressed, or none for a preset, which is stored by a copy.
  const selectedSaved = selected?.startsWith('saved:') ? saved.find((each) => keyOf.saved(each.id) === selected) : undefined;
  const newest = selectedSaved?.versions.at(-1);
  const ask: WorkflowUseAsk | null =
    selected === keyOf.current ? { ...at, workflow: CURRENT }
      : selected === keyOf.clear ? at
        : selectedSaved && newest !== undefined && selectedSaved.problem === null ? { ...at, workflow: selectedSaved.id, version: newest }
          : null;
  const askKey = ask === null ? '' : JSON.stringify(ask);
  const announced = list?.use?.includes('plan') === true && onPlan !== undefined;

  // The plan is read once per selection, and again after a refusal that says it moved or a choice that landed.
  useEffect(() => {
    const mine = ++asked.current;
    const send = sender.current;
    if (!announced || ask === null || !send) { setPlan(null); return undefined; }
    setPlan({ key: askKey, state: 'reading' });
    send({ ...ask, plan: true }).then(
      (answer) => { if (mine === asked.current) setPlan({ key: askKey, state: 'read', answer }); },
      (failure) => { if (mine === asked.current) setPlan({ key: askKey, state: 'failed', why: failure instanceof Error ? failure.message : String(failure) }); },
    );
    return () => { asked.current++; };
    // `ask` is its key's content; the sender is read through a ref, since the bridge hands a new one each render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [askKey, announced, again]);

  const token = planToken(plan, askKey);
  const planned = announced && ask !== null;

  const press = async (what: WorkflowUseAsk) => {
    setSaid(null);
    setChosen(false);
    try {
      const answer = await onUse(token ? { ...what, expected: token } : what);
      if (answer.refused) setSaid(answer.message ?? answer.refused);
      else if (answer.done) {
        setChosen(true);
        if (selected === keyOf.clear) setSelected(null);
      }
      if (answer.refused === 'moved' || answer.done) setAgain((was) => was + 1);
    } catch (failure) {
      setSaid(failure instanceof Error ? failure.message : String(failure));
    }
  };

  const select = (key: string, id?: string) => {
    setSelected(key);
    setCopying(false);
    setSaid(null);
    setChosen(false);
    if (id !== undefined) onShow(id);
  };
  const go = (next: Tab) => { setPicked(next); setSelected(null); setCopying(false); setSaid(null); setChosen(false); };

  const copy = async (preset: PresetWorkflow, name: string) => {
    setSaid(null);
    try {
      const answer = await onSave({ id: copyIdFor(name, preset.id, saved.map((each) => each.id)), graph: preset.body, name });
      if (answer.refused || !answer.done) {
        setSaid(answer.message ?? answer.refused ?? '');
        return;
      }
      setCopying(false);
      setPicked('saved');
      select(keyOf.saved(answer.id), answer.id);
    } catch (failure) {
      setSaid(failure instanceof Error ? failure.message : String(failure));
    }
  };

  // What the preview draws: a preset's steps, a saved version once it is read, or Current where the route follows it.
  const drawn = (workflow: string, name: string | null, version: number | null, digest: string | null, graph: unknown): WorkflowEffective => ({
    route: route.key, workflow, name, version, digest, schema: 2,
    source: effective?.source ?? { level: level === 'repository' ? 'repository' : 'workspace', kind: route.kind, label: route.label, paths: [] },
    verifies: false, graph, cells: [], limits: {}, problem: null,
  });
  const chart = (candidate: WorkflowEffective, caption: string) => (
    <EffectiveChart
      effective={candidate}
      caption={caption}
      routeLabel={route.label}
      page={level}
      name={here}
      workspace={current.workspace}
      selected={null}
      onSelect={() => {}}
    />
  );

  const selectedPreset = selected?.startsWith('preset:') ? presets.find((each) => keyOf.preset(each.id) === selected) : undefined;

  const preview = () => {
    if (selectedPreset) {
      const name = presetName(t, i18n, selectedPreset);
      return selectedPreset.graph && selectedPreset.body !== undefined && selectedPreset.body !== null
        ? chart(drawn(selectedPreset.id, name, null, null, selectedPreset.body), t('workflow.place.change.preview', { name }))
        : <p className="m-0 text-small text-ink-soft">{t('workflow.place.change.noGraph')}</p>;
    }
    if (selectedSaved) {
      const read = shown?.id === selectedSaved.id ? shown : null;
      if (!read) return <p className="m-0 text-small text-ink-soft">{t('workflow.place.change.shownReading')}</p>;
      if (read.schema !== 2) return <p className="m-0 text-small text-ink-soft">{t('workflow.place.change.steps')}</p>;
      return chart(
        drawn(read.id, nameOf(selectedSaved), read.version, read.digest, read.body),
        t('workflow.place.change.previewSaved', { name: nameOf(selectedSaved), version: read.version }),
      );
    }
    if (selected === keyOf.current) {
      return effective && effective.workflow === CURRENT && effective.graph
        ? chart(effective, t('workflow.place.change.previewCurrent'))
        : <p className="m-0 text-small text-ink-soft">{t('workflow.place.change.currentNotDrawn')}</p>;
    }
    return null;
  };

  const useHere = (name: string, what: WorkflowUseAsk) => (
    <Button
      variant="ghost"
      className={small}
      disabled={using || saving || (planned && token === null)}
      aria-label={t('workflow.place.change.use', { name })}
      onClick={() => press(what)}
    >
      {t('workflow.edit.use')}
    </Button>
  );

  const candidates = () => {
    if (list === null) return null;
    if (tab === 'presets') {
      return (
        <ul className="m-0 grid list-none gap-1.5 p-0">
          {presets.map((preset) => {
            const name = presetName(t, i18n, preset);
            const purpose = presetFor(t, i18n, preset);
            return (
              <li key={preset.id} className="grid min-w-0 gap-0.5">
                <button
                  type="button"
                  aria-pressed={selected === keyOf.preset(preset.id)}
                  onClick={() => select(keyOf.preset(preset.id))}
                  className={cn(
                    'min-w-0 border-l-2 py-0.5 pl-2.5 text-left text-body [overflow-wrap:anywhere] hover:bg-raised',
                    selected === keyOf.preset(preset.id) ? 'border-accent font-semibold text-ink' : 'border-transparent text-ink',
                  )}
                >
                  {name}
                </button>
                <span className="block pl-3 text-small text-ink-soft"><Inline text={purpose ?? preset.line} /></span>
              </li>
            );
          })}
        </ul>
      );
    }
    if (tab === 'saved') {
      if (saved.length === 0) return <p className="m-0 text-small text-ink-soft">{t('workflow.place.change.savedNone')}</p>;
      return (
        <ul className="m-0 grid list-none gap-1.5 p-0">
          {saved.map((workflow) => {
            const latest = workflow.versions.at(-1);
            const elsewhere = chosenAt(workflow);
            const key = keyOf.saved(workflow.id);
            return (
              <li key={workflow.id} className="grid min-w-0 gap-0.5">
                <div className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5">
                  <button
                    type="button"
                    aria-pressed={selected === key}
                    onClick={() => select(key, workflow.id)}
                    className={cn(
                      'min-w-0 border-l-2 py-0.5 pl-2.5 text-left text-body [overflow-wrap:anywhere] hover:bg-raised',
                      selected === key ? 'border-accent font-semibold text-ink' : 'border-transparent text-ink',
                    )}
                  >
                    {nameOf(workflow)}
                  </button>
                  {latest !== undefined && <Pill tone="neutral">{t('workflow.place.change.version', { version: latest })}</Pill>}
                  {chosenOnRoute(workflow) && <Pill tone="done">{t('workflow.place.change.chosenHere')}</Pill>}
                </div>
                {workflow.line && <span className="block pl-3 text-small text-ink-soft"><Inline text={workflow.line} /></span>}
                {workflow.problem && <span className="block pl-3 text-small text-warn">{workflow.problem}</span>}
                {elsewhere.length > 0 && <span className="block pl-3 text-meta text-ink-faint">{t('workflow.place.change.chosenAt', { places: elsewhere.join(', ') })}</span>}
              </li>
            );
          })}
        </ul>
      );
    }
    return (
      <ul className="m-0 grid list-none gap-1.5 p-0">
        <li className="grid min-w-0 gap-0.5">
          <div className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5">
            <button
              type="button"
              aria-pressed={selected === keyOf.current}
              onClick={() => select(keyOf.current)}
              className={cn(
                'min-w-0 border-l-2 py-0.5 pl-2.5 text-left text-body hover:bg-raised',
                selected === keyOf.current ? 'border-accent font-semibold text-ink' : 'border-transparent text-ink',
              )}
            >
              {t('workflow.place.change.current')}
            </button>
            <Pill tone="neutral">{t('workflow.place.change.version', { version: current.version })}</Pill>
          </div>
          <span className="block pl-3 text-small text-ink-soft">{t('workflow.place.change.currentLive')}</span>
        </li>
      </ul>
    );
  };

  const canClear = route.own && 'repository' in place;
  const clearing = selected === keyOf.clear;

  const pane = () => {
    if (clearing) {
      return (
        <div className="grid min-w-0 gap-2">
          {plan && <ChangePlan plan={plan} />}
          <div className="flex flex-wrap items-center gap-1.5">
            <Button
              variant="ghost"
              className={small}
              disabled={using || saving || (planned && token === null)}
              onClick={() => press(at)}
            >
              {t('workflow.place.change.clear')}
            </Button>
            <Button variant="ghost" className="min-h-0 px-2 py-0.5 text-small" onClick={() => setSelected(null)}>{t('common.cancel')}</Button>
          </div>
        </div>
      );
    }
    if (selected === null) return <p className="m-0 text-small text-ink-soft">{t('workflow.place.change.pick')}</p>;

    const name = selectedPreset ? presetName(t, i18n, selectedPreset)
      : selectedSaved ? nameOf(selectedSaved) : t('workflow.place.change.current');
    return (
      <div className="grid min-w-0 gap-2">
        <h4 className="m-0 text-body font-semibold text-ink [overflow-wrap:anywhere]">{name}</h4>
        {selectedPreset && <p className="m-0 text-small text-ink-soft"><Inline text={selectedPreset.line} /></p>}
        {preview()}
        {plan && <ChangePlan plan={plan} />}
        <div className="flex flex-wrap items-start gap-1.5">
          {selectedPreset?.graph && selectedPreset.body !== undefined && selectedPreset.body !== null && (
            <>
              <Button variant="ghost" className={small} disabled={saving} onClick={() => setCopying((was) => !was)}>{t('workflow.place.change.copy.open')}</Button>
              <Button
                variant="ghost"
                className={small}
                aria-label={t('workflow.place.change.startFrom', { name })}
                onClick={() => onStartFrom({ graph: selectedPreset.body, name })}
              >
                {t('workflow.place.steps.open')}
              </Button>
            </>
          )}
          {selectedSaved && ask && !chosenOnRoute(selectedSaved) && useHere(name, ask)}
          {selected === keyOf.current && ask && useHere(name, ask)}
        </div>
        {selectedPreset && copying && (
          <CopyForm key={selectedPreset.id} initial={name} busy={saving} onSave={(copied) => copy(selectedPreset, copied)} onCancel={() => setCopying(false)} />
        )}
      </div>
    );
  };

  return (
    <section aria-labelledby={headId} className="grid min-w-0 gap-3 rounded-control border border-line px-3 py-2.5">
      <div className="flex min-w-0 flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <h3 id={headId} className="m-0 text-body font-semibold text-ink">{t('workflow.place.change.title')}</h3>
        <Button variant="ghost" className="min-h-0 px-2 py-0.5 text-small" onClick={onClose}>{t('workflow.place.change.close')}</Button>
      </div>
      <p className="m-0 text-small text-ink-soft">{t('workflow.place.change.body', { route: routeWords(t, route.key, route.label) })}</p>

      {canClear && !clearing && (
        <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
          <span className="min-w-0 flex-1 basis-48 text-small text-ink-soft">
            {t('workflow.place.change.own', { place: here, route: routeWords(t, route.key, route.label) })}
          </span>
          <Button
            variant="ghost"
            className={small}
            disabled={using || saving}
            onClick={() => (announced ? select(keyOf.clear) : press(at))}
          >
            {t('workflow.place.change.clear')}
          </Button>
        </div>
      )}

      {error && !list
        ? <Refused sentence={error instanceof Error ? error.message : String(error)} />
        : list === null
          ? <div aria-busy="true" className="grid gap-1"><p className="m-0 text-small text-ink-soft">{t('workflow.place.change.reading')}</p><SkeletonRows rows={3} /></div>
          : (
            <div className="grid min-w-0 gap-3">
              <Tabs tab={tab} onTab={go} label={t('workflow.place.change.tabs')} ids={ids} />
              <div role="tabpanel" id={ids(tab).panel} aria-labelledby={ids(tab).tab} className="grid min-w-0 gap-3">
                {candidates()}
              </div>
              <div aria-live="polite" className="min-w-0 border-t border-line pt-2.5">{pane()}</div>
            </div>
          )}
      {chosen && <p className="m-0 text-small text-ink">{t('workflow.place.change.chosen')}</p>}
      {said && <Refused sentence={said} />}
    </section>
  );
}

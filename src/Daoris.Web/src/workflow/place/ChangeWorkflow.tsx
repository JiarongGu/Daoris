import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, Pill, SkeletonRows } from '../../design/ui';
import { Refused } from '../../work/InlineConfirm';
import type { PresetWorkflow, SavedWorkflow, WorkflowListAnswer, WorkflowUseAnswer, WorkflowUseAsk } from '../edit/answers';
import type { CurrentWorkflow } from '../current';

/** The workflow a preset starts a draft from: its graph as a version holds it, and the name it is offered under. */
export type PresetStart = { graph: unknown; name: string };

/** The id `WORKFLOW_USE` takes for the workflow Daoris derives from the rules, which no file holds. */
const CURRENT = 'current';

const nameOf = (saved: SavedWorkflow) => saved.name ?? saved.id;

const presetName = (preset: PresetWorkflow, language: string) => (language.startsWith('zh') ? preset.names?.zh : preset.names?.en) ?? preset.name;

/**
 * **Change…** (AGENTSPLACE1h; the Agents-place design §3): what a place's work follows, chosen from three lists: the presets by the line
 * each composes, the workflows saved here with the places that choose them, and *Current*, labelled live and derived. A saved workflow is
 * chosen at the version the person read (`WORKFLOW_USE` refuses `stale` once it moved), and Current is chosen by name. A preset is never
 * stored until saved, so it offers the editor from its steps instead.
 *
 * A molecule: the lists arrive as props, and the choice and the editor's start go out.
 */
export function ChangeWorkflow({ list, error, current, place, using, onUse, onStartFrom, onClose }: {
  /** The saved workflows and the presets; null while read. */
  list: WorkflowListAnswer | null;
  /** The read was refused. */
  error?: unknown;
  /** The place's Current, for its version. */
  current: CurrentWorkflow;
  /** The place the choice is for. */
  place: { repository: string } | { workspace: string };
  using?: boolean;
  onUse: (ask: WorkflowUseAsk) => Promise<WorkflowUseAnswer>;
  /** Open the step editor from a preset's steps. */
  onStartFrom: (start: PresetStart) => void;
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const headId = useId();
  const [said, setSaid] = useState<string | null>(null);
  const here = 'repository' in place ? place.repository : place.workspace;
  const level = 'repository' in place ? 'repository' : 'workspace';

  const use = async (ask: WorkflowUseAsk) => {
    setSaid(null);
    try {
      const answer = await onUse(ask);
      if (answer.refused) setSaid(answer.message ?? answer.refused);
    } catch (failure) {
      setSaid(failure instanceof Error ? failure.message : String(failure));
    }
  };

  const chosenHere = (saved: SavedWorkflow) => saved.chosen.some((each) => each.level === level && each.name === here);
  const chosenAt = (saved: SavedWorkflow) => saved.chosen.filter((each) => !(each.level === level && each.name === here)).map((each) => each.name);

  return (
    <section aria-labelledby={headId} className="grid min-w-0 gap-3 rounded-control border border-line px-3 py-2.5">
      <div className="flex min-w-0 flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <h3 id={headId} className="m-0 text-body font-semibold text-ink">{t('workflow.place.change.title')}</h3>
        <Button variant="ghost" className="min-h-0 px-2 py-0.5 text-small" onClick={onClose}>{t('workflow.place.change.close')}</Button>
      </div>
      <p className="m-0 text-small text-ink-soft">{t('workflow.place.change.body')}</p>

      <div className="grid min-w-0 gap-1">
        <h4 className="m-0 text-small font-medium text-ink">{t('workflow.place.change.current')}</h4>
        <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
          <Pill tone="neutral">{t('workflow.place.change.version', { version: current.version })}</Pill>
          <span className="min-w-0 flex-1 basis-48 text-small text-ink-soft">{t('workflow.place.change.currentLive')}</span>
          <Button
            variant="ghost"
            className="min-h-0 border border-line px-2 py-0.5 text-small"
            disabled={using}
            aria-label={t('workflow.place.change.use', { name: t('workflow.place.change.current') })}
            onClick={() => use({ ...place, workflow: CURRENT })}
          >
            {t('workflow.edit.use')}
          </Button>
        </div>
      </div>

      {error && !list
        ? <Refused sentence={error instanceof Error ? error.message : String(error)} />
        : list === null
          ? <div aria-busy="true" className="grid gap-1"><p className="m-0 text-small text-ink-soft">{t('workflow.place.change.reading')}</p><SkeletonRows rows={3} /></div>
          : (
            <>
              <div className="grid min-w-0 gap-1">
                <h4 className="m-0 text-small font-medium text-ink">{t('workflow.place.change.saved')}</h4>
                {list.workflows.length === 0
                  ? <p className="m-0 text-small text-ink-soft">{t('workflow.place.change.savedNone')}</p>
                  : (
                    <ul className="m-0 grid list-none gap-1.5 p-0">
                      {list.workflows.map((saved) => {
                        const newest = saved.versions.at(-1);
                        const mine = chosenHere(saved);
                        const elsewhere = chosenAt(saved);
                        return (
                          <li key={saved.id} className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
                            <span className="min-w-0 flex-1 basis-48">
                              <span className="block text-body text-ink [overflow-wrap:anywhere]">{nameOf(saved)}</span>
                              {saved.line && <span className="block text-small text-ink-soft"><Inline text={saved.line} /></span>}
                              {saved.problem && <span className="block text-small text-warn">{saved.problem}</span>}
                              {elsewhere.length > 0 && <span className="block text-meta text-ink-faint">{t('workflow.place.change.chosenAt', { places: elsewhere.join(', ') })}</span>}
                            </span>
                            {newest !== undefined && <Pill tone="neutral">{t('workflow.place.change.version', { version: newest })}</Pill>}
                            {mine
                              ? <Pill tone="done">{t('workflow.place.change.chosenHere')}</Pill>
                              : (
                                <Button
                                  variant="ghost"
                                  className="min-h-0 border border-line px-2 py-0.5 text-small"
                                  disabled={using || newest === undefined || saved.problem !== null}
                                  aria-label={t('workflow.place.change.use', { name: nameOf(saved) })}
                                  onClick={() => use({ ...place, workflow: saved.id, ...(newest === undefined ? {} : { version: newest }) })}
                                >
                                  {t('workflow.edit.use')}
                                </Button>
                              )}
                          </li>
                        );
                      })}
                    </ul>
                  )}
              </div>
              <div className="grid min-w-0 gap-1">
                <h4 className="m-0 text-small font-medium text-ink">{t('workflow.place.change.presets')}</h4>
                <ul className="m-0 grid list-none gap-1.5 p-0">
                  {list.presets.map((preset) => {
                    const name = presetName(preset, i18n.language);
                    return (
                      <li key={preset.id} className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
                        <span className="min-w-0 flex-1 basis-48">
                          <span className="block text-body text-ink [overflow-wrap:anywhere]">{name}</span>
                          <span className="block text-small text-ink-soft"><Inline text={preset.line} /></span>
                        </span>
                        {preset.graph && preset.body !== undefined && (
                          <Button
                            variant="ghost"
                            className="min-h-0 border border-line px-2 py-0.5 text-small"
                            aria-label={t('workflow.place.change.startFrom', { name })}
                            onClick={() => onStartFrom({ graph: preset.body, name })}
                          >
                            {t('workflow.place.steps.open')}
                          </Button>
                        )}
                      </li>
                    );
                  })}
                </ul>
              </div>
            </>
          )}
      {said && <Refused sentence={said} />}
    </section>
  );
}

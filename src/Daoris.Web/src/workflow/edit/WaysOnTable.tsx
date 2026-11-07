import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, SelectField } from '../../design/ui';
import type { GraphProblem, WorkflowPaletteAnswer } from './answers';
import { kindIn, rowNone, rowSaid, targetSaid } from './editWords';
import { canInsertOn, type DraftWire, type GraphDraft, rowsOf, targetsOf, type WayRowKey } from './graphDraft';
import { Refusals } from './Refusals';

// *What happens next*: a step's rows, one for each way it may go on (*Once done*,
// *If you ask for changes*, *If it fails*, each outcome), each holding its targets. A target is chosen, never a wire's kind: the row and
// where the target stands give it, so the choices are only the steps that can take the way, a later one by its title and an earlier one as
// *Back to* it. A row may hold several (*Also to…*), each removable alone, and each way that can take a step between its ends offers one.

const NONE = ':none';

/** What the table asks of the page: a way's target changed or removed, a target added, a row chosen on the chart or given a new step. */
export type WaysActs = {
  onTarget: (row: WayRowKey, wire: DraftWire, to: string | null) => void;
  onAdd: (row: WayRowKey, to: string) => void;
  onPick: (row: WayRowKey) => void;
  onNew: (row: WayRowKey) => void;
  onInsert: (wire: DraftWire) => void;
};

export function WaysOnTable({ palette, draft, step, titleOf, problemsAt, acts }: {
  palette: WorkflowPaletteAnswer;
  draft: GraphDraft;
  step: string;
  titleOf: (id: string) => string;
  problemsAt: (wire: string) => GraphProblem[];
  acts: WaysActs;
}) {
  const { t } = useTranslation();
  const headId = useId();
  const from = titleOf(step);
  const rows = rowsOf(palette, draft, step);
  if (rows.length === 0) return null;

  return (
    <section aria-labelledby={headId} className="grid min-w-0 gap-2">
      <h4 id={headId} className="m-0 text-small font-semibold text-ink">{t('workflow.edit.next.title')}</h4>
      {rows.map(({ key, ways }) => {
        const row = rowSaid(t, key, kindIn(draft, step));
        const where = t('workflow.edit.next.where', { row, step: from });
        const open = targetsOf(palette, draft, step, key);
        const option = (id: string, kind: string) => ({ value: id, label: targetSaid(t, kind, titleOf(id)) });
        return (
          <div key={key} data-way-row={key} className="grid min-w-0 gap-1 border-t border-line pt-2">
            <span className={key.startsWith('outcome:') ? 'font-mono text-small text-ink' : 'text-small text-ink'}>{row}</span>
            {ways.length === 0 && (
              <SelectField
                ariaLabel={where}
                value={NONE}
                onChange={(to) => { if (to !== NONE) acts.onAdd(key, to); }}
                options={[{ value: NONE, label: rowNone(t, key) }, ...open.map((target) => option(target.id, target.ask.kind))]}
              />
            )}
            {ways.map((wire, index) => {
              const choices = targetsOf(palette, draft, step, key, wire.id);
              const kept = choices.some((each) => each.id === wire.to) ? [] : [option(wire.to, wire.kind)];
              const to = titleOf(wire.to);
              return (
                <div key={wire.id} className="grid min-w-0 gap-1">
                  <div className="flex min-w-0 flex-wrap items-center gap-1.5">
                    <div className="min-w-0 flex-1">
                      <SelectField
                        ariaLabel={index === 0 ? where : t('workflow.edit.next.alsoLabel', { row, step: from })}
                        value={wire.to}
                        onChange={(chosen) => acts.onTarget(key, wire, chosen === NONE ? null : chosen)}
                        options={[{ value: NONE, label: rowNone(t, key) }, ...kept, ...choices.map((target) => option(target.id, target.ask.kind))]}
                      />
                    </div>
                    <Button variant="ghost" className="min-h-0 border border-line py-0.5 text-small" aria-label={t('workflow.edit.next.removeNamed', { row, to })}
                      onClick={() => acts.onTarget(key, wire, null)}>
                      {t('workflow.edit.next.remove')}
                    </Button>
                    {canInsertOn(draft, wire.id) && (
                      <Button variant="ghost" className="min-h-0 border border-line py-0.5 text-small"
                        aria-label={t('workflow.edit.insert.between', { from, to })} onClick={() => acts.onInsert(wire)}>
                        {t('workflow.edit.next.insert')}
                      </Button>
                    )}
                  </div>
                  <Refusals problems={problemsAt(wire.id)} />
                </div>
              );
            })}
            {ways.length > 0 && open.length > 0 && (
              <SelectField
                ariaLabel={t('workflow.edit.next.alsoLabel', { row, step: from })}
                value=""
                placeholder={t('workflow.edit.next.also')}
                onChange={(to) => acts.onAdd(key, to)}
                options={open.map((target) => option(target.id, target.ask.kind))}
              />
            )}
            <div className="flex min-w-0 flex-wrap gap-1.5">
              {open.length > 0 && (
                <Button variant="ghost" className="min-h-0 border border-line py-0.5 text-small" aria-label={t('workflow.edit.next.pickNamed', { row, step: from })}
                  onClick={() => acts.onPick(key)}>
                  {t('workflow.edit.next.pick')}
                </Button>
              )}
              <Button variant="ghost" className="min-h-0 border border-line py-0.5 text-small" aria-label={t('workflow.edit.next.newNamed', { row, step: from })}
                onClick={() => acts.onNew(key)}>
                {t('workflow.edit.next.new')}
              </Button>
            </div>
          </div>
        );
      })}
    </section>
  );
}

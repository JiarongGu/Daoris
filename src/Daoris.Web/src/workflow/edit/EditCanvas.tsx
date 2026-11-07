import type { TFunction, i18n as I18n } from 'i18next';
import { useTranslation } from 'react-i18next';
import { Button, Prose } from '../../design/ui';
import { type ChartEdit, FlowChart } from '../chart/FlowChart';
import { type DrawnNode, type DrawnWire, type DrawOptions, graphFlow, type GraphFlow } from '../graphFlow';
import type { PresetWorkflow, SavedWorkflow, WorkflowListAnswer, WorkflowPaletteAnswer } from './answers';
import { howSaid, kindIn, plain, stepTitle } from './editWords';
import { entriesOf, type GraphDraft, outcomesOf } from './graphDraft';
import { presetName } from './presetWords';

// The editor's canvas: the draft drawn through the one drawing (`graphFlow`) and the chart's
// components, edited on through `ChartEdit`; and, while it holds no step, where to begin: a preset or a workflow saved here, so nothing
// *Start from* offered is lost with it.

/** The draft drawn through the one drawing: each step a node pressed to select it, each way between two of them an edge, in the editor's words. */
export function draftFlow(
  t: TFunction, i18n: I18n, palette: WorkflowPaletteAnswer | null, draft: GraphDraft, holds: DrawOptions['holds'], select: (id: string) => void,
): GraphFlow {
  const nodes = draft.nodes.map((node): DrawnNode => ({
    id: node.id, kind: node.kind, title: stepTitle(t, i18n, palette, node), outcomes: outcomesOf(node), open: () => select(node.id),
  }));
  const wires = draft.wires.filter((wire) => wire.from !== null).map((wire): DrawnWire => ({
    id: wire.id, kind: wire.kind, from: wire.from!, to: wire.to, outcome: wire.outcome, on: wire.on, says: plain(howSaid(t, wire, kindIn(draft, wire.from!))),
    ...(wire.kind === 'data' ? { output: wire.fromPort } : {}),
  }));
  return graphFlow(t, { nodes, wires, entries: entriesOf(draft), end: draft.end }, { label: t('workflow.edit.drawing'), holds });
}

/** A place to begin an empty draft from: a preset's graph, or a saved workflow's newest version. */
export type Begin = { preset: PresetWorkflow } | { saved: SavedWorkflow };

export function EditCanvas({ flow, selected, edit, list, waiting, onBegin }: {
  /** The drawing, or null while the draft holds no step. */
  flow: GraphFlow | null;
  selected: string | null;
  edit: ChartEdit;
  list: WorkflowListAnswer;
  /** A saved workflow being read to begin from. */
  waiting: string | null;
  onBegin: (from: Begin) => void;
}) {
  const { t, i18n } = useTranslation();
  if (flow) {
    return (
      <FlowChart label={flow.label} nodes={flow.nodes} edges={flow.edges} entries={flow.entries} sections={flow.sections} track={flow.track}
        selected={selected} edit={edit} canvas="edit" />
    );
  }
  const presets = list.presets.filter((preset) => preset.graph && preset.body);
  const saved = list.workflows.filter((each) => each.schema === 2 && !each.problem);
  const begin = (label: string, onPress: () => void, key: string) => (
    <li key={key}>
      <Button variant="ghost" className="min-h-0 border border-line py-0.5 text-small" aria-label={t('workflow.edit.empty.from', { name: label })} onClick={onPress}>
        {label}
      </Button>
    </li>
  );
  return (
    <div className="grid min-w-0 gap-3 rounded-card border border-dashed border-line-strong p-4">
      <Prose className="text-small">{t('workflow.edit.empty.body')}</Prose>
      {presets.length > 0 && (
        <div className="grid min-w-0 gap-1">
          <p className="m-0 text-meta text-ink-soft">{t('workflow.edit.empty.presets')}</p>
          <ul className="m-0 flex list-none flex-wrap gap-1.5 p-0">
            {presets.map((preset) => begin(presetName(t, i18n, preset), () => onBegin({ preset }), `preset:${preset.id}`))}
          </ul>
        </div>
      )}
      {saved.length > 0 && (
        <div className="grid min-w-0 gap-1">
          <p className="m-0 text-meta text-ink-soft">{t('workflow.edit.source.saved')}</p>
          <ul className="m-0 flex list-none flex-wrap gap-1.5 p-0">
            {saved.map((each) => begin(
              t('workflow.edit.source.savedVersion', { name: each.name ?? each.id, version: each.versions.at(-1) ?? 1 }),
              () => onBegin({ saved: each }), `saved:${each.id}`,
            ))}
          </ul>
        </div>
      )}
      {waiting && <p className="m-0 text-meta text-ink-faint">{t('workflow.edit.source.loading', { id: waiting })}</p>}
    </div>
  );
}

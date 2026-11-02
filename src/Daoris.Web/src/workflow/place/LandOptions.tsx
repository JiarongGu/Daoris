import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { fingerprintOf } from '../draftFingerprint';
import type { EditorRole, WorkflowPaletteAnswer, WorkflowSaveAnswer, WorkflowSaveAsk } from '../edit/answers';
import { stepTitle } from '../edit/editWords';
import { choiceOf, type GraphDraft, draftOf, graphOf, setOption } from '../edit/graphDraft';
import { optionsShown, setNodeOption } from '../edit/Inspector';
import { StepOptions } from '../edit/StepOptions';
import { NextVersion } from './NextVersion';

/**
 * **The land step's options of the version a place follows** (WORKFLOWCLEAN1h-b; the land node design §150): the editor's own options for the
 * `land` step, and a change saved as the version's next through `NextVersion`, the editor's own judgement, review and save. It draws nothing
 * but the options and the save; the place's chart and the editor stay where they are.
 *
 * A molecule: the graph and the bridge's answers arrive as props, and the judgement and the save go out. Nothing here is a page rule.
 */
export function LandOptions({
  palette, roles, graph, workflow, lines, judged, judgedOf, onJudge, saving, onSave,
}: {
  palette: WorkflowPaletteAnswer;
  roles: EditorRole[];
  /** The followed version's graph as it is saved. */
  graph: unknown;
  /** The saved workflow it is a version of, and the version the change is made against. */
  workflow: { id: string; version: number };
  /** The lines the place declares, which a landing's target is chosen from. */
  lines?: string[];
  /** The newest planned save's answer and the fingerprint of the draft it answered. */
  judged: WorkflowSaveAnswer | null;
  judgedOf: string | null;
  onJudge: (ask: WorkflowSaveAsk) => void;
  saving: boolean;
  onSave: (ask: WorkflowSaveAsk) => Promise<WorkflowSaveAnswer>;
}) {
  const { t, i18n } = useTranslation();
  const base = useMemo(() => draftOf(graph), [graph]);
  const baseFingerprint = useMemo(() => fingerprintOf(graphOf(base)), [base]);
  const [draft, setDraft] = useState<GraphDraft>(base);
  // The version saved becomes the one followed, and the page hands it back as the graph: the draft begins again from it.
  useEffect(() => { setDraft(base); }, [baseFingerprint]); // eslint-disable-line react-hooks/exhaustive-deps

  const land = draft.nodes.find((node) => node.kind === 'land');
  if (!land) return null;
  const choice = choiceOf(palette, land.kind);
  const { options, notes } = optionsShown(t, i18n, palette, land);
  const acts = {
    onChange: (made: GraphDraft) => setDraft(made),
    onOption: (option: string, value: string | boolean | string[] | undefined) => setDraft(setOption(draft, land.id, option, value)),
  };

  return (
    <div className="grid min-w-0 gap-3">
      {choice && options.length > 0 && (
        <StepOptions
          node={land}
          fields={options}
          roleTakes={choice.roleTakes}
          roles={roles}
          lines={lines}
          notes={notes}
          onSet={(option, value) => setNodeOption(t, i18n, palette, draft, land, stepTitle(t, i18n, palette, land), acts, option, value)}
        />
      )}
      <NextVersion
        palette={palette}
        base={base}
        draft={draft}
        workflow={workflow}
        judged={judged}
        judgedOf={judgedOf}
        onJudge={onJudge}
        saving={saving}
        onSave={onSave}
        onReread={() => setDraft(base)}
      />
    </div>
  );
}

import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDebounced } from '../../lib/useDebounced';
import { sentence } from '../../locales/format';
import { fingerprintOf } from '../draftFingerprint';
import type { EditorRole, WorkflowPaletteAnswer, WorkflowSaveAnswer, WorkflowSaveAsk } from '../edit/answers';
import { stepTitle } from '../edit/editWords';
import { choiceOf, type GraphDraft, draftOf, graphOf, setOption } from '../edit/graphDraft';
import { optionsShown, setNodeOption } from '../edit/Inspector';
import { changesBetween, consentKey } from '../edit/saveChanges';
import { type ReviewFacts, SaveReview } from '../edit/SaveReview';
import { StepOptions } from '../edit/StepOptions';

/** How long a change holds still before it is judged, as the editor judges: each judge is a planned save. */
const JUDGE_MS = 250;

/**
 * **The land step's options of the version a place follows** (WORKFLOWCLEAN1h-b; the land node design §150): the editor's own options for the
 * `land` step and the editor's own save, so a change is judged as the version's next, reviewed with the places it reaches, and saved only
 * once those places are let follow it. It draws nothing but the options and the save; the place's chart and the editor stay where they are.
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
  const [reviewing, setReviewing] = useState(false);
  const [consented, setConsented] = useState<string | null>(null);
  const [said, setSaid] = useState<{ saved: WorkflowSaveAnswer | null; failed: string | null }>({ saved: null, failed: null });
  // The version saved becomes the one followed, and the page hands it back as the graph: the draft begins again from it.
  useEffect(() => { setDraft(base); setReviewing(false); setConsented(null); }, [baseFingerprint]); // eslint-disable-line react-hooks/exhaustive-deps

  const land = draft.nodes.find((node) => node.kind === 'land');
  const next = useMemo(() => graphOf(draft), [draft]);
  const fingerprint = useMemo(() => fingerprintOf(next), [next]);
  const changed = fingerprint !== baseFingerprint;
  const ask = useMemo((): WorkflowSaveAsk => ({ id: workflow.id, graph: next, base: workflow.version }), [next, workflow.id, workflow.version]);
  const settled = useDebounced(ask, JUDGE_MS);
  useEffect(() => { if (changed && fingerprintOf(settled.graph) === fingerprint) onJudge(settled); }, [settled]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!land) return null;
  const choice = choiceOf(palette, land.kind);
  const { options, notes } = optionsShown(t, i18n, palette, land);
  const hold = (made: GraphDraft) => { setDraft(made); setReviewing(false); setSaid({ saved: null, failed: null }); };
  const acts = { onChange: (made: GraphDraft) => hold(made), onOption: (option: string, value: string | boolean | string[] | undefined) => hold(setOption(draft, land.id, option, value)) };

  const holds = changed && judged?.done === true && judgedOf === fingerprint;
  const refused = said.saved?.refused === 'reach' ? said.saved : null;
  const scopes = refused?.scopes ?? (holds && judged?.appended ? judged.scopes : []);
  const reached = refused?.reach ?? (holds ? judged?.reach ?? [] : []);
  const consent = consentKey(scopes);
  const allowed = scopes.length > 0 && consented === consent;
  const review = (): ReviewFacts => {
    const title = (each: { kind: string }) => stepTitle(t, i18n, palette, each);
    const made = changesBetween(base, draft);
    return {
      fresh: null,
      added: made.added.map(title),
      removed: made.removed.map(title),
      changed: made.changed.map(title),
      ways: made.ways,
      part: draft.nodes.filter((each) => palette.kinds.find((kind) => kind.kind === each.kind)?.personJudges).map(title),
      reach: reached.flatMap((row) => row.cannots.length > 0
        ? [t('workflow.edit.review.cannot', { repository: row.repository, why: row.said ?? row.cannots.join('; ') })]
        : row.noGates ? [t('workflow.edit.review.noGates', { repository: row.repository })] : []),
    };
  };
  const save = async () => {
    try {
      const answer = await onSave({ ...ask, ...(allowed ? { authorize: scopes.map((scope) => scope.key) } : {}) });
      setSaid({ saved: answer, failed: null });
      if (answer.done) { setReviewing(false); setConsented(null); }
    } catch (failure) {
      setSaid({ saved: null, failed: sentence(failure) });
    }
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
      {changed && (
        <SaveReview
          target={{ id: workflow.id, name: '', base: workflow.version }}
          id={workflow.id}
          onName={() => {}}
          empty={false}
          holds={holds}
          review={reviewing && holds ? review() : null}
          onReview={setReviewing}
          scopes={scopes}
          consented={allowed}
          onConsent={(on) => setConsented(on ? consent : null)}
          saving={saving}
          using={false}
          said={{ saved: said.saved, used: null, failed: said.failed }}
          onSave={() => void save()}
          onReread={() => hold(base)}
          use={{ is: 'none' }}
          onPlan={() => {}}
          onUse={() => {}}
        />
      )}
    </div>
  );
}

import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDebounced } from '../../lib/useDebounced';
import { sentence } from '../../locales/format';
import { fingerprintOf } from '../draftFingerprint';
import type { WorkflowPaletteAnswer, WorkflowSaveAnswer, WorkflowSaveAsk } from '../edit/answers';
import { stepTitle } from '../edit/editWords';
import { type GraphDraft, graphOf } from '../edit/graphDraft';
import { changesBetween, consentKey } from '../edit/saveChanges';
import { type ReviewFacts, SaveReview } from '../edit/SaveReview';

/** How long a change holds still before it is judged, as the editor judges: each judge is a planned save. */
const JUDGE_MS = 250;

/**
 * **A saved workflow's next version** (WORKFLOWCLEAN1h-b): the editor's own judgement, review and save of a draft against the version it was
 * made from, with the places the append reaches let follow it by one consent (SETUPUX1s). `draft` is judged as `WORKFLOW_SAVE {plan}` once it
 * differs from `base`; the review opens only on a judgement of this very draft that holds; nothing is saved that was not reviewed.
 *
 * A molecule: the bridge's answers arrive as props, and the judgement and the save go out. `updates` names the shipped default a version
 * updates its copy to, which the save keeps as where the version came from.
 */
export function NextVersion({
  palette, base, draft, workflow, updates, judged, judgedOf, onJudge, saving, onSave, onReread,
}: {
  palette: WorkflowPaletteAnswer;
  /** The version followed, as a draft. */
  base: GraphDraft;
  /** The draft to save as its next. */
  draft: GraphDraft;
  workflow: { id: string; version: number };
  updates?: string;
  judged: WorkflowSaveAnswer | null;
  judgedOf: string | null;
  onJudge: (ask: WorkflowSaveAsk) => void;
  saving: boolean;
  onSave: (ask: WorkflowSaveAsk) => Promise<WorkflowSaveAnswer>;
  /** After a refusal that the version moved: the page reads it again. */
  onReread: () => void;
}) {
  const { t, i18n } = useTranslation();
  const [reviewing, setReviewing] = useState(false);
  const [consented, setConsented] = useState<string | null>(null);
  const [said, setSaid] = useState<{ saved: WorkflowSaveAnswer | null; failed: string | null }>({ saved: null, failed: null });
  const next = useMemo(() => graphOf(draft), [draft]);
  const fingerprint = useMemo(() => fingerprintOf(next), [next]);
  const baseFingerprint = useMemo(() => fingerprintOf(graphOf(base)), [base]);
  const changed = fingerprint !== baseFingerprint;
  const ask = useMemo((): WorkflowSaveAsk => ({ id: workflow.id, graph: next, base: workflow.version }), [next, workflow.id, workflow.version]);
  const settled = useDebounced(ask, JUDGE_MS);
  useEffect(() => { if (changed && fingerprintOf(settled.graph) === fingerprint) onJudge(settled); }, [settled]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { setReviewing(false); setSaid({ saved: null, failed: null }); }, [fingerprint]);

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
      const answer = await onSave({ ...ask, ...(allowed ? { authorize: scopes.map((scope) => scope.key) } : {}), ...(updates ? { updates } : {}) });
      setSaid({ saved: answer, failed: null });
      if (answer.done) { setReviewing(false); setConsented(null); }
    } catch (failure) {
      setSaid({ saved: null, failed: sentence(failure) });
    }
  };

  if (!changed) return null;
  return (
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
      onReread={onReread}
      use={{ is: 'none' }}
      onPlan={() => {}}
      onUse={() => {}}
    />
  );
}

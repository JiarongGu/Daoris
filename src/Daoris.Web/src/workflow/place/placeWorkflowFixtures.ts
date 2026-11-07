import { SETUP_DEFAULTS } from '../../projects/fixtures';
import type { RepositorySetupProps } from '../../projects/setup/RepositorySetup';
import type { CurrentWorkflow, WorkflowStart } from '../current';
import { EFFECTIVE_CURRENT, EFFECTIVE_DOCS, EFFECTIVE_LANDED_NONE, EFFECTIVE_RELEASE } from './effectiveFixtures';
import { FULL, NO_VERIFICATION, START } from '../fixtures';

// A place's Workflow for the stories and the measured check: the engine of the workspace aurora, whose default work follows a
// saved release workflow, whose documentation work follows another, and whose landed work is verified by nothing.

const nothing = () => {};

/** Default work follows the saved release workflow; Documentation follows Documents workflow; landed work is not verified. */
export const SAVED_ROUTES: WorkflowStart = {
  ...START,
  routes: [
    { kind: null, label: null, paths: [], workflow: 'release', level: 'repository', name: 'Release for engine' },
    { ...START.routes[1]!, workflow: 'documents', name: 'Documents workflow' },
  ],
  landed: NO_VERIFICATION.landed,
  verifications: [],
};

/** Default work follows Current: nothing names a workflow. */
export const CURRENT_ROUTES: WorkflowStart = { ...SAVED_ROUTES, routes: [START.routes[0]!, SAVED_ROUTES.routes[1]!] };

/** The place's Workflow as `WORKFLOW_CURRENT` answers it for the route asked. */
export const answerFor = (route: string): CurrentWorkflow => ({
  ...FULL,
  start: SAVED_ROUTES,
  effective: route === 'kind:docs' ? EFFECTIVE_DOCS : route === 'landed' ? EFFECTIVE_LANDED_NONE : EFFECTIVE_RELEASE,
});

/** Default work following Current, drawn from the rules as they stand. */
export const CURRENT_ANSWER: CurrentWorkflow = { ...FULL, start: CURRENT_ROUTES, effective: EFFECTIVE_CURRENT };

/** The rows the folds hold: the line, its landing, the commits, the second opinion, the look, the landed route and a heavy gate. */
export const PLACE_SETUP: RepositorySetupProps = {
  ...SETUP_DEFAULTS,
  work: {
    ...SETUP_DEFAULTS.work,
    commits: { repository: 'engine', workspace: 'aurora', rule: { mode: 'as-you-go' }, source: 'default' },
    onCommits: nothing,
    review: { repository: 'engine', workspace: 'aurora', rule: null, source: null },
    onReview: nothing,
    opinion: { repository: 'engine', workspace: 'aurora', rule: null, source: null },
    onOpinion: nothing,
  },
  landed: { start: SAVED_ROUTES, workspace: 'aurora', onChoose: nothing },
  heavy: { gates: [{ name: 'build', run: 'dotnet build' }], onSave: nothing },
};

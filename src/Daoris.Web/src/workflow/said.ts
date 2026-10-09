import type { TFunction } from 'i18next';
import {
  flagOf, KNOWN_KINDS, KNOWN_LIMITS, namesOf, textOf, type CurrentWorkflow, type WorkflowStep, yourPart,
} from './current';

// What a Current says in the reader's language (WORKFLOW1b, the workflow design §6.2): each step's title, who acts, what it
// is set to, where it was set, what moves work on after it, and its limit. Every sentence is composed from the step's typed
// fields, so it cannot drift from what the driver drew (design §2.5); a name from the person's file (an environment, a
// branch pattern, a plugin's id, a reviewer) is set as code and never translated.

/** Names set as code: `a`, `b`. */
const ticked = (names: readonly string[]) => names.map((name) => `\`${name}\``);

const known = <T extends string>(list: readonly T[], value: string): value is T => (list as readonly string[]).includes(value);

/** A step's title: its kind's name, a landing by its form, the look by its environment. */
export function stepTitle(t: TFunction, step: WorkflowStep): string {
  if (!known(KNOWN_KINDS, step.kind)) return t('workflow.step.unknown', { kind: step.kind });
  switch (step.kind) {
    case 'work': return t('workflow.step.work');
    case 'opinion': return t('workflow.step.opinion');
    case 'look': {
      const environment = textOf(step, 'environment');
      return environment ? t('workflow.step.look', { environment }) : t('workflow.step.lookAnywhere');
    }
    case 'landing': return textOf(step, 'form') === 'branch' ? t('workflow.step.landingBranch') : t('workflow.step.landingMerge');
    default: return t('workflow.step.pullRequest');
  }
}

/** Who acts (design §3.1), said apart from the person's part: an agent, the reviewers by name, Daoris, or a plugin by its id. */
export function executorSaid(t: TFunction, step: WorkflowStep): string {
  if (step.executor === 'daoris') return t('workflow.executor.daoris');
  if (step.executor === 'plugin') {
    const plugin = textOf(step, 'plugin');
    return plugin ? t('workflow.executor.plugin', { plugin }) : t('workflow.executor.agent');
  }
  const reviewers = step.kind === 'opinion' ? namesOf(step, 'reviewers') : [];
  return reviewers.length > 0
    ? t('workflow.executor.reviewers', { reviewers: ticked(reviewers).join(t('workflow.elseJoin')) })
    : t('workflow.executor.agent');
}

/** What a step is set to, as sentences: the settings its kind carries, in the reader's words. */
export function stepSet(t: TFunction, step: WorkflowStep): string | null {
  const join = (parts: string[]) => parts.join(t('workflow.set.join'));
  switch (step.kind) {
    case 'work':
      return t('workflow.set.work');
    case 'opinion': {
      const on = namesOf(step, 'on');
      const landing = on.includes('landing');
      const steps = on.includes('steps');
      return t('workflow.set.opinion.says', {
        when: t(landing && steps ? 'workflow.set.opinion.both' : steps ? 'workflow.set.opinion.steps' : 'workflow.set.opinion.landing'),
        required: t(flagOf(step, 'required') ? 'workflow.set.opinion.required' : 'workflow.set.opinion.optional'),
        recheck: t(flagOf(step, 'recheck') ? 'workflow.set.opinion.recheck' : 'workflow.set.opinion.noRecheck'),
      });
    }
    case 'look': {
      const environment = textOf(step, 'environment');
      return environment ? t('workflow.set.look', { environment }) : t('workflow.set.lookAnywhere');
    }
    case 'landing': {
      const pattern = textOf(step, 'pattern');
      const plugin = textOf(step, 'plugin');
      const branch = textOf(step, 'form') === 'branch';
      return join([
        branch && pattern ? t('workflow.set.landing.branch', { pattern }) : t('workflow.set.landing.merge'),
        textOf(step, 'accept') === 'automatic' ? t('workflow.set.landing.automatic') : t('workflow.set.landing.accept'),
        ...(branch ? [plugin ? t('workflow.set.landing.plugin', { plugin }) : t('workflow.set.landing.push')] : []),
        ...(flagOf(step, 'tidy') ? [t('workflow.set.landing.tidy')] : []),
      ]);
    }
    case 'pull-request': {
      const plugin = textOf(step, 'plugin');
      return plugin ? t('workflow.set.pullRequest', { plugin }) : null;
    }
    default:
      return null;
  }
}

/**
 * Where a step was read from (design §2.7, its source): this repository's rule, its workspace's, Daoris's default, or for
 * the work, always. On a workspace's page a rule at the workspace's level is this workspace's own.
 */
export function sourceSaid(t: TFunction, step: WorkflowStep, page: 'repository' | 'workspace', workspace: string): string {
  const { rule, level } = step.source;
  if (rule === null) return t('workflow.source.always');
  const named = t(`workflow.rule.${rule}`);
  if (level === 'repository') return t('workflow.source.repository', { rule: named });
  if (level === 'workspace') {
    return page === 'workspace'
      ? t('workflow.source.ownWorkspace', { rule: named })
      : t('workflow.source.workspace', { rule: named, workspace });
  }
  return t('workflow.source.default', { rule: named });
}

/** What moves work on after a step (design §6.2's edges): the person's press where the step has one, else automatically. */
export function edgeSaid(t: TFunction, step: WorkflowStep): string {
  return step.press === null ? t('workflow.edge.automatic') : t(`workflow.edge.${step.press}`);
}

/** The person's part, composed from the steps' presses in order (design §2.5): no summary is typed. */
export function partSaid(t: TFunction, workflow: Pick<CurrentWorkflow, 'steps'>): string {
  const presses = yourPart(workflow.steps);
  if (presses.length === 0) return t('workflow.part.none');
  const parts = presses.map(({ press, environment }) => (press === 'reviewed'
    ? environment ? t('workflow.part.reviewed', { environment }) : t('workflow.part.reviewedAnywhere')
    : t(`workflow.part.${press}`)));
  return t('workflow.part.says', { parts: parts.join(t('workflow.part.join')) });
}

/**
 * A step's limit, never folded away (design §6.2): in the reader's words by its code, and where the page has no words for
 * a code, the driver's own sentence, marked as shown as recorded.
 */
export function limitSaid(
  t: TFunction, step: WorkflowStep, limits: Record<string, string>,
): { text: string; recorded: boolean } | null {
  if (step.limit === null) return null;
  if (known(KNOWN_LIMITS, step.limit)) return { text: t(`workflow.limit.${step.limit}`), recorded: false };
  return { text: limits[step.limit] ?? step.limit, recorded: true };
}

/** The plugins that may hold a start, as one sentence, or null where none may. */
export function holdsSaid(t: TFunction, startHolds: readonly string[]): string | null {
  return startHolds.length === 0 ? null : t('workflow.holds.says', { plugins: ticked(startHolds).join(t('workflow.listJoin')) });
}

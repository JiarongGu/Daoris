import { useTranslation } from 'react-i18next';
import { Button, Inline, Prose, SelectField } from '../../design/ui';
import type { GitRead } from './RepositoryGit';

/** What a switch rests on (the bridge's `GitSwitchPlan`, handed as it came). */
export type SwitchPlanValue = {
  from: string | null;
  head: string | null;
  to: string;
  dirty: boolean;
  elsewhere: string | null;
  why: string | null;
  hooks: readonly string[];
};

/** The press: the branch chosen, and the branch and commit HEAD stood on in the plan shown, which the driver finds unchanged or refuses. */
export type SwitchBoxPress = { branch: string; from: string | null; head: string | null };

/**
 * **The switch box of the Changes tab** (GITAPP2k, D204, the git-writes design §8): the local branches to move this checkout to, the hooks
 * the switch runs (never skipped) and, in the driver's sentence, why it would be refused (tracked files changed, the branch out in another
 * tree, the branch the checkout is on). A switch is never forced and carries no change across; switching back is the same press.
 *
 * @remarks **A molecule**: the plan arrives as a prop with the branch chosen, which is its holder's since the plan is read per branch.
 * `busy` holds the press while an act runs.
 */
export function SwitchBox({ branches, chosen, onChoose, read, busy, onPress }: {
  /** The local branches offered, by short name. */
  branches: readonly string[];
  chosen: string;
  onChoose: (branch: string) => void;
  /** The plan for the branch chosen; absent while none is. */
  read: GitRead<SwitchPlanValue> | null;
  busy: boolean;
  onPress: (press: SwitchBoxPress) => void;
}) {
  const { t } = useTranslation();
  const plan = read?.state === 'read' ? read.value : null;
  const blocked = chosen === '' ? null : plan ? plan.why : null;
  return (
    <section aria-label={t('projects.git.switch.heading')} className="grid min-w-0 gap-2 rounded-control border border-line p-2">
      <h3 className="m-0 text-small font-semibold text-ink-faint">{t('projects.git.switch.heading')}</h3>
      <Prose className="min-w-0 text-small text-ink-soft"><Inline text={t('projects.git.switch.note')} /></Prose>
      {branches.length === 0
        ? <Prose className="min-w-0 text-small text-ink-soft">{t('projects.git.switch.none')}</Prose>
        : <SelectField value={chosen} onChange={onChoose} options={branches.map((name) => ({ value: name, label: name }))} placeholder={t('projects.git.switch.choose')} ariaLabel={t('projects.git.switch.branch')} disabled={busy} />}
      {chosen !== '' && read?.state === 'reading' && <Prose className="text-small text-ink-soft">{t('projects.git.switch.reading')}</Prose>}
      {chosen !== '' && read?.state === 'refused' && <Prose className="text-small text-ink-soft">{read.sentence}</Prose>}
      {plan && (
        <Prose className="min-w-0 text-small text-ink-soft">
          <Inline text={plan.hooks.length === 0 ? t('projects.git.commit.hooksNone') : t('projects.git.commit.hooks', { hooks: plan.hooks.map((hook) => `\`${hook}\``).join(', ') })} />
        </Prose>
      )}
      {blocked && <Prose className="min-w-0 text-small text-ink-soft"><Inline text={blocked} /></Prose>}
      <div className="flex flex-wrap gap-2">
        <Button disabled={busy || chosen === '' || !plan || blocked !== null} onClick={() => onPress({ branch: chosen, from: plan!.from, head: plan!.head })}>
          {t('projects.git.switch.press')}
        </Button>
      </div>
    </section>
  );
}

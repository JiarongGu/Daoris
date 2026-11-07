import type { ReactNode } from 'react';
import { CommitField, commitRowSays, type RepositoryCommits, type CommitRule } from '../../settings/repository/Commits';
import { HeavyGates, heavySummary } from '../../settings/repository/HeavyGates';
import { IntegrationDeclaration } from '../../settings/repository/IntegrationDeclaration';
import { LandedRoute, type LandedSetup } from '../../settings/repository/LandedRoute';
import { SessionChecksRow, sessionChecksSummary } from '../../settings/repository/SessionChecks';
import { LinePolicyField, policySays } from '../../settings/repository/IntegratePolicy';
import { LineField, type RepositoryLine } from '../../settings/repository/Lines';
import { OpinionField, opinionRowSays, type RepositoryOpinion } from '../../settings/repository/Opinions';
import { type RepositoryReview, ReviewField, reviewRowSays } from '../../settings/repository/Reviews';
import { CheckField, Inline, SettingRow } from '../../design/ui';
import { clockOf } from '../../locales/format';
import { Inheritable } from './SetupParts';
import { type TFunction, type i18n } from 'i18next';
import type { Driving, HeavySetup, LandsHere, SetupSectionId, WorkSetup } from './RepositorySetup';
import { JOIN, LandingSetting } from './setupBits';

export function setupRows({ driving, drivingLine, t, repository, busy, i18n, line, work, lineOwn, lands, commits, commitRule, commitsOwn, review, reviewOwn, opinion, opinionOwn, landed, gatesApart, heavy }: {
  driving: Driving | null | undefined;
  drivingLine: string | null | undefined;
  t: TFunction<"translation", undefined>;
  repository: string;
  busy: boolean;
  i18n: i18n;
  line: RepositoryLine | null | undefined;
  work: WorkSetup | null | undefined;
  lineOwn: boolean;
  lands: LandsHere | null;
  commits: RepositoryCommits | null | undefined;
  commitRule: CommitRule;
  commitsOwn: boolean;
  review: RepositoryReview | null | undefined;
  reviewOwn: boolean;
  opinion: RepositoryOpinion | null | undefined;
  opinionOwn: boolean;
  landed: (LandedSetup & { workspace: string; }) | null | undefined;
  gatesApart: boolean;
  heavy: HeavySetup | null | undefined;
}) {
  const sections: { id: SetupSectionId; summary: string; body: ReactNode }[] = [];
  if (driving && drivingLine) {
    sections.push({
      id: 'driving',
      summary: drivingLine,
      body: (
        <>
          <SettingRow
            label={t('projects.driver.drive')}
            hint={t('projects.setup.twin.drive', { repository })}
            control={<CheckField checked={driving.drivable} disabled={busy} onChange={driving.onDrive} label={t('projects.driver.drive')} hideLabel />}
          >
            {driving.note && <p className="m-0 text-small text-ink-soft">{driving.note}</p>}
            {driving.stewardSet && (
              <p data-steward-set="" className="m-0 text-small text-ink-soft">
                {t('projects.setup.stewardSet', { time: clockOf(driving.stewardSet, undefined, i18n.language) })}
              </p>
            )}
          </SettingRow>
          {/* A hold appears only once the repository is driven: a hold on nothing is noise. */}
          {driving.drivable && (
            <SettingRow
              label={t('projects.driver.hold')}
              hint={t('projects.setup.twin.hold', { repository })}
              control={<CheckField checked={driving.held} disabled={busy} onChange={driving.onHold} label={t('projects.driver.hold')} hideLabel />}
            />
          )}
          {/* Session trees (D51): its sessions open their own worktree, so the person's uncommitted work in the
              checkout stops holding the driver. */}
          <SettingRow
            label={t('projects.driver.trees')}
            hint={t('projects.setup.twin.trees', { repository })}
            control={<CheckField checked={driving.ownTree} disabled={busy} onChange={driving.onTrees} label={t('projects.driver.trees')} hideLabel />}
          />
        </>
      ),
    });
  }
  // The rows of the line and landing, each one `work` holds and the place's folds hold apart (SETUPUX1o).
  const lineRow = line && work?.onLine
    ? (
      <>
            <Inheritable
              setHere={t('projects.setup.setHere')}
              label={t('projects.setup.line')}
              twin={t('projects.setup.twin.line', { repository })}
              says={line.notReadYet
                ? t('settings.lines.from.notReadYet')
                : line.branch
                ? t(`settings.lines.from.${line.source}`, { branch: line.branch, workspace: line.workspace })
                : t('settings.lines.from.none')}
              own={lineOwn}
              busy={busy}
              onClear={() => work.onLine?.(undefined)}
              editor={() => (
                <LineField
                  label={t('settings.lines.repositoryField', { repository })}
                  set={lineOwn ? line.branch : undefined}
                  placeholder={line.branch ?? t('settings.lines.unnamed')}
                  busy={busy}
                  clearable={false}
                  onSave={(branch) => work.onLine?.(branch)}
                />
              )}
            />
            {work.onLinePolicy && (
              <SettingRow label={t('projects.setup.linePolicy')}>
                <p className="m-0 text-small text-ink-soft">
                  <Inline text={line.policy ? policySays(t, line.policy) : t('projects.setup.linePolicyNone')} />
                </p>
                <div className="mt-2">
                  <LinePolicyField name={repository} set={line.policy} busy={busy} onSave={work.onLinePolicy} />
                </div>
              </SettingRow>
            )}
      </>
      )
    : null;
  const landingRow = work && lands?.is === 'version'
    ? <LandingSetting twin={t('projects.setup.twin.landing', { repository })} lands={lands} />
    : null;
  const commitsRow = commits && work?.onCommits
    ? (
            <Inheritable
              setHere={t('projects.setup.setHere')}
              label={t('settings.commits.label')}
              twin={t('settings.commits.twin.repository', { repository })}
              why={t('settings.commits.body')}
              says={commitRowSays(t, commitRule, { source: commits.source, workspace: commits.workspace })}
              own={commitsOwn}
              busy={busy}
              wide
              onClear={() => work.onCommits?.({ clear: true })}
              editor={() => (
                <CommitField
                  name={repository}
                  set={commitsOwn ? commitRule : undefined}
                  inherited={commitRule}
                  busy={busy}
                  onChange={(edit) => work.onCommits?.(edit)}
                />
              )}
            />
      )
    : null;
  const reviewRow = review && work?.onReview
    ? (
            <Inheritable
              setHere={t('projects.setup.setHere')}
              label={t('settings.review.label')}
              twin={t('settings.review.twin.repository', { repository })}
              why={t('settings.review.body')}
              says={reviewRowSays(t, review.rule, { source: review.source, workspace: review.workspace })}
              own={reviewOwn}
              busy={busy}
              wide
              onClear={() => work.onReview?.({ clear: true })}
              editor={() => (
                <ReviewField
                  name={repository}
                  owner="repository"
                  set={reviewOwn ? review.rule ?? undefined : undefined}
                  busy={busy}
                  onChange={(edit) => work.onReview?.(edit)}
                />
              )}
            />
      )
    : null;
  const opinionRow = opinion && work?.onOpinion
    ? (
            <Inheritable
              setHere={t('projects.setup.setHere')}
              label={t('settings.opinion.label')}
              twin={t('settings.opinion.twin.repository', { repository })}
              why={t('settings.opinion.body')}
              says={opinionRowSays(t, opinion.rule, { source: opinion.source, workspace: opinion.workspace })}
              own={opinionOwn}
              busy={busy}
              wide
              onClear={() => work.onOpinion?.({ clear: true })}
              editor={() => (
                <OpinionField
                  name={repository}
                  owner="repository"
                  set={opinionOwn ? opinion.rule ?? undefined : undefined}
                  busy={busy}
                  onChange={(edit) => work.onOpinion?.(edit)}
                />
              )}
            />
      )
    : null;
  const landedRow = landed
    ? <LandedRoute repository={repository} workspace={landed.workspace} start={landed.start} busy={busy} onChoose={landed.onChoose} />
    : null;
  const integrationRow = !gatesApart && landed?.start.integration && landed.onAcceptIntegration
    ? <IntegrationDeclaration repository={repository} integration={landed.start.integration} busy={busy} onAccept={landed.onAcceptIntegration} />
    : null;
  // FABRIC-PIPELINE1b: the heavy gates and the sessions' checks, the checks first. The heavy gates named are those its own file marks where it
  // marks some, else this machine's list, as `run-heavy` resolves them.
  const heavyNames = heavy ? (heavy.marks?.marks === true ? heavy.marks.names : heavy.gates.map((gate) => gate.name)) : [];
  const heavyChecksSaid = heavy?.checks ? sessionChecksSummary(t, heavy.checks.set) : null;
  const heavyRows = heavy
    ? (
        <>
          {heavy.checks && (
            <SessionChecksRow
              repository={repository}
              checks={heavy.checks.set}
              light={heavy.marks?.light ?? null}
              heavy={heavyNames}
              known={typeof heavy.marks?.marks === 'boolean'}
              busy={busy}
              onChange={heavy.checks.onChange}
            />
          )}
          <HeavyGates repository={repository} gates={heavy.gates} marks={heavy.marks} busy={busy} onSave={heavy.onSave} />
        </>
      )
    : null;
  const heavySaid = heavy ? [...(heavyChecksSaid ? [heavyChecksSaid] : []), heavySummary(t, heavy.gates, heavy.marks)].join(JOIN) : null;
  return { sections, lineRow, landingRow, commitsRow, reviewRow, opinionRow, landedRow, integrationRow, heavyRows, heavySaid };
}

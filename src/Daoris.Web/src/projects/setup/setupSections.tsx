import type * as React from 'react';
import type { ReactNode } from 'react';
import { AccessRows, accessSummary } from '../../settings/repository/Access';
import type { RepositoryAcross } from '../../settings/repository/Across';
import { HelperField, helperRowSays, type RepositoryHelpers, type HelperRule } from '../../settings/repository/Helpers';
import { HandOverField, handOverRowSays, type RepositoryHandOver, type HandOverRule } from '../../settings/repository/HandOver';
import { LanguageChoice, type LanguageOption, type RepositoryLanguage } from '../../settings/repository/Languages';
import { Segmented, SettingRow } from '../../design/ui';
import { RepositoryRules } from './RepositoryRules';
import { Inheritable } from './SetupParts';
import { StandingAnswer } from './StandingAnswer';
import { GitRemotes } from '../../settings/git/GitRemotes';
import { GitAccessRows, gitAccessSummary } from '../../settings/repository/GitAccess';
import { type TFunction } from 'i18next';
import type { AccessSetup, GitSetup, ReachSetup, SessionsSetup, SetupSectionId, WorkSetup } from './RepositorySetup';
import { CheckoutSwitch, JOIN, WritesInto } from './setupBits';

export function setupSections({ work, workParts, sections, lineRow, landingRow, commitsRow, reviewRow, opinionRow, landedRow, integrationRow, git, t, repository, busy, accessShown, heavyRows, heavySaid, sessions, sessionsParts, language, languageOwn, helpers, helperRule, helpersOwn, handOver, handOverRule, handOverOwn, standing, reach, reachParts, across, readOwn, writesTo, landingSaid, opinionSaid, reviewSaid, landedSaid, lineSaid, commitsSaid }: {
  work: WorkSetup | null | undefined;
  workParts: string[] | null | undefined;
  sections: { id: SetupSectionId; summary: string; body: React.ReactNode; }[];
  lineRow: React.JSX.Element | null;
  landingRow: React.JSX.Element | null;
  commitsRow: React.JSX.Element | null;
  reviewRow: React.JSX.Element | null;
  opinionRow: React.JSX.Element | null;
  landedRow: React.JSX.Element | null;
  integrationRow: React.JSX.Element | null;
  git: GitSetup | null | undefined;
  t: TFunction<"translation", undefined>;
  repository: string;
  busy: boolean;
  accessShown: AccessSetup | null;
  heavyRows: React.JSX.Element | null;
  heavySaid: string | null;
  sessions: SessionsSetup | null | undefined;
  sessionsParts: string[] | null | undefined;
  language: { resolved: RepositoryLanguage | null; inherited?: string | undefined; table: LanguageOption[]; onSet: (language: string | null) => void; } | undefined;
  languageOwn: boolean;
  helpers: RepositoryHelpers | null | undefined;
  helperRule: HelperRule;
  helpersOwn: boolean;
  handOver: RepositoryHandOver | null | undefined;
  handOverRule: HandOverRule;
  handOverOwn: boolean;
  standing: { says: string; at?: string | null | undefined; } | null;
  reach: ReachSetup | null | undefined;
  reachParts: string[] | null | undefined;
  across: RepositoryAcross | null | undefined;
  readOwn: boolean;
  writesTo: string[];
  landingSaid: string | null;
  opinionSaid: string | null;
  reviewSaid: string | null;
  landedSaid: string | null;
  lineSaid: string | null;
  commitsSaid: string | null;
}) {
  if (work && workParts && workParts.length > 0) {
    sections.push({
      id: 'work',
      summary: workParts.join(JOIN),
      body: <>{lineRow}{landingRow}{commitsRow}{reviewRow}{opinionRow}{landedRow}{integrationRow}</>,
    });
  }
  if (git && (git.remotes.length > 0 || git.access)) {
    sections.push({
      id: 'git',
      summary: [
        ...git.remotes.map((remote) => `${remote.name}: ${remote.serves === 'identity' ? remote.label : remote.serves === 'own'
          ? t('settings.git.own') : remote.serves === 'none' ? t('settings.git.none') : t('settings.git.remotes.local')}`),
        ...(git.access ? [gitAccessSummary(t, git.access.classes)] : []),
      ].join(JOIN),
      body: (
        <>
          {git.remotes.length > 0 && <GitRemotes remotes={git.remotes} identities={git.identities} acts={git.acts} />}
          {git.access && (
            <GitAccessRows
              owner="repository"
              name={repository}
              workspace={git.access.workspace}
              classes={git.access.classes}
              above={git.access.above}
              busy={busy}
              onChange={git.access.onChange}
            />
          )}
        </>
      ),
    });
  }
  if (accessShown) {
    sections.push({
      id: 'access',
      summary: accessSummary(t, accessShown.pauses, 'repository'),
      body: (
        <AccessRows
          owner="repository"
          name={repository}
          workspace={accessShown.workspace}
          pauses={accessShown.pauses}
          aboveFull={accessShown.aboveFull}
          busy={busy}
          setHere={t('projects.setup.setHere')}
          onChange={accessShown.onChange}
        />
      ),
    });
  }
  if (heavyRows && heavySaid !== null) sections.push({ id: 'heavy', summary: heavySaid, body: heavyRows });
  if (sessions && sessionsParts && sessionsParts.length > 0) {
    sections.push({
      id: 'sessions',
      summary: sessionsParts.join(JOIN),
      body: (
        <>
          {language && (
            <Inheritable
              setHere={t('projects.setup.setHere')}
              label={t('projects.language.label')}
              twin={t('projects.setup.twin.language', { repository })}
              why={t('projects.language.hint')}
              says={language.resolved?.source && language.resolved.language
                ? t(`settings.sessionLanguage.from.${language.resolved.source}`, {
                    name: language.resolved.name ?? language.resolved.language,
                  })
                : t('settings.sessionLanguage.from.none')}
              own={languageOwn}
              busy={busy}
              onClear={() => language.onSet(null)}
              editor={() => (
                <LanguageChoice
                  label={t('projects.language.field', { repository })}
                  set={languageOwn ? language.resolved?.language : undefined}
                  inherited={language.inherited}
                  table={language.table}
                  onChoose={(code) => language.onSet(code ?? null)}
                />
              )}
            />
          )}
          {helpers && sessions.onHelpers && (
            <Inheritable
              setHere={t('projects.setup.setHere')}
              label={t('settings.helpers.label')}
              twin={t('settings.helpers.twin.repository', { repository })}
              why={t('settings.helpers.body')}
              says={helperRowSays(t, helperRule, { source: helpers.source, workspace: helpers.workspace })}
              own={helpersOwn}
              busy={busy}
              wide
              onClear={() => sessions.onHelpers?.({ clear: true })}
              editor={() => (
                <HelperField
                  name={repository}
                  set={helpersOwn ? helperRule : undefined}
                  inherited={helperRule}
                  busy={busy}
                  onChange={(edit) => sessions.onHelpers?.(edit)}
                />
              )}
            />
          )}
          {handOver && sessions.onHandOver && (
            <Inheritable
              setHere={t('projects.setup.setHere')}
              label={t('settings.handOver.label')}
              twin={t('settings.handOver.twin.repository', { repository })}
              why={t('settings.handOver.body')}
              says={handOverRowSays(t, handOverRule, { source: handOver.source, workspace: handOver.workspace })}
              own={handOverOwn}
              busy={busy}
              onClear={() => sessions.onHandOver?.({ clear: true })}
              editor={() => (
                <HandOverField
                  name={repository}
                  set={handOverOwn ? handOverRule : undefined}
                  inherited={handOverRule}
                  busy={busy}
                  onChange={(edit) => sessions.onHandOver?.(edit)}
                />
              )}
            />
          )}
          {sessions.onStanding && (
            <StandingAnswer
              repository={repository}
              says={standing?.says ?? null}
              at={standing?.at ?? null}
              busy={busy}
              onSave={sessions.onStanding}
            />
          )}
        </>
      ),
    });
  }
  if (reach && reachParts && reachParts.length > 0) {
    sections.push({
      id: 'reach',
      summary: reachParts.join(JOIN),
      body: (
        <>
          {across && reach.onRead && (
            <Inheritable
              setHere={t('projects.setup.setHere')}
              label={t('projects.setup.read')}
              twin={t('projects.setup.twin.read', { repository })}
              says={[
                across.read ? t('settings.across.readBy', { workspace: across.workspace }) : t('settings.across.readByNone'),
                t(`settings.across.from.${across.source}`, { workspace: across.workspace }),
                ...(across.checkout ? [] : [t('settings.across.noCheckout')]),
              ].join(t('projects.setup.sentenceJoin'))}
              own={readOwn}
              busy={busy}
              onClear={() => reach.onRead?.(undefined)}
              editor={() => (
                <Segmented<'on' | 'off'>
                  label={t('settings.across.repositoryField', { repository })}
                  value={across.read ? 'on' : 'off'}
                  options={[
                    { value: 'on', label: t('settings.across.on') },
                    { value: 'off', label: t('settings.across.off') },
                  ]}
                  onChange={(choice) => !busy && reach.onRead?.(choice === 'on')}
                />
              )}
            />
          )}
          {across && reach.onWrite && (
            <WritesInto
              repository={repository}
              writesTo={writesTo}
              candidates={reach.candidates ?? []}
              busy={busy}
              onWrite={reach.onWrite}
            />
          )}
          {reach.checkout && (
            <CheckoutSwitch checkout={reach.checkout} busy={busy} />
          )}
          {reach.rules && (
            <SettingRow label={t('projects.setup.rules')} hint={t('projects.setup.twin.rules', { repository })}>
              <RepositoryRules rules={reach.rules} busy={busy} onAdd={reach.onAddRule} onRemove={reach.onRemoveRule} />
            </SettingRow>
          )}
        </>
      ),
    });
  }

  // The place's Workflow folds (SETUPUX1o), each a row above drawn apart under its own name, in the order the page draws them.
  const folds: [SetupSectionId, string | null, ReactNode][] = [
    ['landing', landingSaid, landingRow],
    ['opinion', opinionSaid, opinionRow],
    ['look', reviewSaid, reviewRow],
    ['landedRoute', landedSaid, landedRow],
    ['line', lineSaid, lineRow],
    ['more', [commitsSaid, heavySaid].filter((each): each is string => each !== null).join(JOIN) || null, commitsRow || heavyRows ? <>{commitsRow}{heavyRows}</> : null],
  ];
  for (const [id, summary, body] of folds) if (summary !== null && body) sections.push({ id, summary, body });
}

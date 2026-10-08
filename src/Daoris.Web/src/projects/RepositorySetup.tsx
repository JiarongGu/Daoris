import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { RepositoryAcross } from '../settings/Across';
import type { RuleListName } from '../settings/AgentRules';
import { type Accepting, AcceptingNote, LandingField, type LandingRule, type RepositoryLanding } from '../settings/Landings';
import { LanguageChoice, type LanguageOption, type RepositoryLanguage } from '../settings/Languages';
import { LineField, type RepositoryLine } from '../settings/Lines';
import { type RepositoryReview, type ReviewEdit, ReviewField, reviewRowSays, reviewSummary } from '../settings/Reviews';
import { Button, CheckField, Chip, Icon, Segmented, SelectField, SettingRow } from '../ui';
import { RepositoryRules, type RuleLists } from './RepositoryRules';
import { Inheritable, landingSays, SetupSection } from './SetupParts';
import { StandingAnswer } from './StandingAnswer';

/** The four sections of a repository's Setup (UX6f, D150 §4.2), in the order the page draws them. */
export type SetupSectionId = 'driving' | 'work' | 'sessions' | 'reach';

/** This machine's driver's standing choices for one repository (D46 §6), and the presses that change them. */
export type Driving = {
  drivable: boolean;
  held: boolean;
  ownTree: boolean;
  onDrive: (drivable: boolean) => void;
  onHold: (held: boolean) => void;
  onTrees: (ownTree: boolean) => void;
  /** What driving here does that the choice alone does not say: a direct door leaves a quest here sitting (INT3c). */
  note?: string;
};

/** Its line and how its work lands (WSR1, WSR2), as the driver resolves them, and the presses that set or clear them. */
export type WorkSetup = {
  /** Its line as the driver resolved it, and what said so; absent where the driver has not answered. */
  line?: RepositoryLine | null;
  onLine?: (branch: string | undefined) => void;
  /** How its work lands as the driver chose it; absent on a shell older than it. */
  landing?: RepositoryLanding | null;
  /** What stands under a rule of its own: its workspace's rule, else Daoris's merge. */
  landingAbove?: LandingRule;
  /** The plugins here that land work, which a branch rule may hand its branch to (D100). */
  landers?: string[];
  onLanding?: (rule: LandingRule | undefined) => void;
  /**
   * Where its work is reviewed before it lands (REVIEWENV1a, D154 point 2), as the driver resolves it: its own, its
   * workspace's, or none set; absent on a shell older than it, and no row is offered.
   */
  review?: RepositoryReview | null;
  onReview?: (edit: ReviewEdit) => void;
};

/** The language its sessions write to the person in (LANG1c) and its standing answer (KNOWUSE1b). */
export type SessionsSetup = {
  /** As the driver resolved it, its workspace's by the table's name, and the table; absent on a shell older than it. */
  language?: { resolved: RepositoryLanguage | null; inherited?: string; table: LanguageOption[]; onSet: (language: string | null) => void };
  /** Its standing answer, the person's words and when they set them; null for none. */
  standing?: { says: string; at?: string | null } | null;
  /** Keep a standing answer, or with null clear it; absent on a shell older than it, and nothing is offered. */
  onStanding?: (says: string | null) => void;
};

/** Whether agents outside it read its checkout, what its sessions also write into (READ1, D107), and its rules (PERM1). */
export type ReachSetup = {
  /** As the driver resolved it; absent on a shell older than it. */
  across?: RepositoryAcross | null;
  /** What stands under a reading of its own: its workspace's, else Daoris's, which reads. */
  readAbove?: { read: boolean; source: 'workspace' | 'default' };
  /** The other repositories of its workspace it does not write into yet: a relationship never crosses a workspace (D48). */
  candidates?: string[];
  onRead?: (read: boolean | undefined) => void;
  onWrite?: (to: string, allow: boolean) => void;
  /** Claude Code's rules for this repository alone; absent where the rules were not answered. */
  rules?: RuleLists | null;
  onAddRule?: (list: RuleListName, rule: string, added: () => void) => void;
  onRemoveRule?: (rule: string) => void;
};

export type RepositorySetupProps = {
  repository: string;
  /** Driving, where it has somewhere to start (INT3c): absent for one with no root here. */
  driving?: Driving | null;
  work?: WorkSetup | null;
  /** Its sessions' language and standing answer, where it can be driven. */
  sessions?: SessionsSetup | null;
  reach?: ReachSetup | null;
  /** A change on its way: every control waits for it. */
  busy?: boolean;
  /** A section a door asked to see open, beside those that open on their own. */
  open?: SetupSectionId;
};

const JOIN = ' · ';

/**
 * **A repository's Setup** (UX6f, D150 §4.2): every setting the repository holds on this machine, in four sections, each
 * folding to a line that names its values. *Driving* (drive, hold, a tree per session), *Line and landing* (its line, how
 * its work lands; the design's *Work*, which the catalogue keeps for no name since the view it named was retired),
 * *Sessions* (the language they write in, the standing answer) and *Reach* (read by agents outside it, what its sessions
 * also write into, Claude Code's rules here).
 *
 * @remarks
 * **What Daoris decides folds; what the person decides shows** (§1 rule 4). A folded section's line names each value, one
 * at its workspace's or Daoris's default marked as such. A section opens on its own where it holds a value set for this
 * repository, which carries its *Clear*: a line, a landing rule, a session language or a reading of its own, a standing
 * answer, a relationship or a rule. *Driving* opens only on a press: its three are yes or no for this repository alone,
 * with nothing above them to fall back to, so none carries a *Clear*, and the page's header already says *drives here*
 * or *held*.
 *
 * **A value from above says so and offers *Set for this repository***, which opens its control in place; one set here
 * carries *Clear*, which hands it back to what stands above. **Each row's hint is its terminal twin** (D41 §4, D50):
 * `daoris driver <verb> <repository>`, the same file either door edits, so a refusal is the driver's sentence.
 *
 * A molecule: every state is reached by its props, and every press goes out.
 */
export function RepositorySetup({ repository, driving, work, sessions, reach, busy = false, open }: RepositorySetupProps) {
  const { t } = useTranslation();
  const marked = (value: string, source: 'repository' | 'workspace' | 'daoris') =>
    source === 'repository' ? value : t(`projects.setup.marked.${source}`, { value });

  // Driving: its three, in the words the session list uses.
  const drivingLine = driving && [
    t(driving.drivable ? 'projects.setup.summary.driven' : 'projects.setup.summary.notDriven'),
    t(driving.ownTree ? 'projects.setup.summary.ownTree' : 'projects.setup.summary.sharedTree'),
    ...(driving.drivable ? [t(driving.held ? 'projects.setup.summary.held' : 'projects.setup.summary.notHeld')] : []),
  ].join(JOIN);

  // Work: its line, then how its work lands, each with where it came from.
  const line = work?.line;
  const lineOwn = line?.source === 'repository';
  const landing = work?.landing;
  const landingOwn = landing?.source === 'repository';
  // Where its work is reviewed before it lands (REVIEWENV1a): its own, its workspace's, or none set, which is Daoris's.
  const review = work?.onReview ? work.review : undefined;
  const reviewOwn = review?.source === 'repository';
  const workParts = work && [
    ...(line
      ? [marked(
          line.branch ? t('projects.setup.summary.line', { branch: line.branch }) : t('projects.setup.summary.noLine'),
          line.source === 'repository' ? 'repository' : line.source === 'workspace' ? 'workspace' : 'daoris')]
      : []),
    ...(landing ? [marked(landingSays(t, landing), landing.source === 'default' ? 'daoris' : landing.source)] : []),
    ...(review ? [marked(reviewSummary(t, review.rule), review.rule && review.source ? review.source : 'daoris')] : []),
  ];

  // Sessions: the language they write in, then the standing answer.
  const language = sessions?.language;
  const languageOwn = language?.resolved?.source === 'repository';
  const standing = sessions?.standing ?? null;
  const sessionsParts = sessions && [
    ...(language
      ? [language.resolved?.source && language.resolved.language
          ? marked(language.resolved.name ?? language.resolved.language, language.resolved.source)
          : marked(t('projects.setup.summary.noLanguage'), 'daoris')]
      : []),
    ...(sessions.onStanding
      ? [standing ? t('projects.setup.summary.standing', { count: standing.says.split('\n').length }) : t('projects.setup.summary.noStanding')]
      : []),
  ];

  // Reach: whether it is read, what it writes into, its rules.
  const across = reach?.across;
  const readOwn = across?.source === 'repository';
  const writesTo = across?.writesTo ?? [];
  const ruleCount = reach?.rules ? reach.rules.allow.length + reach.rules.ask.length + reach.rules.deny.length : 0;
  const reachParts = reach && [
    ...(across
      ? [
          marked(
            t(across.read ? 'projects.setup.summary.read' : 'projects.setup.summary.unread'),
            across.source === 'default' ? 'daoris' : across.source,
          ),
          writesTo.length > 0
            ? t('projects.setup.summary.writes', { repositories: writesTo.join(t('projects.setup.summary.listJoin')) })
            : t('projects.setup.summary.writesNone'),
        ]
      : []),
    ...(reach.rules ? [ruleCount > 0 ? t('projects.setup.summary.rules', { count: ruleCount }) : t('projects.setup.summary.noRules')] : []),
  ];

  // What opens on its own: a section holding a value set for this repository.
  const opensItself: Record<SetupSectionId, boolean> = {
    driving: false,
    work: lineOwn || landingOwn || reviewOwn,
    sessions: languageOwn || standing !== null,
    reach: readOwn || writesTo.length > 0 || ruleCount > 0,
  };

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
  if (work && workParts && workParts.length > 0) {
    sections.push({
      id: 'work',
      summary: workParts.join(JOIN),
      body: (
        <>
          {line && work.onLine && (
            <Inheritable
              setHere={t('projects.setup.setHere')}
              label={t('projects.setup.line')}
              twin={t('projects.setup.twin.line', { repository })}
              says={line.branch
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
          )}
          {landing && work.onLanding && (
            <LandingSetting
              repository={repository}
              landing={landing}
              above={work.landingAbove ?? { form: 'merge' }}
              landers={work.landers ?? []}
              busy={busy}
              onLanding={work.onLanding}
            />
          )}
          {review && work.onReview && (
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
          )}
        </>
      ),
    });
  }
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
          {reach.rules && (
            <SettingRow label={t('projects.setup.rules')} hint={t('projects.setup.twin.rules', { repository })}>
              <RepositoryRules rules={reach.rules} busy={busy} onAdd={reach.onAddRule} onRemove={reach.onRemoveRule} />
            </SettingRow>
          )}
        </>
      ),
    });
  }

  return (
    <div className="grid">
      {sections.map((section) => (
        <SetupSection
          // Keyed by the repository, so another repository's page opens its own sections afresh.
          key={`${repository}:${section.id}`}
          title={t(`projects.setup.section.${section.id}`)}
          summary={section.summary}
          startsOpen={opensItself[section.id] || open === section.id}
        >
          {section.body}
        </SetupSection>
      ))}
    </div>
  );
}

/** How its work lands: an `Inheritable` whose control is the rule's own, and what accepting automatically gives. */
function LandingSetting({ repository, landing, above, landers, busy, onLanding }: {
  repository: string;
  landing: RepositoryLanding;
  above: LandingRule;
  landers: string[];
  busy: boolean;
  onLanding: (rule: LandingRule | undefined) => void;
}) {
  const { t } = useTranslation();
  const [accepting, setAccepting] = useState<Accepting>(null);
  const own = landing.source === 'repository';
  const said = landing.source === 'default'
    ? t('settings.landing.from.default')
    : t(`settings.landing.from.${landing.source}.${landing.form === 'branch' ? 'branch' : 'merge'}`, {
        pattern: landing.pattern, workspace: landing.workspace,
      });
  const says = [
    said,
    ...(landing.form === 'branch' && landing.plugin ? [t('settings.landing.byPlugin', { plugin: landing.plugin })] : []),
    ...(landing.form === 'branch' && landing.autoAccept ? [t('settings.landing.byAuto')] : []),
  ].join(t('projects.setup.sentenceJoin'));
  return (
    <Inheritable
      setHere={t('projects.setup.setHere')}
      label={t('projects.setup.landing')}
      twin={t('projects.setup.twin.landing', { repository })}
      says={says}
      own={own}
      busy={busy}
      wide
      onClear={() => onLanding(undefined)}
      editor={() => (
        <LandingField
          name={repository}
          set={own ? landing : undefined}
          inherited={own ? above : landing}
          landers={landers}
          busy={busy}
          clearable={false}
          alignStart
          onSave={(rule) => onLanding(rule)}
          onAccepting={setAccepting}
        />
      )}
    >
      <AcceptingNote accepting={accepting} />
    </Inheritable>
  );
}

/**
 * What its sessions may also write into (D107): each declared relationship with its take-back, and a choice of the other
 * repositories of its workspace. A declaration is the person's standing say-so, in one direction.
 */
function WritesInto({ repository, writesTo, candidates, busy, onWrite }: {
  repository: string;
  writesTo: string[];
  candidates: string[];
  busy: boolean;
  onWrite: (to: string, allow: boolean) => void;
}) {
  const { t } = useTranslation();
  return (
    <SettingRow
      label={t('projects.setup.writes')}
      hint={t('projects.setup.twin.writes', { repository })}
      control={candidates.length > 0 && (
        <SelectField
          value=""
          disabled={busy}
          ariaLabel={t('settings.across.addWrite', { repository })}
          placeholder={t('settings.across.addWritePlaceholder')}
          options={candidates.map((name) => ({ value: name, label: name }))}
          onChange={(to) => to && onWrite(to, true)}
        />
      )}
    >
      {writesTo.length === 0
        ? <p className="m-0 text-small text-ink-soft">{t('settings.across.writesNone')}</p>
        : (
          <ul aria-label={t('settings.across.writesIntoList', { repository })} className="m-0 flex list-none flex-wrap gap-1.5 p-0">
            {writesTo.map((to) => (
              <li key={to} className="inline-flex items-center gap-0.5">
                <Chip>{to}</Chip>
                <Button
                  variant="ghost"
                  disabled={busy}
                  aria-label={t('settings.across.stopWriting', { repository, to })}
                  onClick={() => onWrite(to, false)}
                >
                  <Icon name="x" size={12} />
                </Button>
              </li>
            ))}
          </ul>
        )}
    </SettingRow>
  );
}

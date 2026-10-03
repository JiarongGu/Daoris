import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { Registration, Repository } from '../api';
import { ago, figure } from '../format';
import type { RepositoryLine } from '../settings/Lines';
import { Button, Chip, Icon, Inline, Prose, Tip, WhyGlyph } from '../ui';
import { type MainNotice, PageHead, PageSection, ViewMain } from '../work/ViewMain';
import { DriverChoices } from './DriverChoices';
import { RepositoryMarks } from './ProjectList';
import { StandingAnswer } from './StandingAnswer';

/** One fact of a repository: its label in the page's label column, its content wrapping beside it (POLISH4). */
function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <>
      <dt className="text-meta text-ink-faint">{label}</dt>
      <dd className="m-0 flex min-w-0 flex-wrap items-baseline gap-1.5">{children}</dd>
    </>
  );
}

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
  /** Its standing answer on this machine (KNOWUSE1b), the person's words and when they set them; null for none. */
  standing?: { says: string; at?: string | null } | null;
  /** Keep a standing answer, or with null clear it — absent on a shell older than it, and nothing is offered. */
  onStanding?: (says: string | null) => void;
};

/**
 * **A repository's page** (FRAME1e, D118 §2): what its card said, in Repositories' main area beside the list. Its
 * header holds its name, its standing, its summary as its one line, and its acts: the door to its code map (MAP3a) and
 * *Manage*. Then its facts (what the index holds, the commit it was fed from, its workspace, its line, its unlanded
 * branches), its declaration, this machine's driving row, and for one not adopted the steps to adopt.
 *
 * @remarks
 * **A molecule**: every state is reached by its props, and every press goes out.
 *
 * - **Adoption is the repository's own act** (D31, D32): one not adopted is offered the steps as text, never a button,
 *   and none of adoption's own acts, like *Manage*, which writes its declaration into it.
 * - **An absent act is absent, never disabled**: *Manage* and the driving row only where a shell answers (D48 §7), the
 *   driving row only where there is somewhere to start (INT3c).
 * - **A page never prints a machine path it was answered** (D47 §4): a checkout here is a mark, never its folder.
 */
export function ProjectPage({
  registration, counts, line, unlanded = 0, here, driving, onManage, onOpenCode,
}: {
  registration: Registration;
  /** What the index holds of it: absent where it holds nothing. */
  counts?: Repository;
  /** Its line as the driver resolves it (WSR2): a shell's, absent in a browser. */
  line?: RepositoryLine | null;
  /** How many session branches hold work no branch of the person's holds (WSR3, D88). */
  unlanded?: number;
  /** Whether it has a checkout on this machine: absent where nothing can tell, a browser. */
  here?: boolean;
  /** This machine's driving row, where a shell answers and it can be driven. */
  driving?: Driving | null;
  /** Manage its declaration, its wiring and its retirement (D48 §7): a shell's, and an adopter's. */
  onManage?: () => void;
  /** Open its code map, one level into the Map (MAP3a). */
  onOpenCode?: () => void;
}) {
  const { t } = useTranslation();
  const { repository, adopted, summary } = registration;
  const indexed = counts && counts.total > 0 ? counts : null;
  // One sentence for "the index holds nothing of this", whether absent from the index or present with none.
  const index = indexed
    ? adopted
      ? t('projects.entries', {
          // `count` picks the plural form; the formatted string is what is shown.
          count: indexed.total,
          total: figure(indexed.total),
          local: figure(indexed.local),
          canonical: figure(indexed.canonical),
        })
      : t('projects.outside.readable', { count: indexed.total, total: figure(indexed.total) })
    : t('projects.nothingIndexed');
  const declared = registration.owns.length > 0 || registration.accepts.length > 0 || registration.packs.length > 0;

  const acts = (onOpenCode || onManage) && (
    <>
      {onOpenCode && (
        <Button variant="ghost" onClick={onOpenCode}><Icon name="map" size={14} />{t('projects.page.codeMap')}</Button>
      )}
      {onManage && <Button onClick={onManage}>{t('projects.manage.open')}</Button>}
    </>
  );

  const head = (
    <PageHead
      title={repository}
      pills={(
        <RepositoryMarks
          drivable={driving?.drivable}
          held={driving?.held}
          adopted={adopted}
          here={here}
        />
      )}
      line={adopted && summary ? summary : undefined}
      acts={acts || undefined}
    />
  );

  return (
    <ViewMain header={head}>
      {adopted && !summary && (
        /* Addressable regardless — adoption gates addressing, declaration does not (D34) — but an asker deserves to
           know they would be guessing. */
        <p className="m-0 mb-4 max-w-prose border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
          <Inline text={t('projects.undeclared')} />
        </p>
      )}

      <dl className="m-0 grid grid-cols-[max-content_1fr] items-baseline gap-x-4 gap-y-2">
        {/* One sentence for "the index holds nothing of this": the deployed family read "0 entries", "nothing indexed
            yet" and "—" for the same fact. */}
        <Fact label={t('projects.page.index')}>
          <span className="font-mono text-small tabular-nums text-ink-soft">{index}</span>
        </Fact>
        {counts?.fed && (
          /* Where this deployment's copy came from (D48 §6). Shown rather than implied: the index is a claim about a
             commit, and a person who cannot see which commit has no way to tell a current view from one a machine
             stopped feeding a month ago. */
          <Fact label={t('projects.fed')}>
            <Tip content={t('projects.fedTip', {
              commit: counts.fed.commit,
              branch: counts.fed.branch,
              origin: counts.fed.origin ?? t('projects.fedUnknownOrigin'),
            })}
            >
              <span className="font-mono text-small text-ink-soft">{counts.fed.shortCommit} · {ago(counts.fed.committedAt)}</span>
            </Tip>
          </Fact>
        )}
        {registration.workspace && (
          /* Which workspace this one shares with (D48). Shown rather than assumed: a machine holding two workspaces
             would otherwise present them as one family. */
          <Fact label={t('projects.workspace')}>
            <Tip content={t('projects.workspaceTip')}><Chip>{registration.workspace}</Chip></Tip>
          </Fact>
        )}
        {line?.branch && (
          /* The branch this machine grows its work here from and lands it on (WSR2), and what said so — Settings →
             Workspace is where it is set. */
          <Fact label={t('projects.line')}>
            <span className="text-small text-ink-soft">
              <Inline text={t(`settings.lines.from.${line.source}`, { branch: line.branch, workspace: line.workspace })} />
            </span>
          </Fact>
        )}
        {unlanded > 0 && (
          <Fact label={t('projects.unlandedLabel')}>
            <span className="text-small text-warn">{t('projects.unlanded', { count: unlanded })}</span>
          </Fact>
        )}
      </dl>

      {adopted && declared && (
        <PageSection title={t('projects.page.declaration')}>
          {/* The labels are a column and the chips wrap in their own, so a second line of chips lines up under the
              first (POLISH4). */}
          <dl className="m-0 grid grid-cols-[max-content_1fr] items-baseline gap-x-4 gap-y-2">
            {registration.owns.length > 0 && (
              <Fact label={t('projects.owns')}>{registration.owns.map((item) => <Chip key={item}>{item}</Chip>)}</Fact>
            )}
            {registration.accepts.length > 0 && (
              <Fact label={t('projects.accepts')}>{registration.accepts.map((item) => <Chip key={item} accent>{item}</Chip>)}</Fact>
            )}
            {registration.packs.length > 0 && (
              <Fact label={t('projects.packs')}>{registration.packs.map((item) => <Chip key={item}>{item}</Chip>)}</Fact>
            )}
          </dl>
        </PageSection>
      )}

      {driving && (
        /* The person's standing choices for THIS machine's driver (D46 §6): only where a shell answers. */
        <PageSection title={t('projects.page.machine')}>
          <DriverChoices
            drivable={driving.drivable}
            held={driving.held}
            ownTree={driving.ownTree}
            onDrive={driving.onDrive}
            onHold={driving.onHold}
            onTrees={driving.onTrees}
            note={driving.note}
          />
          {driving.onStanding && (
            /* What the person says holds for every session here (KNOWUSE1b), beside the other choices a session's
               instruction is composed from. */
            <StandingAnswer says={driving.standing?.says ?? null} at={driving.standing?.at ?? null} onSave={driving.onStanding} />
          )}
        </PageSection>
      )}

      {!adopted && (
        <PageSection title={t('projects.page.adopt')}>
          {/* The reasoning is one press away, not eight lines read before the steps on every visit (UX5 U36). */}
          <div className="flex items-start gap-1.5">
            <Prose>{t('projects.outside.lead')}</Prose>
            <span className="mt-0.5 shrink-0"><WhyGlyph why={t('projects.outside.body')} /></span>
          </div>
          {/* A sentence with its commands as code — it was all monospace, prose included (POLISH4). */}
          <p className="mt-3 mb-0 max-w-prose rounded-control bg-accent-soft px-3 py-2.5 text-small">
            <Inline text={t('projects.outside.join')} />
          </p>
        </PageSection>
      )}
    </ViewMain>
  );
}

/**
 * Repositories' main area with no page to show (D118 §3b): **nothing chosen** says how to choose and offers the list's
 * `＋` where there is one; **gone** says the chosen repository is no longer here; **loading** is skeleton rows, never
 * the empty state; and a registry that never answered says its sentence in place.
 */
export function ProjectsMainNotice({ state, sentence, action }: {
  state: 'none' | 'gone' | 'loading' | 'unanswered';
  /** The sentence for a registry that has never had an answer. */
  sentence?: string;
  /** The list's `＋`, offered with nothing chosen: a shell's. */
  action?: { label: string; onAct: () => void };
}) {
  const { t } = useTranslation();
  if (state === 'unanswered') return <ViewMain><Prose><Inline text={sentence ?? ''} /></Prose></ViewMain>;
  const none: MainNotice = {
    icon: 'projects',
    headline: t('projects.none.headline'),
    body: t('projects.none.body'),
    action: action && <Button onClick={action.onAct}>{action.label}</Button>,
  };
  const gone: MainNotice = { icon: 'projects', headline: t('projects.gone.headline'), body: t('projects.gone.body') };
  return <ViewMain state={state} none={none} gone={gone} />;
}

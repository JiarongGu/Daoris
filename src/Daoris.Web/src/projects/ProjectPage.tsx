import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Registration, Repository } from '../api';
import { ago, figure } from '../format';
import type { RepositoryLine } from '../settings/Lines';
import { Button, Chip, Icon, Inline, Prose, Tip, WhyGlyph } from '../ui';
import { type MainNotice, PageHead, PageSection, PageTabs, ViewMain } from '../work/ViewMain';
import { RepositoryMarks } from './ProjectList';
import { RepositorySetup, type RepositorySetupProps, type SetupSectionId } from './RepositorySetup';
import type { ProjectTab } from './tabs';

export type { Driving } from './RepositorySetup';

/** One fact of a repository: its label in the page's label column, its content wrapping beside it (POLISH4). */
function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <>
      <dt className="text-meta text-ink-faint">{label}</dt>
      <dd className="m-0 flex min-w-0 flex-wrap items-baseline gap-1.5">{children}</dd>
    </>
  );
}

/**
 * **A repository's page** (FRAME1e, D118 §2): in Repositories' main area beside the list. Its header holds its name, its
 * standing, its summary as its one line, and its acts: the door to its code map (MAP3a) and *Manage*. Beneath, its tabs
 * (UX6f, D150 §4.2): **Details**, its facts (what the index holds, the commit it was fed from, its workspace, its line, its
 * unlanded branches), its declaration and for one not adopted the steps to adopt; and **Setup**, every setting it holds
 * on this machine (`RepositorySetup`).
 *
 * @remarks
 * **A molecule**: every state is reached by its props, and every press goes out. The tab shown is its holder's,
 * remembered per view, so a door can open the page at Setup.
 *
 * - **A setting has one home, its Setup** (§1 rule 1): Details shows its line read-only, with a door to Setup at *Line
 *   and landing*.
 * - **A browser gets Details alone** (D47 §4): setup is this machine's, and one tab is no tabs, so it has no tab row.
 * - **Adoption is the repository's own act** (D31, D32): one not adopted is offered the steps as text, never a button,
 *   and none of adoption's own acts, like *Manage*, which writes its declaration into it.
 * - **An absent act is absent, never disabled**: *Manage* and Setup only where a shell answers (D48 §7), Setup's
 *   driving only where there is somewhere to start (INT3c).
 * - **A page never prints a machine path it was answered** (D47 §4): a checkout here is a mark, never its folder.
 */
export function ProjectPage({
  registration, counts, line, unlanded = 0, here, drivable, held, setup, tab = 'details', onTab, onManage, onOpenCode,
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
  /** This machine's driver may start work here, which its header marks: absent where no shell answers. */
  drivable?: boolean;
  /** The person has stopped the driver starting anything new here, which its header marks. */
  held?: boolean;
  /** Its Setup on this machine: a shell's. Absent, the page is Details alone, with no tab row. */
  setup?: RepositorySetupProps | null;
  /** The tab shown, its holder's to remember; Details where it is not handed. */
  tab?: ProjectTab;
  onTab?: (tab: ProjectTab) => void;
  /** Manage its declaration, its wiring and its retirement (D48 §7): a shell's, and an adopter's. */
  onManage?: () => void;
  /** Open its code map, one level into the Map (MAP3a). */
  onOpenCode?: () => void;
}) {
  const { t } = useTranslation();
  // The section a door on Details asked Setup to open: the line's opens Line and landing.
  const [asked, setAsked] = useState<SetupSectionId | null>(null);
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
  // A right-click on the page offers its header's acts, as its buttons press them, then its name (CTX1, D138 §4).
  const menu = {
    label: repository,
    acts: [
      ...(onOpenCode ? [{ id: 'codeMap', label: t('projects.page.codeMap'), icon: 'map' as const, onSelect: onOpenCode }] : []),
      ...(onManage ? [{ id: 'manage', label: t('projects.manage.open'), icon: 'settings' as const, onSelect: onManage }] : []),
      { id: 'copy', label: t('contextMenu.act.copyRepository'), icon: 'copy' as const, copy: repository },
    ],
  };

  const head = (
    <PageHead
      title={repository}
      pills={<RepositoryMarks drivable={drivable} held={held} adopted={adopted} here={here} />}
      line={adopted && summary ? summary : undefined}
      acts={acts || undefined}
    />
  );

  // The line's door opens Setup at Line and landing, where the line is set.
  const toSetup = setup?.work?.onLine && onTab ? () => { setAsked('work'); onTab('setup'); } : undefined;

  const details = (
    <>
      {adopted && !summary && (
        /* Addressable regardless — adoption gates addressing, declaration does not (D34) — but an asker deserves to
           know they would be guessing. */
        <p className="m-0 mb-4 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
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
          /* The branch this machine grows its work here from and lands it on (WSR2), and what said so: read-only here,
             since a setting has one home (UX6f, D150 §1), and its door opens Setup at Line and landing, where it is set. */
          <Fact label={t('projects.line')}>
            <span className="text-small text-ink-soft">
              <Inline text={t(`settings.lines.from.${line.source}`, { branch: line.branch, workspace: line.workspace })} />
            </span>
            {toSetup && (
              <Button variant="ghost" className="text-small" onClick={toSetup}>
                {t('projects.lineDoor')}
              </Button>
            )}
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

      {!adopted && (
        <PageSection title={t('projects.page.adopt')}>
          {/* The reasoning is one press away, not eight lines read before the steps on every visit (UX5 U36). */}
          <div className="flex items-start gap-1.5">
            <Prose>{t('projects.outside.lead')}</Prose>
            <span className="mt-0.5 shrink-0"><WhyGlyph why={t('projects.outside.body')} /></span>
          </div>
          {/* A sentence with its commands as code — it was all monospace, prose included (POLISH4). */}
          <p className="mt-3 mb-0 rounded-control bg-accent-soft px-3 py-2.5 text-small">
            <Inline text={t('projects.outside.join')} />
          </p>
        </PageSection>
      )}
    </>
  );

  return (
    <ViewMain header={head} menu={menu}>
      {setup && onTab
        ? (
          <PageTabs<ProjectTab>
            label={t('projects.tab.list', { repository })}
            tabs={[
              { id: 'details', label: t('projects.tab.details') },
              { id: 'setup', label: t('projects.tab.setup') },
            ]}
            chosen={tab}
            // A tab chosen by its own press opens Setup as it opens itself; only the line's door asks for Work.
            onChoose={(next) => { setAsked(null); onTab(next); }}
          >
            {tab === 'setup' ? <RepositorySetup {...setup} open={asked ?? setup.open} /> : details}
          </PageTabs>
        )
        : details}
    </ViewMain>
  );
}

/**
 * Repositories' main area with no page to show (D118 §3b): **nothing chosen** says how to choose and offers the list's
 * `＋` where there is one; **gone** says the chosen repository, or workspace, is no longer here; **loading** is skeleton
 * rows, never the empty state; and a registry that never answered says its sentence in place.
 */
export function ProjectsMainNotice({ state, of = 'repository', sentence, action }: {
  state: 'none' | 'gone' | 'loading' | 'unanswered';
  /** What has gone: the chosen repository, or the chosen workspace (UX6g). */
  of?: 'repository' | 'workspace';
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
  const gone: MainNotice = of === 'workspace'
    ? { icon: 'projects', headline: t('projects.workspace.gone.headline'), body: t('projects.workspace.gone.body') }
    : { icon: 'projects', headline: t('projects.gone.headline'), body: t('projects.gone.body') };
  return <ViewMain state={state} none={none} gone={gone} />;
}

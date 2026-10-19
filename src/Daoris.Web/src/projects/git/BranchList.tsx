import { type ReactNode, useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { ago, stamp } from '../../locales/format';
import { ExternalLink } from '../../design/links';
import { ListMore } from '../../work/ListPane';
import { Button, Inline, Pill, Prose, SESSION_TONE, type ShownState } from '../../design/ui';
import { kindOf, landedStanding, pullRequestSaid, standing } from './branchStanding';

/** Yours are folded after five (the built-in Git design §2.2): the rest are one press away. */
export const YOURS_SHOWN = 5;

/** A landed branch as the list says it (D102): how it stands to its own name on origin, and its pull request. */
export type ListedLanded = {
  /** One of the driver's `GitOrigin`: `in-step` · `ahead` · `behind` · `diverged` · `gone` · `not-pushed` · `unknown`. */
  origin: string;
  pushed: boolean;
  pullRequest: string | null;
  originAhead: number | null;
  originBehind: number | null;
  /** What its plugin last answered (`open` · `completed` · `abandoned` · `unknown`), and when. */
  pullRequestState: { state: string; at: string | null; askedAt: string; plugin: string | null } | null;
  pullRequestAskFailed: { code: string; plugin: string; at: string } | null;
};

/**
 * A branch as the list draws it: its kind, its last commit, how far it stands from its line, and what names a session's or a
 * landed one. The bridge's `GitBranch` is one, so the list's answer is handed as it came (D52: the molecule imports nothing of
 * the bridge).
 */
export type ListedBranch = {
  name: string;
  /** `line` · `session` · `landed` · `yours` · `origin`; any other reads as yours. */
  kind: string;
  /** Its last commit's whole id, a graph's tip (D147 §4.2); empty where git named none, and the row opens nothing. */
  commit: string;
  at: string | null;
  subject: string;
  ahead: number | null;
  behind: number | null;
  session: { session: string | null; state: string | null } | null;
  landed: ListedLanded | null;
};

/** One repository's list (D147 §2.2): its line and how it stands to origin, its last fetch, and its branches in the driver's order. */
export type BranchListValue = {
  workspace: string;
  line: { branch: string | null; source: string; commit: string | null; origin: string | null; ahead: number | null; behind: number | null };
  fetch: { at: string; heard: boolean } | null;
  branches: readonly ListedBranch[];
  /** The session records could not be read, so no branch is named a session's. */
  sessionsUnread?: boolean;
};

/** What set the line, as Setup says it (D86); a source the page has no word for is the checkout's own guess. */
const SOURCES = new Set(['repository', 'workspace', 'checkout']);

/**
 * **A repository's branches by kind** (UX6h2a, the built-in Git design §2.2): its line at the head, saying what set it, how it
 * stands to origin's copy and when the checkout last fetched; then *Sessions'*, each named by its session's title and state
 * with how far it stands from the line; *Landed*, each saying whether it is pushed and its pull request, with its state and
 * when; and *Yours*, folded after five.
 *
 * @remarks
 * **A molecule**: the list arrives as a prop, and a session's title with it, joined on the page by its holder (the rail's
 * rule, `sessionTitle`). Origin's branches, with no local branch, are on the wire and drawn only from the list's ⋯ after
 * Yours (drawn where some exist), a ticked item that shows *Origin's* and hides it again (UX6h2b1). **Its words are git's facts**: a count git did not make is said as one it could not make, a fetch as git's own
 * `FETCH_HEAD` time or never, and a pull request's state only where its plugin answered (§2.4); nothing is guessed.
 *
 * **A branch's name opens its page** where `onOpen` is handed (UX6h3b, §2.3): the page draws it beside its line since they
 * parted, so a row opens only where both it and its line have a commit here; and the line's own row opens History, since the
 * line's page is the repository's history. A row with no press says nothing of one: it is absent, never disabled.
 */
export function BranchList({ repository, list, titles = NO_TITLES, onOpen, onOpenHistory }: {
  repository: string;
  list: BranchListValue;
  /** Each session branch's session's title, by the branch's name. */
  titles?: Readonly<Record<string, string>>;
  /** Open a branch's page, by its name; absent, no name is a press. */
  onOpen?: (branch: string) => void;
  /** Open the repository's History: the line's own row's press. */
  onOpenHistory?: () => void;
}) {
  const { t } = useTranslation();
  const join = (sentences: (string | null)[]) => sentences.filter(Boolean).join(t('projects.git.branches.join'));
  const of = (kind: string) => list.branches.filter((branch) => kindOf(branch) === kind);
  const sessions = of('session');
  const landed = of('landed');
  const yours = of('yours');
  const origins = of('origin');
  // Hidden each time the tab opens: page state, not remembered (UX6h2b1).
  const [showOrigin, setShowOrigin] = useState(false);
  const line = list.line.branch;
  // A branch is drawn beside its line from both commits (`sinceParted`'s two tips): with either missing, it opens nothing.
  const opens = onOpen && list.line.commit ? (branch: ListedBranch) => (branch.commit ? () => onOpen(branch.name) : undefined) : NO_PRESS;

  return (
    <section aria-label={t('projects.git.branches.list', { repository })} className="grid gap-4">
      <LineHead list={list} onOpenHistory={onOpenHistory} sentence={join([
        line ? t(`settings.lines.from.${SOURCES.has(list.line.source) ? list.line.source : 'checkout'}`, { branch: line, workspace: list.workspace })
          : t('settings.lines.from.none'),
        lineStanding(t, list.line),
        fetched(t, list.fetch),
      ])} />

      {sessions.length > 0 && (
        <Kind
          heading={t('projects.git.branches.kind.session', { count: sessions.length })}
          branches={sessions}
          opens={opens}
          note={list.sessionsUnread
            ? <Prose className="text-small"><Inline text={t('projects.git.branches.sessionsUnread')} /></Prose>
            : null}
          row={(branch) => {
            const state = branch.session?.state;
            const title = titles[branch.name];
            const unnamed = !branch.session?.session && !list.sessionsUnread;
            return (
              <Facts>
                {state && <Pill tone={SESSION_TONE[state as ShownState] ?? 'neutral'}>{t(`sessionState.${state}`, { defaultValue: state })}</Pill>}
                {title && <span className="text-ink">{title}</span>}
                <Said text={join([unnamed ? t('projects.git.branches.session.unnamed') : null, line ? standing(t, line, branch.ahead, branch.behind) : null])} />
              </Facts>
            );
          }}
        />
      )}

      {landed.length > 0 && (
        <Kind
          heading={t('projects.git.branches.kind.landed', { count: landed.length })}
          branches={landed}
          opens={opens}
          row={(branch) => branch.landed && <LandedFacts name={branch.name} landed={branch.landed} />}
        />
      )}

      {yours.length > 0 && (
        <Kind
          heading={t('projects.git.branches.kind.yours', { count: yours.length })}
          branches={yours}
          opens={opens}
          fold={YOURS_SHOWN}
          row={(branch) => line && <Facts><Said text={standing(t, line, branch.ahead, branch.behind)} /></Facts>}
        />
      )}

      {origins.length > 0 && (
        <>
          {/* The list's own ⋯, a body control: ListMore's pieces as the rail's header uses them (platform language §4). */}
          <div className="flex">
            <ListMore
              label={t('projects.git.branches.origin.more')}
              items={[{ id: 'origin', label: t('projects.git.branches.origin.show', { count: origins.length }), checked: showOrigin }]}
              onChoose={() => setShowOrigin((shown) => !shown)}
            />
          </div>
          {showOrigin && (
            <Kind
              heading={t('projects.git.branches.kind.origin', { count: origins.length })}
              branches={origins}
              opens={opens}
              row={() => null}
            />
          )}
        </>
      )}
    </section>
  );
}

const NO_TITLES: Readonly<Record<string, string>> = {};

/** What a row's name presses: nothing. */
const NO_PRESS = (): undefined => undefined;

/**
 * The list's head (§2.2): the line's own branch and last commit, then what set it, how it stands to origin and the last fetch.
 * Its name opens History where that is handed: the line's page is the repository's history (§2.3).
 */
function LineHead({ list, sentence, onOpenHistory }: { list: BranchListValue; sentence: string; onOpenHistory?: () => void }) {
  const { t } = useTranslation();
  const heading = useId();
  const own = list.branches.find((branch) => branch.kind === 'line');
  return (
    <div role="group" aria-labelledby={heading} className="grid gap-0.5">
      <h3 id={heading} className="m-0 text-small font-semibold text-ink-faint">{t('projects.line')}</h3>
      {own && (
        <Named
          branch={own}
          press={own.commit && onOpenHistory
            ? { label: t('projects.git.branches.openHistory', { branch: own.name }), onPress: onOpenHistory }
            : undefined}
        />
      )}
      <Prose className="text-small"><Inline text={sentence} /></Prose>
    </div>
  );
}

/**
 * **One kind's branches**: its heading, counted, and its rows. A folded kind shows its first rows and a press for the rest, as
 * the rail's *Ended* does (`work.list.showMore`); the press moves the focus to the first row it brought, so a keyboard reader
 * goes on where the list did.
 */
function Kind({ heading, branches, note = null, fold, opens, row }: {
  heading: string;
  branches: readonly ListedBranch[];
  note?: ReactNode;
  /** How many rows show before the press; every one where unset. */
  fold?: number;
  /** What a row's name presses, where it opens anything. */
  opens: (branch: ListedBranch) => (() => void) | undefined;
  row: (branch: ListedBranch) => ReactNode;
}) {
  const { t } = useTranslation();
  const named = useId();
  const [open, setOpen] = useState(false);
  const rows = useRef<HTMLUListElement>(null);
  const pressed = useRef(false);
  useEffect(() => {
    if (!open || !pressed.current || fold === undefined) return;
    pressed.current = false;
    (rows.current?.children[fold] as HTMLElement | undefined)?.focus();
  }, [open, fold]);

  const shown = fold !== undefined && !open ? branches.slice(0, fold) : branches;
  const hidden = branches.length - shown.length;
  return (
    <div className="grid gap-1">
      <h3 id={named} className="m-0 text-small font-semibold text-ink-faint">{heading}</h3>
      {note}
      <ul ref={rows} aria-labelledby={named} className="m-0 grid list-none p-0">
        {shown.map((branch) => {
          const onPress = opens(branch);
          return (
            // A row takes the focus a press gives it, and is never a stop of its own.
            <li key={branch.name} tabIndex={-1} className="grid min-w-0 gap-0.5 border-b border-line py-1.5 last:border-b-0">
              <Named branch={branch} press={onPress && { label: t('projects.git.branches.open', { branch: branch.name }), onPress }} />
              {row(branch)}
            </li>
          );
        })}
      </ul>
      {hidden > 0 && (
        <div>
          <Button variant="ghost" className="text-small" onClick={() => { pressed.current = true; setOpen(true); }}>
            {t('work.list.showMore', { count: hidden })}
          </Button>
        </div>
      )}
    </div>
  );
}

/**
 * A branch's name, whole, and when its last commit was made; then that commit's subject, which wraps rather than being cut.
 * Where it opens something, the name is a ghost button named for what it opens, as a workspace's group's heading is.
 */
function Named({ branch, press }: { branch: ListedBranch; press?: { label: string; onPress: () => void } }) {
  // A branch name is one long word: it wraps anywhere rather than widening the tab.
  const name = <code className="min-w-0 font-mono text-small text-ink [overflow-wrap:anywhere]">{branch.name}</code>;
  return (
    <>
      <div className="flex min-w-0 items-baseline gap-2">
        {press
          ? (
            <span className="flex min-w-0 flex-1">
              <Button variant="ghost" className="-my-1 -ml-2 min-h-0 max-w-full text-left" aria-label={press.label} onClick={press.onPress}>
                {name}
              </Button>
            </span>
          )
          : <span className="flex min-w-0 flex-1">{name}</span>}
        {branch.at && <When at={branch.at} />}
      </div>
      {branch.subject && <span className="min-w-0 text-small text-ink-soft [overflow-wrap:anywhere]">{branch.subject}</span>}
    </>
  );
}

/** What a row's kind says of it, on one wrapping line: status leads (platform UX §4). */
function Facts({ children }: { children: ReactNode }) {
  return <p className="m-0 flex flex-wrap items-baseline gap-x-2 gap-y-0.5 text-small text-ink-soft">{children}</p>;
}

function Said({ text }: { text: string | null }) {
  return text ? <span><Inline text={text} /></span> : null;
}

/** When a commit was made or a plugin answered: how long ago, its whole stamp in its tip. The branch page says it so too. */
export function When({ at }: { at: string }) {
  return <time dateTime={at} title={stamp(at)} className="shrink-0 text-meta text-ink-faint tabular-nums">{ago(at)}</time>;
}

/**
 * **A landed branch's facts** (§2.2): whether it is pushed (its own name on origin in step, ahead, behind, or gone), then its
 * pull request: the link the landing kept, as the session's landed note opens it, and its state where a plugin answered, with
 * when, in the words a workflow's run says it (`workflow.run.said.pull-request.*`), with an ask that failed since.
 */
function LandedFacts({ name, landed }: { name: string; landed: ListedLanded }) {
  const { t } = useTranslation();
  const said = pullRequestSaid(t, landed);
  const link = landed.pushed && landed.pullRequest;
  return (
    <>
      <Facts><Said text={landedStanding(t, name, landed)} /></Facts>
      {(link || said.length > 0) && (
        <Facts>
          {link && (
            <ExternalLink href={landed.pullRequest!} className="text-accent underline underline-offset-2">{t('work.review.pullRequest')}</ExternalLink>
          )}
          {said.map(({ sentence, at }) => (
            <span key={sentence}>
              <Inline text={sentence} />
              {at && <>{' '}<When at={at} /></>}
            </span>
          ))}
        </Facts>
      )}
    </>
  );
}

/** How the line stands to origin's copy of it, as last fetched: counted, the same commit, or a difference git did not count. */
function lineStanding(t: TFunction, line: BranchListValue['line']): string | null {
  if (!line.branch) return null;
  if (!line.commit) return t('projects.git.branches.line.noLocal', { line: line.branch });
  const than = `origin/${line.branch}`;
  if (!line.origin) return t('projects.git.branches.standing.absent', { than });
  return standing(t, than, line.ahead, line.behind)
    ?? t(line.origin === line.commit ? 'projects.git.branches.standing.inStep' : 'projects.git.branches.standing.unknown', { than });
}

/** The last fetch, git's own `FETCH_HEAD` time: one that heard something, one tried that heard nothing, or none. */
function fetched(t: TFunction, fetch: BranchListValue['fetch']): string {
  if (!fetch) return t('projects.git.branches.fetch.never');
  return t(fetch.heard ? 'projects.git.branches.fetch.at' : 'projects.git.branches.fetch.empty', { ago: ago(fetch.at) });
}

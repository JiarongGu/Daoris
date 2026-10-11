import { useMemo, useState } from 'react';
import { type GitAnswer, type GitBranch, type GitGraphPage, type GitRepositoryBranches, gitRefusal, useGitBranches, useGitGraphPages } from '../bridge/git';
import { useSessionOpenings } from '../bridge/sessions';
import { sentence } from '../format';
import { useQuests, useSessions } from '../queries';
import { questName, sessionTitle } from '../work/identity';
import { BranchesTab, type GitRead, HistoryTab, type HistoryRead } from './RepositoryGit';
import { WorkspaceBranches, type WorkspaceGroups } from './WorkspaceBranches';

/** Matched without case, as the driver matches a repository's name and a workspace's. */
const same = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();

const NO_TIPS: string[] = [];

/**
 * **A repository's Branches tab, with its data** (UX6h1, D150 §4.4): the part that reads the list, drawn only while the tab
 * shows, so the page asks Git nothing until it is open. A repository with no checkout here (the registry names no root) is
 * said so at once, with no read: there is no git to ask (D147 §6).
 */
export function RepositoryBranches({ repository, workspace, here }: {
  repository: string;
  /** Its workspace, which with its name scopes the list to it (D112, BRSCOPE1). */
  workspace: string;
  /** It has a checkout on this machine. */
  here: boolean;
}) {
  return here
    ? <BranchesRead repository={repository} workspace={workspace} />
    : <BranchesTab repository={repository} read={{ state: 'noCheckout' }} />;
}

/**
 * **A repository's History tab, with its data** (UX6h1, the built-in Git design §2.3): its line's graph, walked from the
 * commit the list names its line's. Read only while it shows, and not at all with no checkout here.
 */
export function RepositoryHistory({ repository, workspace, here }: { repository: string; workspace: string; here: boolean }) {
  return here
    ? <HistoryRead repository={repository} workspace={workspace} />
    : <HistoryTab repository={repository} read={{ state: 'noCheckout' }} />;
}

const NO_BRANCHES: GitBranch[] = [];

/**
 * **A workspace's branch list, with its data** (UX6h2b2, the built-in Git design §2.2, D112): drawn by its Branches tab above
 * the clean-up and the look. One `GIT_BRANCHES` for the workspace answers its repositories holding a branch of Daoris's,
 * each a group, and the rest apart. A group asks its sessions' titles only once it shows, so a tab of many repositories asks
 * for five; one apart is read alone, as its own page's Branches reads it, once the person includes it, never by an `all`
 * read, which would read every checkout here again.
 *
 * @remarks
 * The rows are filtered to the workspace again, as BRSCOPE1's are, so a row of another workspace's is never this tab's.
 */
export function WorkspaceBranchGroups({ workspace, onOpenRepository }: {
  workspace: string;
  onOpenRepository: (repository: string) => void;
}) {
  const { query } = useGitBranches({ workspace });
  const answer = query.data;
  const entries = answer && Array.isArray(answer.repositories) ? answer.repositories.filter((each) => same(each.workspace, workspace)) : [];
  const apart = answer && Array.isArray(answer.apart) ? answer.apart.filter((each) => same(each.workspace, workspace)) : [];
  const read: GitRead<WorkspaceGroups> = query.error ? { state: 'refused', sentence: sentence(query.error) }
    : !answer ? { state: 'reading' }
    : { state: 'read', value: { held: entries.map((each) => each.repository), apart: apart.map((each) => each.repository) } };
  const sessionsUnread = answer?.sessionsUnread === true;

  return (
    <WorkspaceBranches
      workspace={workspace}
      read={read}
      onOpen={onOpenRepository}
      group={(repository, included) => {
        if (included) return <BranchesRead repository={repository} workspace={workspace} />;
        const entry = entries.find((each) => each.repository === repository);
        return entry ? <HeldGroup entry={entry} sessionsUnread={sessionsUnread} /> : null;
      }}
    />
  );
}

/** A group the workspace's read already holds: its list as read, its sessions' titles joined while it shows. */
function HeldGroup({ entry, sessionsUnread }: { entry: GitRepositoryBranches; sessionsUnread: boolean }) {
  const titles = useSessionTitles(entry.repository, entry.branches);
  // A list Git could not read at all says the driver's sentence in its place, as a repository's own tab does.
  const read: GitRead<GitRepositoryBranches & { sessionsUnread: boolean }> = entry.problem && entry.branches.length === 0
    ? { state: 'refused', sentence: entry.problem }
    : { state: 'read', value: { ...entry, sessionsUnread } };
  return <BranchesTab repository={entry.repository} read={read} titles={titles} />;
}

function BranchesRead({ repository, workspace }: { repository: string; workspace: string }) {
  const read = useRepositoryBranches(repository, workspace);
  const titles = useSessionTitles(repository, read.state === 'read' ? read.value.branches : NO_BRANCHES);
  return <BranchesTab repository={repository} read={read} titles={titles} />;
}

/**
 * **Each session branch's session's title, by the branch's name** (UX6h2a, the built-in Git design §2.2): the rail's rule
 * (`sessionTitle`: its quest's name, else its ask or its opening words), joined here from the session the driver named for
 * the branch's tree and the records the service lists. Asked only where a session's branch names one. With no record listed,
 * a quest the driver named still names it, by its name where the quest is listed and by its reference otherwise.
 */
function useSessionTitles(repository: string, branches: readonly GitBranch[]): Record<string, string> {
  const named = branches.filter((branch) => branch.kind === 'session' && (branch.session?.session || branch.session?.quest));
  const sessions = useSessions(repository, true, named.length > 0);
  const quests = useQuests(null, true, named.length > 0);
  const records = (sessions.data ?? []).filter((record) => named.some((branch) => branch.session?.session === record.id));
  const openings = useSessionOpenings(records);

  return Object.fromEntries(named.flatMap((branch): [string, string][] => {
    const record = records.find((each) => each.id === branch.session?.session);
    const questId = record?.quest ?? branch.session?.quest;
    const quest = questId ? (quests.data ?? []).find((each) => each.id === questId) ?? null : null;
    if (record) return [[branch.name, sessionTitle(record, quest, openings[record.id])]];
    if (quest) return [[branch.name, questName(quest)]];
    return questId ? [[branch.name, `#${questId}`]] : [];
  }));
}

/** One page of the graph as its query holds it: the page, a refusal's code with its sentence, or still on its way. */
type PageOutcome = { page: GitGraphPage } | { code: string | null; sentence: string } | null;

function pageOutcome(query: { error: unknown; data?: GitAnswer<GitGraphPage> }): PageOutcome {
  if (query.error) return { code: null, sentence: sentence(query.error) };
  if (!query.data) return null;
  // A refusal is a code the page words (`errors.{code}`), never git's own sentence (GIT1d).
  const refusal = gitRefusal(query.data);
  if (refusal) return { code: refusal.code, sentence: sentence(refusal) };
  const page = query.data.value;
  if (!page || !Array.isArray(page.commits)) return { code: null, sentence: sentence({ code: 'UNKNOWN' }) };
  return { page };
}

/**
 * The History tab's read, the graph's pages accumulated (UX6h3a): one `GIT_GRAPH` per page asked, `useGitGraphPages`, each
 * cached by its page, and the count asked a state of the holder's, reset with the tip it counts for. The commits are the
 * leading pages that arrived, in order, so the lanes are laid over the whole list; the next page on its way, or refused, is
 * said in the foot beside them, which a press asks again.
 */
function HistoryRead({ repository, workspace }: { repository: string; workspace: string }) {
  const list = useRepositoryBranches(repository, workspace);
  const commit = list.state === 'read' ? list.value.line.commit : null;
  // The graph's tips are whole ids the list resolved (D147 §4.2): with none, the graph asks nothing.
  const tips = useMemo(() => (commit ? [commit] : NO_TIPS), [commit]);
  const [asked, setAsked] = useState({ tip: '', pages: 1 });
  const pages = asked.tip === (commit ?? '') ? asked.pages : 1;
  const { queries } = useGitGraphPages(repository, tips, pages, { workspace });
  const outcomes = queries.map(pageOutcome);

  const loaded: GitGraphPage[] = [];
  for (const outcome of outcomes) {
    if (!outcome || !('page' in outcome)) break;
    loaded.push(outcome.page);
  }
  const next = queries[loaded.length];
  const failed = outcomes[loaded.length];
  const onMore = () => {
    // The page that failed is asked again; otherwise the next is asked.
    if (next) void next.refetch();
    else setAsked({ tip: commit ?? '', pages: pages + 1 });
  };

  const read = ((): HistoryRead => {
    if (list.state !== 'read') return list;
    const { line } = list.value;
    if (!line.commit) return { state: 'noLine', line: line.branch };
    const first = outcomes[0];
    if (!first) return { state: 'reading' };
    if (!('page' in first)) return first.code === 'GIT_NO_CHECKOUT' ? { state: 'noCheckout' } : { state: 'refused', sentence: first.sentence };
    const loadingMore = Boolean(next?.isFetching);
    return {
      state: 'read',
      value: {
        line: line.branch ?? '', tips, commits: loaded.flatMap((page) => page.commits), more: loaded[loaded.length - 1]!.more,
        loadingMore, moreError: !loadingMore && failed && !('page' in failed) ? failed.sentence : null,
      },
    };
  })();
  return <HistoryTab repository={repository} read={read} onMore={onMore} />;
}

/**
 * The list's read of one repository (`GIT_BRANCHES`, scoped to it): on its way, failed in the driver's words, absent where
 * it has no checkout here, or its entry, with the list's own fact beside it that the session records went unread. A list Git
 * could not read at all says the driver's sentence in its place.
 */
function useRepositoryBranches(repository: string, workspace: string): GitRead<GitRepositoryBranches & { sessionsUnread: boolean }> {
  const { query } = useGitBranches({ repository, workspace });
  if (query.error) return { state: 'refused', sentence: sentence(query.error) };
  if (!query.data) return { state: 'reading' };
  const entries = Array.isArray(query.data.repositories) ? query.data.repositories : [];
  const entry = entries.find((each) => same(each.repository, repository));
  if (!entry) return { state: 'noCheckout' };
  if (entry.problem && entry.branches.length === 0) return { state: 'refused', sentence: entry.problem };
  return { state: 'read', value: { ...entry, sessionsUnread: query.data.sessionsUnread === true } };
}

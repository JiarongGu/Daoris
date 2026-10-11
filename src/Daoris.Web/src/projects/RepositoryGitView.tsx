import { useMemo } from 'react';
import { type GitRepositoryBranches, gitRefusal, useGitBranches, useGitGraph } from '../bridge/git';
import { sentence } from '../format';
import { BranchesTab, type GitRead, HistoryTab, type HistoryRead } from './RepositoryGit';

/** Matched without case, as the driver matches a repository's name. */
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

function BranchesRead({ repository, workspace }: { repository: string; workspace: string }) {
  return <BranchesTab repository={repository} read={useRepositoryBranches(repository, workspace)} />;
}

function HistoryRead({ repository, workspace }: { repository: string; workspace: string }) {
  const list = useRepositoryBranches(repository, workspace);
  const commit = list.state === 'read' ? list.value.line.commit : null;
  // The graph's tips are whole ids the list resolved (D147 §4.2): with none, the graph asks nothing.
  const tips = useMemo(() => (commit ? [commit] : NO_TIPS), [commit]);
  const { query } = useGitGraph(repository, tips, { workspace });

  const read = ((): HistoryRead => {
    if (list.state !== 'read') return list;
    const { line } = list.value;
    if (!line.commit) return { state: 'noLine', line: line.branch };
    if (query.error) return { state: 'refused', sentence: sentence(query.error) };
    if (!query.data) return { state: 'reading' };
    // A refusal is a code the page words (`errors.{code}`), never git's own sentence (GIT1d).
    const refusal = gitRefusal(query.data);
    if (refusal) return refusal.code === 'GIT_NO_CHECKOUT' ? { state: 'noCheckout' } : { state: 'refused', sentence: sentence(refusal) };
    const page = query.data.value;
    if (!page || !Array.isArray(page.commits)) return { state: 'refused', sentence: sentence({ code: 'UNKNOWN' }) };
    return { state: 'read', value: { line: line.branch ?? '', tips, commits: page.commits } };
  })();
  return <HistoryTab repository={repository} read={read} />;
}

/**
 * The list's read of one repository (`GIT_BRANCHES`, scoped to it): on its way, failed in the driver's words, absent where
 * it has no checkout here, or its entry. A list Git could not read at all says the driver's sentence in its place.
 */
function useRepositoryBranches(repository: string, workspace: string): GitRead<GitRepositoryBranches> {
  const { query } = useGitBranches({ repository, workspace });
  if (query.error) return { state: 'refused', sentence: sentence(query.error) };
  if (!query.data) return { state: 'reading' };
  const entries = Array.isArray(query.data.repositories) ? query.data.repositories : [];
  const entry = entries.find((each) => same(each.repository, repository));
  if (!entry) return { state: 'noCheckout' };
  if (entry.problem && entry.branches.length === 0) return { state: 'refused', sentence: entry.problem };
  return { state: 'read', value: entry };
}

import { type ReactNode, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField, Prose } from '../ui';
import { type GitRead, Nothing, Waiting } from './RepositoryGit';

/** Groups drawn before the rest are one press away, as a list's *Yours* are (the built-in Git design §2.2). */
export const GROUPS_SHOWN = 5;

/**
 * A workspace's repositories as its Branches tab reads them (D112): those holding a branch of Daoris's, in the driver's order,
 * and every other one with a checkout here, listed apart.
 */
export type WorkspaceGroups = { held: readonly string[]; apart: readonly string[] };

/**
 * **The branch list in a workspace's Branches tab** (UX6h2b2, the built-in Git design §2.2, D112): each repository holding a
 * branch of Daoris's is a group, headed by its name, which is the door to its page, as the workspace's Details names each
 * one; its list is the repository's own (`BranchList`). Groups fold after five, with the rail's *Show N more*. Every other
 * repository with a checkout here is listed apart and folded, and each one the person includes is a group after the rest.
 *
 * @remarks
 * **A molecule**: the read arrives as a prop, and each group's list is drawn by its holder (`group`), called only for a group
 * that shows, so a hidden or apart repository asks nothing: not its branches, not its sessions' titles. What is included is
 * the page's state and not remembered; it is a local read, unlike the look below, which reaches origin.
 */
export function WorkspaceBranches({ workspace, read, group, onOpen }: {
  workspace: string;
  read: GitRead<WorkspaceGroups>;
  /** One repository's list, drawn by its holder; `included` where the person included it from apart. */
  group: (repository: string, included: boolean) => ReactNode;
  /** Open a repository's page: each group's heading is its door. */
  onOpen: (repository: string) => void;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [included, setIncluded] = useState<readonly string[]>([]);
  const groups = useRef<HTMLDivElement>(null);
  // The group whose door takes the focus once it is drawn: the first a press brought.
  const focusOn = useRef<string | null>(null);
  useEffect(() => {
    const name = focusOn.current;
    if (!name) return;
    focusOn.current = null;
    const brought = [...(groups.current?.querySelectorAll<HTMLElement>('[data-group]') ?? [])].find((each) => each.dataset.group === name);
    brought?.querySelector<HTMLElement>('h2 button')?.focus();
  }, [open, included]);

  if (read.state === 'reading') return <Waiting reading sentence={t('projects.git.workspace.reading', { workspace })} />;
  if (read.state === 'refused') return <Waiting reading={false} sentence={read.sentence} />;
  const { held, apart } = read.state === 'read' ? read.value : { held: [], apart: [] };
  if (held.length === 0 && apart.length === 0) {
    return <Nothing headline={t('projects.git.noCheckout.headline')} body={t('projects.git.workspace.noCheckout', { workspace })} />;
  }

  const shown = open ? held : held.slice(0, GROUPS_SHOWN);
  const hidden = held.length - shown.length;
  // In the driver's order, whichever order they were ticked in; one that has since come to hold Daoris's is a group above.
  const takenIn = apart.filter((repository) => included.includes(repository));
  const left = apart.filter((repository) => !included.includes(repository));

  return (
    <div ref={groups} className="grid min-w-0 gap-5">
      {shown.map((repository) => <Group key={repository} repository={repository} onOpen={onOpen}>{group(repository, false)}</Group>)}
      {hidden > 0 && (
        <div>
          <Button variant="ghost" className="text-small" onClick={() => { focusOn.current = held[shown.length] ?? null; setOpen(true); }}>
            {t('work.list.showMore', { count: hidden })}
          </Button>
        </div>
      )}
      {takenIn.map((repository) => <Group key={repository} repository={repository} onOpen={onOpen}>{group(repository, true)}</Group>)}
      {left.length > 0 && (
        <ApartFold
          apart={left}
          onInclude={(ticked) => {
            focusOn.current = left.find((repository) => ticked.includes(repository)) ?? null;
            setIncluded((was) => [...was, ...ticked]);
          }}
        />
      )}
    </div>
  );
}

/** One repository's group: its name as a heading, the door to its page, then its list. */
function Group({ repository, onOpen, children }: { repository: string; onOpen: (repository: string) => void; children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <div data-group={repository} className="grid min-w-0 gap-2 border-t border-line pt-4 first:border-t-0 first:pt-0">
      <h2 className="m-0 min-w-0">
        <Button
          variant="ghost"
          className="-ml-2 max-w-full text-left text-body font-semibold text-ink [overflow-wrap:anywhere]"
          aria-label={t('projects.workspace.open', { repository })}
          onClick={() => onOpen(repository)}
        >
          {repository}
        </Button>
      </h2>
      {children}
    </div>
  );
}

/**
 * The repositories with a checkout here that hold no branch of Daoris's (D112): listed apart, folded, and drawn only once the
 * person ticks them, each or all, and includes them. The look's own list apart (`settings/Sync`) is the same shape, with its
 * own words, since including there reaches origin.
 */
function ApartFold({ apart, onInclude }: { apart: readonly string[]; onInclude: (repositories: readonly string[]) => void }) {
  const { t } = useTranslation();
  const [ticked, setTicked] = useState<readonly string[]>([]);
  const tick = (repository: string, on: boolean) =>
    setTicked((was) => (on ? [...was, repository] : was.filter((name) => name !== repository)));

  return (
    <details>
      <summary className="cursor-pointer text-small text-ink-soft">{t('projects.git.workspace.apart.summary', { count: apart.length })}</summary>
      <div role="group" aria-label={t('projects.git.workspace.apart.label')} className="mt-2 pl-4">
        <Prose className="text-small text-ink-soft">{t('projects.git.workspace.apart.body')}</Prose>
        <CheckField className="mt-2" checked={ticked.length === apart.length} label={t('projects.git.workspace.apart.all', { count: apart.length })}
          onChange={(on) => setTicked(on ? apart : [])} />
        <ul className="m-0 mt-1 grid list-none grid-cols-[repeat(auto-fill,minmax(12rem,1fr))] gap-x-3 p-0">
          {apart.map((repository) => (
            <li key={repository} className="py-0.5">
              <CheckField className="font-mono text-small" checked={ticked.includes(repository)} label={repository}
                onChange={(on) => tick(repository, on)} />
            </li>
          ))}
        </ul>
        <Button className="mt-2" disabled={ticked.length === 0} onClick={() => { onInclude(ticked); setTicked([]); }}>
          {t('projects.git.workspace.apart.include', { count: ticked.length })}
        </Button>
      </div>
    </details>
  );
}

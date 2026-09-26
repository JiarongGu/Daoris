import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Registration } from './api';
import { workspaceOf } from './workspaces';
import { useImportFolder, useRegisterRepository, useRetireRepository, useWireRepository } from './queries';
import { type FolderInspection, usePickFolder, useWriteDeclaration } from './shell';
import { Button, Chip, Drawer, failure, Inline, type Notify, PathText, SectionTitle, Tip } from './ui';

/**
 * Managing the machine's repositories (D48 §7) — the surfaces that exist only where a shell is
 * attached, because managing repositories means touching machine paths and a browser is never told one
 * (D46/D47 §4).
 *
 * Three rules hold across everything here, and each is visible in the UI rather than only in the code:
 *
 * - **Registration lifecycle only.** Adding registers, removing retires. No file is ever deleted, and
 *   the retire panel says so in the one place a person will read it.
 * - **The two updates are kept apart.** Re-wiring a workspace edits a row on this machine — instant,
 *   local, nothing written. Editing the declaration writes a TRACKED file in that repository, and the
 *   diff lands uncommitted for its own review. Presenting them as one form would blur the only
 *   distinction that matters.
 * - **Adoption is not ours to do.** A folder with no manifest is added as a non-adopter and the steps
 *   are shown as text to run in that repository, never as a button here (D31's shape).
 */

const EMPTY_LINES = (value: string): string[] =>
  value.split('\n').map((line) => line.trim()).filter(Boolean);

export function AddProjectDrawer({ onClose, notify }: { onClose: () => void; notify: Notify }) {
  const { t } = useTranslation();
  const pick = usePickFolder();
  const register = useRegisterRepository();
  const [found, setFound] = useState<FolderInspection | null>(null);
  const [workspace, setWorkspace] = useState('');

  const choose = () => pick.mutate(undefined, {
    // Null is the person cancelling the dialog: an answer, and not a thing to report as a failure.
    onSuccess: (inspection) => { if (inspection) setFound(inspection); },
    onError: failure(notify),
  });

  const add = () => {
    if (!found) return;
    register.mutate({
      repository: found.name,
      root: found.path,
      // What the shell found there, stated (D70): registered is addressable, adopted is disciplined —
      // and a row that said adopted without a manifest would be driven with no connector.
      adopted: found.adopted,
      ...(workspace.trim() ? { workspace: workspace.trim() } : {}),
      ...(found.adopted
        ? {
            domain: { summary: found.summary ?? '', owns: found.owns, accepts: found.accepts },
            packs: found.packs,
            join: found.join,
            shareKnowledge: found.shareKnowledge,
          }
        : {}),
    }, {
      onSuccess: (registered) => {
        notify(t('projects.manage.added', { name: found.name, workspace: registered.workspace }));
        onClose();
      },
      onError: failure(notify),
    });
  };

  return (
    <Drawer
      title={t('projects.manage.addTitle')}
      onClose={onClose}
      footer={
        // The move, then *never mind*, as every other drawer puts them (UX5 U38): this one was reversed,
        // its action pushed to the far edge.
        <div className="flex items-center gap-2">
          <Button
            variant="primary"
            disabled={!found?.exists || register.isPending}
            onClick={add}
          >
            {t('projects.manage.register')}
          </Button>
          <Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
        </div>
      }
    >
      <p className="text-body text-ink-soft">{t('projects.manage.addBody')}</p>

      <Button className="mt-3" onClick={choose} disabled={pick.isPending}>
        {t('projects.manage.choose')}
      </Button>

      {found && (
        <div className="mt-4 border-t border-line pt-3.5">
          <p className="text-small text-ink-soft"><PathText path={found.path} /></p>
          <p className="mt-2 flex flex-wrap items-baseline gap-1.5">
            <Chip accent>{found.name}</Chip>
            <Chip>{found.adopted ? t('projects.manage.adopted') : t('projects.manage.notAdopted')}</Chip>
            <Chip>{found.git ? t('projects.manage.git') : t('projects.manage.noGit')}</Chip>
          </p>
          {found.summary && <p className="mt-2 text-body text-ink-soft">{found.summary}</p>}

          {!found.adopted && (
            /* The join steps as TEXT to run in that repository (D31): adoption is its own agent's
               job — the collisions, the review, the budget — and a button here would do that work
               from outside, by whoever knows that codebase least. */
            <p className="mt-3 rounded-control bg-accent-soft px-3 py-2.5 font-mono text-small">
              <Inline text={t('projects.manage.adoptHere')} />
            </p>
          )}

          {!found.git && (
            <p className="mt-3 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
              {t('projects.manage.noGitWarning')}
            </p>
          )}

          <label className="mt-4 block text-small text-ink-faint" htmlFor="add-workspace">
            {t('projects.workspace')}
          </label>
          <input
            id="add-workspace"
            value={workspace}
            onChange={(event) => setWorkspace(event.target.value)}
            placeholder={t('projects.manage.workspacePlaceholder')}
            className="mt-1 w-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
          <p className="mt-1.5 text-small text-ink-faint">{t('projects.manage.workspaceNote')}</p>
        </div>
      )}
    </Drawer>
  );
}

/**
 * Setting a folder up as a workspace (D77): every folder inside it registered at once, wired to the
 * workspace named here — `daoris import <folder> --workspace <name>`'s screen twin. The folder's own
 * name is offered, because that is what setting a folder up as a workspace means. Emptied, the import
 * names none, and an import that names none moves nobody (D48 §2).
 */
export function ImportFolderDrawer({ onClose, notify }: { onClose: () => void; notify: Notify }) {
  const { t } = useTranslation();
  const pick = usePickFolder();
  const importFolder = useImportFolder();
  const [folder, setFolder] = useState<FolderInspection | null>(null);
  const [workspace, setWorkspace] = useState('');

  const choose = () => pick.mutate(undefined, {
    onSuccess: (inspection) => {
      if (!inspection) return;
      setFolder(inspection);
      setWorkspace(inspection.name);
    },
    onError: failure(notify),
  });

  const run = () => {
    if (!folder) return;
    importFolder.mutate({ folder: folder.path, workspace: workspace.trim() || undefined }, {
      // The service's sentence, as said: it names what registered and where it landed.
      onSuccess: (result) => {
        notify(result.message);
        onClose();
      },
      onError: failure(notify),
    });
  };

  return (
    <Drawer
      title={t('projects.manage.importTitle')}
      onClose={onClose}
      footer={
        <div className="flex items-center gap-2">
          <Button variant="primary" disabled={!folder?.exists || importFolder.isPending} onClick={run}>
            {t('projects.manage.importRun')}
          </Button>
          <Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
        </div>
      }
    >
      <p className="text-body text-ink-soft">{t('projects.manage.importBody')}</p>

      <Button className="mt-3" onClick={choose} disabled={pick.isPending}>
        {t('projects.manage.choose')}
      </Button>

      {folder && (
        <div className="mt-4 border-t border-line pt-3.5">
          <p className="text-small text-ink-soft"><PathText path={folder.path} /></p>

          <label className="mt-4 block text-small text-ink-faint" htmlFor="import-workspace">
            {t('projects.workspace')}
          </label>
          <input
            id="import-workspace"
            value={workspace}
            onChange={(event) => setWorkspace(event.target.value)}
            placeholder={t('projects.manage.importWorkspacePlaceholder')}
            className="mt-1 w-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
          <p className="mt-1.5 text-small text-ink-faint">{t('projects.manage.importWorkspaceNote')}</p>
        </div>
      )}
    </Drawer>
  );
}

export function ManageProjectDrawer({ project, onClose, notify }: {
  project: Registration; onClose: () => void; notify: Notify;
}) {
  const { t } = useTranslation();
  const wire = useWireRepository();
  const retire = useRetireRepository();
  const register = useRegisterRepository();
  const writeDeclaration = useWriteDeclaration();

  const [workspace, setWorkspace] = useState(workspaceOf(project));
  const [summary, setSummary] = useState(project.summary ?? '');
  const [owns, setOwns] = useState(project.owns.join('\n'));
  const [accepts, setAccepts] = useState(project.accepts.join('\n'));
  const [confirming, setConfirming] = useState(false);

  const fail = failure(notify);

  const saveWiring = () => wire.mutate({ repository: project.repository, workspace }, {
    onSuccess: (wired) => notify(t('projects.manage.wired', {
      name: project.repository, workspace: wired.workspace,
    })),
    onError: fail,
  });

  // Two writes, in order, because they are two different things: the tracked file first (it is what a
  // reviewer will see), then the registration that reflects it.
  const saveDeclaration = () => {
    if (!project.root) return;
    writeDeclaration.mutate({
      path: project.root,
      summary,
      owns: EMPTY_LINES(owns),
      accepts: EMPTY_LINES(accepts),
      join: project.joined ?? false,
      shareKnowledge: project.sharesKnowledge ?? false,
    }, {
      onSuccess: (written) => {
        register.mutate({
          repository: project.repository,
          root: written.path,
          domain: { summary: written.summary ?? '', owns: written.owns, accepts: written.accepts },
          packs: written.packs,
          join: written.join,
          shareKnowledge: written.shareKnowledge,
        }, {
          onSuccess: () => notify(t('projects.manage.declared', { name: project.repository })),
          onError: fail,
        });
      },
      onError: fail,
    });
  };

  return (
    <Drawer
      title={t('projects.manage.title', { name: project.repository })}
      meta={project.root && <PathText path={project.root} className="text-small" />}
      onClose={onClose}
    >
      <SectionTitle>{t('projects.manage.wiring')}</SectionTitle>
      <p className="text-body text-ink-soft">{t('projects.manage.wiringBody')}</p>
      <div className="mt-2 flex items-center gap-2">
        <input
          value={workspace}
          onChange={(event) => setWorkspace(event.target.value)}
          aria-label={t('projects.workspace')}
          className="min-w-0 flex-1 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
        />
        <Button onClick={saveWiring} disabled={wire.isPending}>{t('projects.manage.rewire')}</Button>
      </div>

      <div className="mt-6">
        <SectionTitle>{t('projects.manage.declaration')}</SectionTitle>
        {project.root ? (
          <>
            <p className="text-body text-ink-soft">{t('projects.manage.declarationBody')}</p>
            <label className="mt-2.5 block text-small text-ink-faint" htmlFor="declaration-summary">
              {t('projects.manage.summary')}
            </label>
            <input
              id="declaration-summary"
              value={summary}
              onChange={(event) => setSummary(event.target.value)}
              className="mt-1 w-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
            />
            <label className="mt-2.5 block text-small text-ink-faint" htmlFor="declaration-owns">
              {t('projects.owns')}
            </label>
            <textarea
              id="declaration-owns"
              value={owns}
              rows={3}
              onChange={(event) => setOwns(event.target.value)}
              className="mt-1 w-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
            />
            <label className="mt-2.5 block text-small text-ink-faint" htmlFor="declaration-accepts">
              {t('projects.accepts')}
            </label>
            <textarea
              id="declaration-accepts"
              value={accepts}
              rows={3}
              onChange={(event) => setAccepts(event.target.value)}
              className="mt-1 w-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
            />
            <p className="mt-1.5 text-small text-ink-faint">{t('projects.manage.linesNote')}</p>
            <Button
              className="mt-2.5"
              onClick={saveDeclaration}
              disabled={writeDeclaration.isPending || register.isPending}
            >
              {t('projects.manage.writeDeclaration')}
            </Button>
          </>
        ) : (
          /* A registration with no checkout here — a teammate's, mirrored down from a remote (D47 §9).
             Its manifest lives on their machine, and editing it from here would be reaching in. */
          <p className="text-body text-ink-soft">{t('projects.manage.noCheckout')}</p>
        )}
      </div>

      <div className="mt-6">
        <SectionTitle>{t('projects.manage.retire')}</SectionTitle>
        <p className="text-body text-ink-soft">{t('projects.manage.retireBody')}</p>
        <p className="mt-2 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
          {t('projects.manage.retireNotDelete')}
        </p>
        {confirming ? (
          <div className="mt-2.5 flex items-center gap-2">
            <Button
              variant="danger"
              disabled={retire.isPending}
              onClick={() => retire.mutate(project.repository, {
                // The service's own sentence, verbatim — it is the part that says what did not happen.
                onSuccess: (answer) => { notify(answer.message); onClose(); },
                onError: fail,
              })}
            >
              {t('projects.manage.retireConfirm')}
            </Button>
            <Button variant="ghost" onClick={() => setConfirming(false)}>{t('common.cancel')}</Button>
          </div>
        ) : (
          <Button className="mt-2.5" variant="danger" onClick={() => setConfirming(true)}>
            {t('projects.manage.retire')}
          </Button>
        )}
      </div>

      <p className="mt-6 text-small text-ink-faint">
        <Tip content={t('projects.manage.doctrineTip')}>
          <span>{t('projects.manage.doctrine')}</span>
        </Tip>
      </p>
    </Drawer>
  );
}

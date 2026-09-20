import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Registration } from './api';
import { AddProjectDrawer, ManageProjectDrawer } from './ProjectManage';
import { ChatDrawer } from './ChatDrawer';
import { ago, sentence } from './format';
import { useRegistry, useRepositories, useSessions } from './queries';
import { useDriver, useHarnesses, useSetDrivable, useSetHold, useStartChat } from './shell';
import {
  Button, Card, CheckField, Chip, type Notify, PageHeader, SkeletonRows, Tip, useErrorNotify,
} from './ui';

/**
 * The setup half of the platform (D38): who is in the family, what each repository owns and accepts —
 * as chips a person can scan — and, just as deliberately, who cannot be asked yet. Membership is a
 * repository's own act (D32): Daoris never writes into a sibling, so nothing joins by being seen; the
 * join steps are proposed as text, never a button (D31's shape).
 */
export function ProjectsView({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const registry = useRegistry();
  const repositories = useRepositories();
  const driver = useDriver();
  const setDrivable = useSetDrivable();
  const setHold = useSetHold();
  // The driver bridge included: a STATE that fails silently reads as a machine with no driver.
  useErrorNotify(registry.error ?? repositories.error ?? driver.error, notify);

  // The management surfaces exist where a shell does (D48 §7) — the same gate as every control, and
  // for the same reason: managing repositories means touching machine paths, and a browser has none.
  const attached = driver.data !== undefined;
  const [adding, setAdding] = useState(false);
  const [managing, setManaging] = useState<Registration | null>(null);

  // A live CONVERSATION in this repository — not just any live session. A driven session also holds
  // the tree, but it has no channel to speak into: it was given its whole target at once. Offering
  // to "open" one would put a person in front of an input box nothing is listening to; starting a
  // chat instead gets the ledger's own refusal, which names what holds the repository.
  const sessions = useSessions(null, false);
  const startChat = useStartChat();
  const [chatting, setChatting] = useState<string | null>(null);

  // The per-session picker (D49 §4): which credential profile the NEXT conversation runs as. One
  // control rather than one per row, because it is a standing choice for the next thing a person
  // starts — and empty is not "no profile", it is "whatever this machine already decided", which is
  // what the label says. Absent entirely when there is nothing to choose between.
  const harnesses = useHarnesses();
  const [profile, setProfile] = useState('');
  // Shape-tolerant for the same reason the roster is: an older shell answers a request it has never
  // heard of with something else, and a page that could not start a conversation because of it would
  // be a worse failure than one that simply offers no picker.
  const spawning = (Array.isArray(harnesses.data?.harnesses) ? harnesses.data.harnesses : [])
    .find((h) => h.harness === harnesses.data?.adapter);
  const choices = Array.isArray(spawning?.profiles) ? spawning.profiles : [];
  const chatIn = (repository: string) =>
    (sessions.data ?? []).find((s) => s.repository === repository && s.kind === 'chat');
  const chat = (sessions.data ?? []).find((s) => s.id === chatting);

  const onChat = (repository: string) => {
    const already = chatIn(repository);
    if (already) {
      setChatting(already.id);
      return;
    }

    startChat.mutate({ repository, profile: profile || undefined }, {
      onSuccess: (result) => {
        // A refusal is the ledger's own sentence — the repository is busy, has no checkout here, or
        // (since D49 §4) its harness is missing or the chosen profile is logged out. Each of those
        // names the action that fixes it, which is why the sentence is shown rather than summarised.
        if (!result.sessionId) notify(result.message, 'error');
        else setChatting(result.sessionId);
      },
      onError: (error: unknown) => notify(sentence(error), 'error'),
    });
  };

  const adopted = (registry.data ?? []).filter((r) => r.adopted);
  const outside = (registry.data ?? []).filter((r) => !r.adopted);
  const indexed = (name: string) => (repositories.data ?? []).find((r) => r.name === name);
  const named = (names: string[], repository: string) =>
    names.some((name) => name.toLowerCase() === repository.toLowerCase());
  const onDriverError = (e: unknown) => notify(sentence(e), 'error');

  return (
    <section>
      <PageHeader
        title={t('projects.title')}
        description={t('projects.description')}
        action={attached
          ? <Button variant="primary" onClick={() => setAdding(true)}>{t('projects.manage.add')}</Button>
          : undefined}
      />

      {attached && choices.length > 0 && (
        <label className="mb-3.5 flex flex-wrap items-center gap-2 text-[0.8rem] text-ink-faint">
          {t('projects.chatAs')}
          <select
            value={profile}
            onChange={(event) => setProfile(event.target.value)}
            className="rounded-control border border-line bg-raised px-2 py-1 text-[0.8rem] text-ink"
          >
            <option value="">{t('projects.chatAsDefault')}</option>
            {choices.map((choice) => (
              // A logged-out profile is offered and labelled rather than hidden: the spawn refuses
              // with the sentence that names the login action, which teaches more than a missing row.
              <option key={choice.name} value={choice.name}>
                {choice.login === 'out' ? t('harness.profileOut', { name: choice.name }) : choice.name}
              </option>
            ))}
          </select>
        </label>
      )}

      {adding && <AddProjectDrawer onClose={() => setAdding(false)} notify={notify} />}
      {managing && (
        <ManageProjectDrawer project={managing} onClose={() => setManaging(null)} notify={notify} />
      )}
      {chat && <ChatDrawer session={chat} onClose={() => setChatting(null)} notify={notify} />}

      {registry.isPending && <SkeletonRows rows={4} />}

      <div className="grid items-start gap-3.5 lg:grid-cols-2">
        {adopted.map((project) => {
          const counts = indexed(project.repository);
          return (
            <Card key={project.repository}>
              <header className="flex items-baseline justify-between gap-4">
                <span className="inline-flex items-center gap-2 text-[0.95rem] font-semibold">
                  <Tip content={t('projects.adoptedDot')}>
                    <span className="inline-block size-2 shrink-0 rounded-full bg-accent" />
                  </Tip>
                  {project.repository}
                </span>
                <span className="whitespace-nowrap font-mono text-[0.78rem] tabular-nums text-ink-faint">
                  {counts
                    ? t('projects.entries', {
                        total: counts.total.toLocaleString(),
                        local: counts.local.toLocaleString(),
                        canonical: counts.canonical.toLocaleString(),
                      })
                    : t('projects.nothingIndexed')}
                </span>
              </header>
              {project.summary
                ? <p className="mt-1.5 text-[0.85rem] text-ink-soft">{project.summary}</p>
                : (
                  /* Addressable regardless — adoption gates addressing, declaration does not (D34) —
                     but an asker deserves to know they would be guessing. */
                  <p className="mt-2 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-[0.875rem] text-ink-soft">
                    {t('projects.undeclared')}
                  </p>
                )}
              {project.owns.length > 0 && (
                <p className="mt-2 flex flex-wrap items-baseline gap-1.5">
                  <span className="min-w-12 text-[0.72rem] text-ink-faint">{t('projects.owns')}</span>
                  {project.owns.map((item) => <Chip key={item}>{item}</Chip>)}
                </p>
              )}
              {project.accepts.length > 0 && (
                <p className="mt-2 flex flex-wrap items-baseline gap-1.5">
                  <span className="min-w-12 text-[0.72rem] text-ink-faint">{t('projects.accepts')}</span>
                  {project.accepts.map((item) => <Chip key={item} accent>{item}</Chip>)}
                </p>
              )}
              {project.packs.length > 0 && (
                <p className="mt-2 flex flex-wrap items-baseline gap-1.5">
                  <span className="min-w-12 text-[0.72rem] text-ink-faint">{t('projects.packs')}</span>
                  {project.packs.map((item) => <Chip key={item}>{item}</Chip>)}
                </p>
              )}
              {counts?.fed && (
                /* Where this deployment's copy came from (D48 §6). Shown rather than implied: the
                   index is a claim about a commit, and a person who cannot see which commit has no
                   way to tell a current view from one a machine stopped feeding a month ago. */
                <p className="mt-2 flex flex-wrap items-baseline gap-1.5">
                  <span className="min-w-12 text-[0.72rem] text-ink-faint">{t('projects.fed')}</span>
                  <Tip content={t('projects.fedTip', {
                    commit: counts.fed.commit,
                    branch: counts.fed.branch,
                    origin: counts.fed.origin ?? t('projects.fedUnknownOrigin'),
                  })}
                  >
                    <span className="font-mono text-[0.78rem] text-ink-soft">
                      {counts.fed.shortCommit} · {ago(counts.fed.committedAt)}
                    </span>
                  </Tip>
                </p>
              )}
              {project.workspace && (
                /* Which circle this one shares with (D48). Shown rather than assumed: a machine
                   holding two workspaces would otherwise present them as one family, and the
                   person would have no way to tell from the list that it was two. */
                <p className="mt-2 flex flex-wrap items-baseline gap-1.5">
                  <span className="min-w-12 text-[0.72rem] text-ink-faint">{t('projects.workspace')}</span>
                  <Tip content={t('projects.workspaceTip')}><Chip>{project.workspace}</Chip></Tip>
                </p>
              )}
              {driver.data && (
                /* The person's standing choices for THIS machine's driver (D46 §6) — rendered only
                   where a shell answers; a browser has no driver to control, and shows nothing. */
                <p className="mt-2.5 flex flex-wrap items-center gap-4 border-t border-line pt-2.5">
                  <span className="min-w-12 text-[0.72rem] text-ink-faint">{t('projects.driver.label')}</span>
                  <CheckField
                    checked={named(driver.data.drivable, project.repository)}
                    onChange={(next) => setDrivable.mutate(
                      { repository: project.repository, drivable: next }, { onError: onDriverError })}
                    label={t('projects.driver.drive')}
                  />
                  {named(driver.data.drivable, project.repository) && (
                    <CheckField
                      checked={named(driver.data.holds, project.repository)}
                      onChange={(next) => setHold.mutate(
                        { repository: project.repository, held: next }, { onError: onDriverError })}
                      label={t('projects.driver.hold')}
                    />
                  )}
                  {/* A conversation in this repository (D49 §3) — where a driver is attached, because
                      a chat is a process on this machine. Already talking? The same button opens it. */}
                  <Button
                    className="ml-auto"
                    disabled={startChat.isPending}
                    onClick={() => onChat(project.repository)}
                  >
                    {chatIn(project.repository) ? t('projects.chat.open') : t('projects.chat.start')}
                  </Button>
                  <Button variant="ghost" onClick={() => setManaging(project)}>
                    {t('projects.manage.open')}
                  </Button>
                </p>
              )}
            </Card>
          );
        })}
      </div>

      {outside.length > 0 && (
        <Card className="mt-3.5">
          <header className="flex items-baseline justify-between gap-4">
            <span className="text-[0.95rem] font-semibold">{t('projects.outside.title')}</span>
            <span className="font-mono text-[0.78rem] tabular-nums text-ink-faint">{outside.length}</span>
          </header>
          <p className="mt-1.5 text-[0.85rem] text-ink-soft">{t('projects.outside.body')}</p>
          <ul className="m-0 mt-2 list-none p-0">
            {outside.map((project) => {
              const counts = indexed(project.repository);
              return (
                <li
                  key={project.repository}
                  className="flex items-baseline justify-between gap-4 border-t border-line py-1.5 text-[0.9rem] first:border-t-0"
                >
                  <span>{project.repository}</span>
                  <span className="font-mono text-[0.72rem] text-ink-faint">
                    {counts && counts.total > 0
                      ? t('projects.outside.readable', { count: counts.total.toLocaleString() })
                      : '—'}
                  </span>
                </li>
              );
            })}
          </ul>
          <p className="mt-3 rounded-control bg-accent-soft px-3 py-2.5 font-mono text-[0.8rem]">
            {t('projects.outside.join')}
          </p>
        </Card>
      )}
    </section>
  );
}

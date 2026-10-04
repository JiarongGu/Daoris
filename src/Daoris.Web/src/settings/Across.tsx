import { useTranslation } from 'react-i18next';
import { Card, Chip, Inline, Prose, SectionTitle, Segmented, SettingRow } from '../ui';
import { OnItsPage } from './OnItsPage';

/** What said whether a checkout is read across (D107): set for its repository, for its workspace, or neither. */
export type ReadSource = 'repository' | 'workspace' | 'default';

/** One repository here, as the driver's ACROSS answer tells it. */
export type RepositoryAcross = {
  repository: string;
  workspace: string;
  /** Whether it has a checkout on this machine; one without is set by name all the same. */
  checkout: boolean;
  /** Whether agents outside it read its checkout, as the driver resolved it. */
  read: boolean;
  source: ReadSource;
  /** What its sessions were declared to write into. */
  writesTo: string[];
};

/** A change to reading: a repository's or a workspace's, cleared when it carries no `read`. */
export type ReadChange = { repository?: string; workspace?: string; read?: boolean };

/** A relationship declared, or taken back. */
export type WriteChange = { repository: string; to: string; allow: boolean };

type Choice = 'inherit' | 'on' | 'off';

/**
 * Reading across repositories (READ1, D107): whether agents outside a workspace's repositories read their checkouts —
 * sessions in its other repositories, and Ask Daoris — for each repository there that sets none of its own.
 *
 * @remarks
 * **What each row shows is the driver's own resolution**, read rather than recomputed, so the page cannot name a reading
 * a session would not get. Each control is the screen's half of `daoris driver across --workspace` (D50), and a refusal
 * comes back as the driver's sentence.
 *
 * **A repository's own reading and what its sessions also write into have one home, its Setup** (UX6f, D150 §1, §3.1):
 * its row left this list, which keeps a line naming the repositories that set either, each a door to its Setup, until UX6g
 * moves the workspace's default too.
 */
export function AcrossList({ repositories, workspaceReads, busy, onRead, onOpen }: {
  repositories: RepositoryAcross[];
  /** What each workspace sets, by name. A workspace with no repository here still shows its own. */
  workspaceReads: { workspace: string; read: boolean }[];
  busy?: boolean;
  onRead: (change: ReadChange) => void;
  /** Open a repository's page at Setup, or with null Repositories at Setup. */
  onOpen?: (repository: string | null) => void;
}) {
  const { t } = useTranslation();
  const circles = [...new Set([...repositories.map((one) => one.workspace), ...workspaceReads.map((w) => w.workspace)])]
    .sort((a, b) => a.localeCompare(b));
  const state = (read: boolean) => t(read ? 'settings.across.on' : 'settings.across.off');
  const options = (inherited: boolean): { value: Choice; label: string }[] => [
    { value: 'inherit', label: t('settings.across.inherited', { state: state(inherited) }) },
    { value: 'on', label: t('settings.across.on') },
    { value: 'off', label: t('settings.across.off') },
  ];
  const chosen = (set: boolean | undefined): Choice => (set === undefined ? 'inherit' : set ? 'on' : 'off');
  const read = (choice: Choice) => (choice === 'inherit' ? {} : { read: choice === 'on' });

  return (
    <Card id="settings-across" className="scroll-mt-3">
      <SectionTitle>{t('settings.across.title')}</SectionTitle>
      <Prose className="mt-1 text-small text-ink-soft"><Inline text={t('settings.across.body')} /></Prose>

      {circles.length === 0 && <Prose className="mt-3">{t('settings.across.none')}</Prose>}

      {circles.map((workspace) => {
        const shared = workspaceReads.find((w) => w.workspace === workspace)?.read;
        // A repository sets its own here by its reading or by a relationship it declared: either is the person's.
        const own = repositories
          .filter((one) => one.workspace === workspace && (one.source === 'repository' || one.writesTo.length > 0))
          .map((one) => one.repository);
        return (
          <section key={workspace} aria-label={workspace} className="mt-3 border-t border-line pt-3">
            <SettingRow
              label={<Chip accent>{workspace}</Chip>}
              hint={t('settings.across.workspaceHint')}
              control={(
                <Segmented<Choice>
                  label={t('settings.across.workspaceField', { workspace })}
                  value={chosen(shared)}
                  options={options(true)}
                  onChange={(choice) => !busy && onRead({ workspace, ...read(choice) })}
                />
              )}
            />
            <OnItsPage own={own} onOpen={onOpen} />
          </section>
        );
      })}
    </Card>
  );
}

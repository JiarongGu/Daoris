import { useTranslation } from 'react-i18next';
import { Button, Card, Chip, Icon, Inline, Prose, SectionTitle, Segmented, SelectField, SettingRow } from '../ui';

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
 * Reading and writing across repositories (READ1, D107): whether agents outside each repository read its
 * checkout — sessions in its workspace's other repositories, and Ask Daoris — and which repositories'
 * sessions may also write into another.
 *
 * @remarks
 * **What each row shows is the driver's own resolution**, read rather than recomputed, so the page cannot
 * name a reading a session would not get. Each control is the screen's half of `daoris driver across`
 * (D50), and a refusal comes back as the driver's sentence. A relationship is offered only toward another
 * repository of the same workspace, because the driver applies none across one (D48).
 */
export function AcrossList({ repositories, workspaceReads, busy, onRead, onWrite }: {
  repositories: RepositoryAcross[];
  /** What each workspace sets, by name. A workspace with no repository here still shows its own. */
  workspaceReads: { workspace: string; read: boolean }[];
  busy?: boolean;
  onRead: (change: ReadChange) => void;
  onWrite: (change: WriteChange) => void;
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
        const members = repositories.filter((one) => one.workspace === workspace);
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
            {members.map((one) => {
              const candidates = members
                .map((other) => other.repository)
                .filter((name) => name !== one.repository && !one.writesTo.some((to) => to.toLowerCase() === name.toLowerCase()));
              return (
                <SettingRow
                  key={one.repository}
                  label={one.repository}
                  hint={[
                    one.read ? t('settings.across.readBy', { workspace }) : t('settings.across.readByNone'),
                    t(`settings.across.from.${one.source}`, { workspace }),
                    ...(one.checkout ? [] : [t('settings.across.noCheckout')]),
                  ].join(' ')}
                  control={(
                    <Segmented<Choice>
                      label={t('settings.across.repositoryField', { repository: one.repository })}
                      value={chosen(one.source === 'repository' ? one.read : undefined)}
                      options={options(shared ?? true)}
                      onChange={(choice) => !busy && onRead({ repository: one.repository, ...read(choice) })}
                    />
                  )}
                >
                  {(one.writesTo.length > 0 || candidates.length > 0) && (
                    <div className="flex flex-wrap items-center gap-2 text-small text-ink-soft">
                      <span>{t(one.writesTo.length > 0 ? 'settings.across.writesInto' : 'settings.across.writesNone')}</span>
                      {one.writesTo.length > 0 && (
                        <ul aria-label={t('settings.across.writesIntoList', { repository: one.repository })} className="m-0 flex list-none flex-wrap gap-1.5 p-0">
                          {one.writesTo.map((to) => (
                            <li key={to} className="inline-flex items-center gap-0.5">
                              <Chip>{to}</Chip>
                              <Button
                                variant="ghost"
                                disabled={busy}
                                aria-label={t('settings.across.stopWriting', { repository: one.repository, to })}
                                onClick={() => onWrite({ repository: one.repository, to, allow: false })}
                              >
                                <Icon name="x" size={12} />
                              </Button>
                            </li>
                          ))}
                        </ul>
                      )}
                      {candidates.length > 0 && (
                        <SelectField
                          value=""
                          disabled={busy}
                          ariaLabel={t('settings.across.addWrite', { repository: one.repository })}
                          placeholder={t('settings.across.addWritePlaceholder')}
                          options={candidates.map((name) => ({ value: name, label: name }))}
                          onChange={(to) => to && onWrite({ repository: one.repository, to, allow: true })}
                        />
                      )}
                    </div>
                  )}
                </SettingRow>
              );
            })}
          </section>
        );
      })}
    </Card>
  );
}

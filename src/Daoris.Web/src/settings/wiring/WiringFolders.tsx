import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { SystemWiring, WiringFolder } from '../../data/wiring';
import { Button, MiddlePath, Pill } from '../../design/ui';
import { moment, size } from '../../locales/format';
import { Unread, WiringCard } from './WiringParts';

/** The pill each way the home stands wears; an install is no event, and a home named for one start is a note. */
const KIND_TONE: Record<string, string> = { install: 'neutral', development: 'taken', named: 'open' };

/** The catalogue word for each kind of home, in `settings.daoris.pill.*`. */
const KIND_WORD: Record<string, string> = { install: 'install', development: 'development', named: 'named' };

/** A size in its unit, with the time of its count; or why there is none yet. */
function Size({ folder, measuring }: { folder: Pick<WiringFolder, 'present' | 'bytes' | 'bytesWhy' | 'measured'>; measuring: boolean }) {
  const { t } = useTranslation();
  if (!folder.present) return <span className="text-small text-ink-faint">{t('settings.daoris.folder.absent')}</span>;
  if (folder.bytes === null) return <Unread why={measuring && !folder.bytesWhy ? 'measuring' : folder.bytesWhy} />;
  return (
    <span className="text-small text-ink-soft" title={folder.measured ? t('settings.daoris.folder.measured', { when: moment(folder.measured) }) : undefined}>
      {size(folder.bytes)}
    </span>
  );
}

/**
 * One folder of the list: its name, its path on one line cut in the middle, its size and one *Open folder*, on the list's
 * four columns; what it holds besides (an entry at its old place, the Downloads' clearing, the home's notes) under it.
 */
function FolderRow({ name, path, why, size: measured, open, children }: {
  name: string; path?: string | null; why?: string | null; size?: ReactNode; open?: ReactNode; children?: ReactNode;
}) {
  return (
    <li className="col-span-4 grid grid-cols-subgrid items-center gap-y-1 border-t border-line py-2 first:border-t-0 first:pt-0 @max-[32rem]:col-span-1 @max-[32rem]:grid-cols-1">
      <span className="text-body font-medium text-ink">{name}</span>
      <span className="min-w-0">{path === undefined ? null : path ? <MiddlePath path={path} /> : <Unread why={why} />}</span>
      <span className="whitespace-nowrap">{measured}</span>
      <span>{open}</span>
      {children && <div className="col-span-full grid gap-1">{children}</div>}
    </li>
  );
}

/**
 * Folders: one list of what Daoris keeps on this disk, the home first, then each top folder by its window name, a row
 * each with its path, its size and *Open folder*; an entry still at its old place under its folder, and what is not Daoris's
 * last. Sizes are measured, never guessed: a row says *Measuring…* until the walk has counted it, its count's time on hover,
 * and *Measure again* asks anew. Nothing here changes a folder.
 *
 * @remarks
 * A molecule: drawn from props, so every state is reachable without a shell. The notices are the host's own sentences, passed
 * through; the account's standing is read from the state's field, never from a notice's English.
 */
export function WiringFolders({ wiring, busy = false, onMeasure, downloads, onDownloads, onOpen }: {
  wiring: Pick<SystemWiring, 'folder' | 'folders' | 'leftOver' | 'measuring'>;
  busy?: boolean;
  onMeasure: () => void;
  /** *Open folder* on the home (`home`) or a top folder, by the id the read gave; absent where the shell offers none. */
  onOpen?: (id: string) => void;
  /** The Downloads row's clearing, open: drawn under that row in place of its press. */
  downloads?: ReactNode;
  /** Opens the clearing's list; absent where the shell offers none. */
  onDownloads?: () => void;
}) {
  const { t } = useTranslation();
  const { folder, folders, leftOver, measuring } = wiring;
  const opener = (id: string, shown: boolean) => (onOpen && shown
    ? <Button variant="ghost" onClick={() => onOpen(id)}>{t('settings.daoris.button.openFolder')}</Button>
    : null);
  const note = (text: string, tone?: 'accent' | 'warn') => (
    <p className={tone ? `m-0 border-l-[3px] bg-page/60 px-3.5 py-2 text-small text-ink-soft ${tone === 'warn' ? 'border-warn' : 'border-accent'}` : 'm-0 text-small text-ink-faint'}>{text}</p>
  );
  return (
    <WiringCard id="settings-daoris-folders" title={t('settings.daoris.card.folders')} aside={{ label: t('settings.daoris.button.measure'), busy: busy || measuring, onPress: onMeasure }}>
      <div className="@container">
        <ul className="m-0 grid list-none grid-cols-[max-content_minmax(0,1fr)_max-content_max-content] gap-x-4 p-0 @max-[32rem]:grid-cols-1">
          <FolderRow
            name={t('settings.daoris.label.home')}
            path={folder.path}
            why={folder.pathWhy}
            size={folder.kind ? <Pill tone={KIND_TONE[folder.kind] ?? 'neutral'}>{t(`settings.daoris.pill.${KIND_WORD[folder.kind] ?? 'install'}`)}</Pill> : null}
            open={opener('home', Boolean(folder.path))}
          >
            {(folder.account || folder.accountNames === 'data' || folder.notice || folder.hostNotice) && (
              <>
                {folder.account && note(t(`settings.daoris.account.${folder.account}`, { defaultValue: '' }))}
                {folder.accountNames === 'data' && note(t('settings.daoris.account.data'))}
                {folder.notice && note(folder.notice, 'accent')}
                {folder.hostNotice && note(folder.hostNotice, 'warn')}
              </>
            )}
          </FolderRow>
          {folders.map((each) => (
            <FolderRow
              key={each.id}
              name={t(`settings.daoris.label.folder.${each.id}`)}
              path={each.path}
              why={each.pathWhy}
              size={<Size folder={each} measuring={measuring} />}
              open={opener(each.id, each.present && Boolean(each.path))}
            >
              {(each.old.length > 0 || (each.id === 'cache' && each.present && (downloads || onDownloads))) && (
                <>
                  {each.old.length > 0 && (
                    <ul className="m-0 grid list-none gap-1 p-0">
                      {each.old.map((entry) => (
                        <li key={entry.entry} className="flex flex-wrap items-baseline gap-x-3 text-small text-ink-faint">
                          <span className="font-mono text-ink-soft">{entry.entry}</span>
                          {entry.bytes !== null && <span>{size(entry.bytes)}</span>}
                          <span>{t(`settings.daoris.old.${entry.why}`, { defaultValue: entry.why })}</span>
                        </li>
                      ))}
                    </ul>
                  )}
                  {each.id === 'cache' && each.present && (downloads ?? (onDownloads && (
                    <p className="m-0"><Button variant="ghost" onClick={onDownloads}>{t('settings.daoris.downloads.open')}</Button></p>
                  )))}
                </>
              )}
            </FolderRow>
          ))}
          {leftOver.length > 0 && (
            <FolderRow name={t('settings.daoris.label.leftOver')}>
              <ul className="m-0 grid list-none gap-1.5 p-0">
                {leftOver.map((each) => (
                  <li key={each.name} className="flex flex-wrap items-baseline gap-x-3 text-small text-ink-faint">
                    <span className="font-mono text-ink-soft">{each.name}</span>
                    <span>{each.bytes !== null ? size(each.bytes) : <Unread why={measuring ? 'measuring' : 'not-measured'} />}</span>
                    <span>{t(`settings.daoris.left.${each.why}`)}</span>
                  </li>
                ))}
              </ul>
            </FolderRow>
          )}
        </ul>
      </div>
    </WiringCard>
  );
}

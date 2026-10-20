import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { SystemWiring, WiringFolder } from '../../data/wiring';
import { Button, PathText, Pill, SettingRow } from '../../design/ui';
import { moment, size } from '../../locales/format';
import { Fact, Unread, WiringCard } from './WiringParts';

/** The pill each way the home stands wears; an install is no event, and a home named for one start is a note. */
const KIND_TONE: Record<string, string> = { install: 'neutral', development: 'taken', named: 'open' };

/** The catalogue word for each kind of home, in `settings.daoris.pill.*`. */
const KIND_WORD: Record<string, string> = { install: 'install', development: 'development', named: 'named' };

/** A size in its unit, with when it was counted; or why there is none yet. */
function Size({ folder, measuring }: { folder: Pick<WiringFolder, 'present' | 'bytes' | 'bytesWhy' | 'measured'>; measuring: boolean }) {
  const { t } = useTranslation();
  if (!folder.present) return <span className="text-small text-ink-faint">{t('settings.daoris.folder.absent')}</span>;
  if (folder.bytes === null) return <Unread why={measuring && !folder.bytesWhy ? 'measuring' : folder.bytesWhy} />;
  return (
    <span className="text-small text-ink-soft">
      {size(folder.bytes)}
      {folder.measured && <span className="text-ink-faint">{' · '}{t('settings.daoris.folder.measured', { when: moment(folder.measured) })}</span>}
    </span>
  );
}

/**
 * Folders (the this-Daoris design §3.1): the Daoris folder with how it stands to the account's `DAORIS_HOME` and the two notices
 * the start carried, then each top folder by its window name with its path, its size and when it was counted, an entry still at
 * its old place under its folder, and what is not Daoris's. Sizes are measured, never guessed: a row says *Measuring…* until
 * the walk has counted it, and *Measure again* asks anew. Nothing here opens or changes a folder.
 *
 * @remarks
 * A molecule: drawn from props, so every state is reachable without a shell. The notices are the host's own sentences, passed
 * through as the Driver page passed them; the account's standing is read from the state's field, never from a notice's English.
 */
export function WiringFolders({ wiring, busy = false, onMeasure, downloads, onDownloads }: {
  wiring: Pick<SystemWiring, 'folder' | 'folders' | 'leftOver' | 'measuring'>;
  busy?: boolean;
  onMeasure: () => void;
  /** The Downloads row's clearing, open (CACHECLEAR1): drawn under that row in place of its press. */
  downloads?: ReactNode;
  /** Opens the clearing's list; absent where the shell offers none. */
  onDownloads?: () => void;
}) {
  const { t } = useTranslation();
  const { folder, folders, leftOver, measuring } = wiring;
  return (
    <WiringCard id="settings-daoris-folders" title={t('settings.daoris.card.folders')} aside={{ label: t('settings.daoris.button.measure'), busy: busy || measuring, onPress: onMeasure }}>
      <SettingRow
        label={t('settings.daoris.label.home')}
        hint={t('settings.daoris.terminal')}
        control={<Fact value={folder.path} why={folder.pathWhy}><PathText path={folder.path ?? ''} className="text-small text-ink-soft" /></Fact>}
      >
        <div className="grid gap-2">
          {folder.kind && <p className="m-0"><Pill tone={KIND_TONE[folder.kind] ?? 'neutral'}>{t(`settings.daoris.pill.${KIND_WORD[folder.kind] ?? 'install'}`)}</Pill></p>}
          {folder.account && <p className="m-0 text-small text-ink-soft">{t(`settings.daoris.account.${folder.account}`, { defaultValue: '' })}</p>}
          {folder.accountNames === 'data' && <p className="m-0 text-small text-ink-soft">{t('settings.daoris.account.data')}</p>}
          {folder.notice && <p className="m-0 border-l-[3px] border-accent bg-page/60 px-3.5 py-2 text-body text-ink-soft">{folder.notice}</p>}
          {folder.hostNotice && <p className="m-0 border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">{folder.hostNotice}</p>}
        </div>
      </SettingRow>
      {folders.map((each) => (
        <SettingRow
          key={each.id}
          label={t(`settings.daoris.label.folder.${each.id}`)}
          hint={each.path ? <PathText path={each.path} /> : <Unread why={each.pathWhy} />}
          control={<Size folder={each} measuring={measuring} />}
        >
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
        </SettingRow>
      ))}
      {leftOver.length > 0 && (
        <SettingRow label={t('settings.daoris.label.leftOver')}>
          <ul className="m-0 grid list-none gap-1.5 p-0">
            {leftOver.map((each) => (
              <li key={each.name} className="flex flex-wrap items-baseline gap-x-3 text-small text-ink-faint">
                <span className="font-mono text-ink-soft">{each.name}</span>
                <span>{each.bytes !== null ? size(each.bytes) : <Unread why={measuring ? 'measuring' : 'not-measured'} />}</span>
                <span>{t(`settings.daoris.left.${each.why}`)}</span>
              </li>
            ))}
          </ul>
        </SettingRow>
      )}
    </WiringCard>
  );
}

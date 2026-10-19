import { useTranslation } from 'react-i18next';
import type { SystemWiring, WiringFile } from '../../data/wiring';
import { PathText, Pill, SettingRow } from '../../design/ui';
import { moment, size } from '../../locales/format';
import { Door, Fact, Fold, Unread, WiringCard } from './WiringParts';

/** Each entry's file name, as D172's `data/` and `profiles/` spell it: a name on disk, never translated. */
const FILE_NAME: Record<string, string> = {
  driver: 'driver.json', harnesses: 'harnesses.json', accounts: 'accounts.json', permissions: 'permissions.json',
  tools: 'tools.json', 'plugin-state': 'plugins.json', 'browser-settings': 'browser/settings.json', update: 'update.json',
  remotes: 'remotes.json', keys: 'keys.json',
  usage: 'usage.json', landings: 'landings.json', 'session-branches': 'session-branches.json', cooling: 'cooling.json',
  windows: 'windows.json', reads: 'reads.json', sessions: 'sessions/', knowledge: 'knowledge.db',
};

/** The doors a file may have: where it is edited. A door the page does not know has no press. */
const DOORS = new Set(['driver', 'agents', 'tools', 'plugins', 'browser', 'workspace']);

function FileRow({ file, onDoor }: { file: WiringFile; onDoor: (door: string) => void }) {
  const { t, i18n } = useTranslation();
  const holds = `settings.daoris.file.${file.id}`;
  const readable = file.present && !file.record;
  return (
    <SettingRow
      label={<span className="font-mono">{FILE_NAME[file.id] ?? file.id}</span>}
      hint={i18n.exists(holds) ? t(holds) : undefined}
      control={(
        <>
          {file.present
            ? (
              <span className="text-small text-ink-soft">
                <Fact value={file.bytes} why={file.bytesWhy}>{size(file.bytes ?? 0)}</Fact>
                {file.written && <span className="text-ink-faint">{' · '}{t('settings.daoris.files.written', { when: moment(file.written) })}</span>}
              </span>
            )
            : <span className="text-small text-ink-faint">{t('settings.daoris.folder.absent')}</span>}
          {file.door && DOORS.has(file.door) && <Door label={t(`settings.daoris.button.${file.door}`)} onPress={() => onDoor(file.door!)} />}
        </>
      )}
    >
      <div className="grid gap-1">
        <Fact value={file.path} why={file.pathWhy}><PathText path={file.path ?? ''} /></Fact>
        {file.overriddenBy && <p className="m-0 text-small text-ink-soft">{t('settings.daoris.files.overridden', { name: file.overriddenBy })}</p>}
        {readable && file.reads === true && <p className="m-0"><Pill tone="done">{t('settings.daoris.pill.reads')}</Pill></p>}
        {readable && file.reads === false && (
          <p className="m-0 text-small text-ink-soft">
            <Pill tone="declined">{t('settings.daoris.pill.unreadable')}</Pill>{' '}{t('settings.daoris.files.refuse')}
          </p>
        )}
        {readable && file.readsWhy && file.readsWhy !== 'unparsed' && <Unread why={file.readsWhy} />}
      </div>
    </SettingRow>
  );
}

/**
 * Settings files (the this-Daoris design §3.4): one row per file a person's choice lives in, each saying what it holds, where
 * it is, its size and when it was last written, whether it reads, the variable that overrides it where one is set, and the
 * door to where it is edited. The records fold under, with no door. **A file's contents are never shown** (§5): the read
 * parses a file to say whether it reads, and the page carries the verdict.
 *
 * @remarks
 * A molecule: drawn from props. A door is a press that goes where the file is edited; this page edits nothing.
 */
export function WiringFiles({ files, onDoor }: { files: SystemWiring['files']; onDoor: (door: string) => void }) {
  const { t } = useTranslation();
  const settings = files.filter((file) => !file.record);
  const records = files.filter((file) => file.record);
  return (
    <WiringCard id="settings-daoris-files" title={t('settings.daoris.card.files')}>
      {settings.map((file) => <FileRow key={file.id} file={file} onDoor={onDoor} />)}
      {records.length > 0 && (
        <Fold label={t('settings.daoris.files.records')}>
          {records.map((file) => <FileRow key={file.id} file={file} onDoor={onDoor} />)}
        </Fold>
      )}
    </WiringCard>
  );
}

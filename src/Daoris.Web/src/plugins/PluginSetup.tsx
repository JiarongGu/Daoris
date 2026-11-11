import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Button, CheckField, Inline, Pill, SelectField } from '../design/ui';
import { authoredText } from '../locales/authored';
import { PageSection } from '../work/frame/ViewMain';
import {
  type CommandShown, declaresInterfaces, type InterfaceRefusal, type InterfacesShown, placesWithWords, type StorageShown, switchedOn,
  type SwitchWord, type ViewShown,
} from './interfaces';

/** A place the person may switch a part at: `machine`, `workspace:<name>` or `repository:<id>`, a repository with its workspace. */
export type PlaceShown = { key: string; workspace?: string | null };

const MACHINE = 'machine';

/** A place's key in the words the page says it in. */
export function placeLabel(t: TFunction, key: string): string {
  if (key === MACHINE) return t('plugin.setup.place.machine');
  const [kind, ...rest] = key.split(':');
  const name = rest.join(':');
  return kind === 'workspace' || kind === 'repository' ? t(`plugin.setup.place.${kind}`, { name }) : key;
}

/** The marks a command wears: what it asks of the person, of the world and of agents. */
function CommandMarks({ command }: { command: CommandShown }) {
  const { t } = useTranslation();
  const marks = [
    command.interactive && 'asks', command.writes && 'writes', command.tool && 'tool', command.adopts && 'adopts',
  ].filter((mark): mark is string => typeof mark === 'string');
  return marks.length > 0 ? <>{marks.map((mark) => <Pill key={mark} tone="neutral">{t(`plugin.setup.mark.${mark}`)}</Pill>)}</> : null;
}

/** An entry refused alone: its name in the mono face, *not read* and the reader's sentence, with nothing to press. */
function RefusedRow({ refusal }: { refusal: InterfaceRefusal }) {
  const { t } = useTranslation();
  return (
    <li className="flex flex-col gap-0.5 text-small text-ink-faint" aria-disabled="true">
      <span className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5">
        {refusal.entry && <code className="font-mono text-meta">{refusal.entry}</code>}
        <Pill tone="declined">{t('plugin.setup.refusedWord')}</Pill>
      </span>
      <span><Inline text={refusal.problem} /></span>
    </li>
  );
}

/**
 * Each command a plugin declares: its title in the viewer's language, its name in the mono face, its marks and its own words; with
 * `onRun`, *Run*, disabled with the reason where it cannot run here. Then each command refused alone, drawn disabled with why.
 */
export function CommandRows({ commands, refused, cannot, running, onRun }: {
  commands: CommandShown[];
  refused: InterfaceRefusal[];
  /** Why no command runs here (the plugin off, or its commands switched off at the place); none where they run. */
  cannot?: string | null;
  /** The command whose card is open, whose Run waits for it. */
  running?: string | null;
  onRun?: (command: CommandShown) => void;
}) {
  const { t, i18n } = useTranslation();
  return (
    <>
      {cannot && onRun && <p className="m-0 mb-2 text-small text-ink-soft"><Inline text={cannot} /></p>}
      <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
        {commands.map((command) => {
          const title = authoredText(command.title, i18n.language);
          return (
            <li key={command.name} className="flex flex-wrap items-start gap-x-3 gap-y-1">
              <span className="flex min-w-0 flex-1 basis-64 flex-col gap-0.5 text-small text-ink-soft">
                <span className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
                  <span className="font-medium text-ink">{title}</span>
                  <code className="font-mono text-meta text-ink-faint">{command.name}</code>
                  <CommandMarks command={command} />
                </span>
                {authoredText(command.about, i18n.language) && <span><Inline text={authoredText(command.about, i18n.language)} /></span>}
              </span>
              {onRun && (
                <Button
                  disabled={Boolean(cannot) || running === command.name}
                  aria-label={t('plugin.setup.runNamed', { command: title })}
                  onClick={() => onRun(command)}
                >
                  {t('plugin.setup.run')}
                </Button>
              )}
            </li>
          );
        })}
        {refused.map((refusal) => <RefusedRow key={`${refusal.entry ?? ''}-${refusal.problem}`} refusal={refusal} />)}
      </ul>
    </>
  );
}

/** What the private storage keeps, in the plugin's words, and whether a removal asks it to sign out first. */
export function StorageRows({ storage }: { storage: StorageShown }) {
  const { t, i18n } = useTranslation();
  return (
    <div className="flex flex-col gap-1 text-small text-ink-soft">
      {authoredText(storage.for, i18n.language) && <span><Inline text={authoredText(storage.for, i18n.language)} /></span>}
      {storage.forget && !storage.forgetDisabled && <span><Inline text={t('plugin.setup.forget', { command: storage.forget })} /></span>}
      {storage.forgetDisabled && <span><Inline text={t('plugin.setup.forgetNot', { why: storage.forgetDisabled })} /></span>}
      <span className="text-ink-faint">{t('plugin.setup.storageBody')}</span>
    </div>
  );
}

/** Each view a plugin declares, where it may stand, the places it is about and what it is told; then each refused alone. */
export function ViewRows({ views, refused }: { views: ViewShown[]; refused: InterfaceRefusal[] }) {
  const { t, i18n } = useTranslation();
  return (
    <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
      {views.map((view) => (
        <li key={view.id} className="flex flex-col gap-0.5 text-small text-ink-soft">
          <span className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
            <span className="font-medium text-ink">{authoredText(view.title, i18n.language)}</span>
            <code className="font-mono text-meta text-ink-faint">{view.id}</code>
          </span>
          <span>
            {view.where.map((where) => t(`plugin.setup.viewWhere.${where}`, { defaultValue: where })).join(' · ')}
            {' · '}
            {t('plugin.setup.viewPlaces', { places: view.places.map((place) => t(`plugin.setup.placeKind.${place}`, { defaultValue: place })).join(', ') })}
          </span>
          {view.context.map((told) => <span key={told}>{t(`plugin.setup.viewContext.${told}`, { defaultValue: told })}</span>)}
        </li>
      ))}
      {refused.map((refusal) => <RefusedRow key={`${refusal.entry ?? ''}-${refusal.problem}`} refusal={refusal} />)}
    </ul>
  );
}

/**
 * What an install adds that Daoris reaches it through, listed before *Install*: each command with its marks, what its storage keeps
 * and whether a removal signs it out, each view, and each entry refused alone. None where it declares none.
 */
export function InterfaceList({ interfaces }: { interfaces?: InterfacesShown }) {
  const { t } = useTranslation();
  if (!interfaces || !declaresInterfaces(interfaces)) return null;
  const refusedOf = (name: string) => interfaces.refused.filter((each) => each.interface === name);
  return (
    <PageSection title={t('plugin.setup.interfaces')}>
      <div className="flex flex-col gap-3">
        {(interfaces.commands.length > 0 || refusedOf('commands').length > 0) && (
          <CommandRows commands={interfaces.commands} refused={refusedOf('commands')} />
        )}
        {interfaces.storage && <StorageRows storage={interfaces.storage} />}
        {(interfaces.views.length > 0 || refusedOf('views').length > 0 || refusedOf('storage').length > 0) && (
          <ViewRows views={interfaces.views} refused={[...refusedOf('views'), ...refusedOf('storage')]} />
        )}
      </div>
    </PageSection>
  );
}

/** The parts a plugin's switches name: each interface it declares, then each point it speaks on. */
function switchable(interfaces: InterfacesShown, points: string[]): string[] {
  return [
    ...(interfaces.commands.length > 0 ? ['commands'] : []),
    ...(interfaces.commands.some((command) => command.tool) ? ['tools'] : []),
    ...(interfaces.views.length > 0 ? ['views'] : []),
    ...(interfaces.storage ? ['storage'] : []),
    ...points,
  ];
}

/**
 * ***Where it is on***: a row per interface and point, a column per place, the machine's first, then each place that holds a word
 * and each the person added; each box the resolver's reading at that place, its press the person's word there. `storage` is
 * switched at this machine alone.
 */
export function WhereItIsOn({ interfaces, points, switches, places, busy = false, onSwitch }: {
  interfaces: InterfacesShown;
  points: string[];
  switches: SwitchWord[];
  /** Each workspace and repository the person may add a column for. */
  places: PlaceShown[];
  busy?: boolean;
  onSwitch: (what: string, place: string, on: boolean) => void;
}) {
  const { t } = useTranslation();
  const [added, setAdded] = useState<string[]>([]);
  const rows = switchable(interfaces, points);
  const worded = rows.flatMap((what) => placesWithWords(switches, what));
  const columns = [...new Set([MACHINE, ...worded, ...added])];
  const offered = places.filter((place) => !columns.includes(place.key));
  const workspaceOf = (key: string) => places.find((place) => place.key === key)?.workspace ?? null;
  const whatLabel = (what: string) => t(`plugin.setup.what.${what}`, { defaultValue: what });
  if (rows.length === 0) return null;
  return (
    <div className="flex flex-col gap-2">
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-small">
          <thead>
            <tr>
              <th scope="col" className="py-1 pr-3 text-left font-normal text-ink-faint"><span className="sr-only">{t('plugin.setup.on')}</span></th>
              {columns.map((place) => (
                <th key={place} scope="col" className="px-2 py-1 text-left font-normal text-ink-faint">{placeLabel(t, place)}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((what) => (
              <tr key={what} className="border-t border-line">
                <th scope="row" className="py-1.5 pr-3 text-left font-normal text-ink-soft">
                  {points.includes(what) ? <code className="font-mono text-meta text-ink">{what}</code> : whatLabel(what)}
                </th>
                {columns.map((place) => (
                  <td key={place} className="px-2 py-1.5">
                    {what === 'storage' && place !== MACHINE ? (
                      <span className="text-ink-faint" title={t('plugin.setup.machineOnly')}>—</span>
                    ) : (
                      <CheckField
                        hideLabel
                        disabled={busy}
                        checked={switchedOn(switches, what, place, workspaceOf(place))}
                        onChange={(on) => onSwitch(what, place, on)}
                        label={t('plugin.setup.switchAt', { what: whatLabel(what), place: placeLabel(t, place) })}
                      />
                    )}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {offered.length > 0 && (
        <div className="max-w-72">
          <SelectField
            value=""
            placeholder={t('plugin.setup.addPlace')}
            ariaLabel={t('plugin.setup.addPlace')}
            options={offered.map((place) => ({ value: place.key, label: placeLabel(t, place.key) }))}
            onChange={(key) => setAdded((was) => [...was, key])}
          />
        </div>
      )}
      <p className="m-0 text-meta text-ink-faint">{t('plugin.setup.onBody')}</p>
    </div>
  );
}

/**
 * **A plugin's *Set up* tab**: *Commands*, each with *Run*; *Storage*, what it keeps and whether a removal signs it out;
 * *Views*, as declared; the parts this build does not know, named once; and *Where it is on*.
 *
 * @remarks
 * **A molecule**: a Run and a switch go out. A Run is offered only where the plugin is on and sound and its commands are on at the
 * place in force; otherwise it is drawn disabled with the reason.
 */
export function PluginSetup({ id, enabled, refused, interfaces, points, switches, place, places, running, switching, onRun, onSwitch }: {
  id: string;
  enabled: boolean;
  /** The plugin is refused whole, so nothing of it runs. */
  refused: boolean;
  interfaces: InterfacesShown;
  points: string[];
  switches: SwitchWord[];
  /** The place a Run runs at: the window's scope, else the machine. */
  place: string;
  places: PlaceShown[];
  running?: string | null;
  switching?: boolean;
  onRun?: (command: CommandShown) => void;
  onSwitch?: (what: string, place: string, on: boolean) => void;
}) {
  const { t } = useTranslation();
  const atPlace = places.find((each) => each.key === place)?.workspace ?? null;
  const cannot = refused || !enabled
    ? t('plugin.setup.offWhole')
    : !switchedOn(switches, 'commands', place, atPlace) ? t('plugin.setup.offHere', { place: placeLabel(t, place) }) : null;
  const refusedOf = (name: string) => interfaces.refused.filter((each) => each.interface === name);
  return (
    <>
      {(interfaces.commands.length > 0 || refusedOf('commands').length > 0) && (
        <PageSection title={t('plugin.setup.commands')}>
          <CommandRows commands={interfaces.commands} refused={refusedOf('commands')} cannot={cannot} running={running} onRun={onRun} />
          <p className="m-0 mt-2 text-meta text-ink-faint"><Inline text={t('plugin.setup.commandsBody', { id })} /></p>
        </PageSection>
      )}
      {(interfaces.storage || refusedOf('storage').length > 0) && (
        <PageSection title={t('plugin.setup.storage')}>
          {interfaces.storage && <StorageRows storage={interfaces.storage} />}
          {refusedOf('storage').length > 0 && <ViewRows views={[]} refused={refusedOf('storage')} />}
        </PageSection>
      )}
      {(interfaces.views.length > 0 || refusedOf('views').length > 0) && (
        <PageSection title={t('plugin.setup.views')}>
          <ViewRows views={interfaces.views} refused={refusedOf('views')} />
          <p className="m-0 mt-2 text-meta text-ink-faint">{t('plugin.setup.viewsBody')}</p>
        </PageSection>
      )}
      {interfaces.unknown.length > 0 && (
        <p className="m-0 mb-4 text-small text-ink-soft"><Inline text={t('plugin.setup.unknown', { names: interfaces.unknown.map((name) => `\`${name}\``).join(', ') })} /></p>
      )}
      {onSwitch && (
        <PageSection title={t('plugin.setup.on')}>
          <WhereItIsOn interfaces={interfaces} points={points} switches={switches} places={places} busy={switching} onSwitch={onSwitch} />
        </PageSection>
      )}
    </>
  );
}

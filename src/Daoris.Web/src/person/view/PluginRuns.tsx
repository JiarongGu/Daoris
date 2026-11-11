import { useTranslation } from 'react-i18next';
import { Inline } from '../../design/ui';

/**
 * What a plugin's press puts on the card of what it runs, each command as its manifest writes it (`${plugin}` included): the bridge
 * leaves a null out, so a plugin that speaks on no point arrives with no `command`.
 */
export type PressPlugin = {
  id: string;
  name: string;
  version: string;
  command?: string[] | null;
  points: string[];
  harnesses: { name: string; command: string[] }[];
  servers: { name: string; command: string[] }[];
  /** What one of the install's own plugins needs, in its README's words. */
  needs?: string[];
  /** What an update changes, each side as the manifests write it. */
  changes?: { what: string; was: string; now: string }[];
};

/** A command as a terminal takes it: a word holding a space quoted, as the driver spells it. */
const line = (command: string[]) => command.map((word) => (word.includes(' ') ? `"${word}"` : word)).join(' ');

/**
 * What a plugin runs, before the person's yes: its id, the process it starts and the points it speaks on, the harnesses it declares and
 * the servers it hands. Chrome translates; every name and command is the manifest's, as it writes it.
 */
export function PluginRuns({ plugin }: { plugin: PressPlugin }) {
  const { t } = useTranslation();
  const said = (text: string, key: string) => (
    <li key={key} className="m-0 text-meta text-ink-soft"><Inline text={text} /></li>
  );
  return (
    <ul aria-label={t('person.plugin.shows')} className="m-0 mt-1 flex list-none flex-col gap-0.5 p-0">
      {said(t('person.plugin.id', { id: plugin.id }), 'id')}
      {said(plugin.command && plugin.command.length > 0
        ? t('person.plugin.runs', { command: line(plugin.command) })
        : t('person.plugin.runsNothing'), 'runs')}
      {plugin.points.length > 0
        && said(t('person.plugin.points', { points: plugin.points.map((point) => `\`${point}\``).join(', ') }), 'points')}
      {plugin.harnesses.map((harness) =>
        said(t('person.plugin.harness', { name: harness.name, command: line(harness.command) }), `harness-${harness.name}`))}
      {plugin.servers.map((server) =>
        said(t('person.plugin.server', { name: server.name, command: line(server.command) }), `server-${server.name}`))}
      {/* Each side as the manifests write it; a side that is empty is none. */}
      {(plugin.changes ?? []).map((change) =>
        said(t('person.plugin.change', {
          what: t(`plugin.update.what.${change.what}`, { defaultValue: change.what }),
          was: change.was ? `\`${change.was}\`` : t('plugin.update.none'),
          now: change.now ? `\`${change.now}\`` : t('plugin.update.none'),
        }), `change-${change.what}`))}
      {/* The README's own requirement lines, which the person sets up themselves. */}
      {(plugin.needs ?? []).map((need) => said(t('person.plugin.needs', { need }), `need-${need}`))}
    </ul>
  );
}

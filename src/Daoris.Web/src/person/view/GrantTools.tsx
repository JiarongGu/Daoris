import { useTranslation } from 'react-i18next';
import { Inline } from '../../design/ui';

/**
 * What an approval of a plugin's server for Ask Daoris names, as the route's view reads the manifest: the plugin, the server, each tool
 * it reads with and each it writes outside Daoris with, and the writes it accepts; for a withdrawal, those it had accepted. The bridge
 * leaves a false out.
 */
export type PressGrant = {
  plugin: string;
  name: string;
  server: string;
  reads: string[];
  writes: string[];
  accepted: string[];
  withdraw?: boolean;
};

/**
 * Every tool an approval hands Ask Daoris, or a withdrawal takes back: its reads, the writes accepted and those not, each named as the
 * plugin declares it, so the person sees exactly what their yes widens or narrows.
 */
export function GrantTools({ grant }: { grant: PressGrant }) {
  const { t } = useTranslation();
  const tools = (names: string[]) => names.map((name) => `\`${name}\``).join(', ');
  const asked = grant.writes.filter((tool) => !grant.accepted.includes(tool));
  const said = (text: string, key: string) => (
    <li key={key} className="m-0 text-meta text-ink-soft"><Inline text={text} /></li>
  );
  return (
    <ul aria-label={t(grant.withdraw ? 'person.grant.loses' : 'person.grant.shows')} className="m-0 mt-1 flex list-none flex-col gap-0.5 p-0">
      {said(grant.name && grant.name !== grant.plugin
        ? t('person.grant.serverNamed', { server: grant.server, plugin: grant.plugin, name: grant.name })
        : t('person.grant.server', { server: grant.server, plugin: grant.plugin }), 'server')}
      {said(grant.reads.length > 0 ? t('person.grant.reads', { tools: tools(grant.reads) }) : t('person.grant.readsNone'), 'reads')}
      {grant.accepted.length > 0 && said(t('person.grant.writesAccepted', { tools: tools(grant.accepted) }), 'accepted')}
      {!grant.withdraw && asked.length > 0 && said(t('person.grant.writesAsked', { tools: tools(asked) }), 'asked')}
    </ul>
  );
}

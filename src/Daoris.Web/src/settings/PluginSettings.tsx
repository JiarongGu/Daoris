import { useTranslation } from 'react-i18next';
import { Button, Card, Pill, SectionTitle, SettingList, SettingRow, WhyGlyph } from '../design/ui';
import { type HelpServerShown, pluginState, type PluginShown } from '../plugins/catalog';
import { PluginIcon } from '../plugins/PluginIcon';
import { STATE_WORD } from '../plugins/PluginList';

/** A server's approval in a word, as its plugin's page judges it (HELPPLUG1c): changed since approved is a wait that asks again. */
function approval(server: HelpServerShown): string {
  const state = server.state ?? 'waits';
  return state === 'waits' && server.changed ? 'changed' : state;
}

/**
 * **Settings' Plugins section** (PLUGSET1, the modes design §4.1): each installed plugin with what it holds set, its switch's
 * word and each server's approval for Ask Daoris with the writes accepted, and a door to its page, where each is changed. Read
 * only, so a person looking in Settings finds a plugin's settings without a second set of controls: a plugin's record is
 * changed on its own page alone (HELPMODE1c3). An install with none says so.
 *
 * A molecule: every state by props, the door's press out.
 */
export function PluginSettings({ plugins, onOpen }: { plugins: readonly PluginShown[]; onOpen: (id: string) => void }) {
  const { t } = useTranslation();
  return (
    <Card>
      <section aria-label={t('settings.plugins.title')}>
        <SectionTitle>
          <span className="inline-flex items-center gap-1.5">
            {t('settings.plugins.title')}
            <WhyGlyph why={t('settings.plugins.why')} />
          </span>
        </SectionTitle>
        {plugins.length === 0 ? (
          <p className="m-0 text-small text-ink-soft">{t('settings.plugins.none')}</p>
        ) : (
          <SettingList>
            {plugins.map((plugin) => <PluginSettingRow key={plugin.id} plugin={plugin} onOpen={onOpen} />)}
          </SettingList>
        )}
      </section>
    </Card>
  );
}

function PluginSettingRow({ plugin, onOpen }: { plugin: PluginShown; onOpen: (id: string) => void }) {
  const { t } = useTranslation();
  const name = plugin.name || plugin.id;
  const word = STATE_WORD[pluginState(plugin)];
  const servers = plugin.helpServers ?? [];
  return (
    <SettingRow
      label={(
        <span className="inline-flex items-center gap-2">
          <PluginIcon id={plugin.id} name={plugin.name} icon={plugin.icon} size="strip" dimmed={!plugin.enabled} />
          {name}
        </span>
      )}
      hint={servers.length === 0 ? t('settings.plugins.holdsNone') : undefined}
      control={(
        <>
          {word && <Pill tone={word.tone}>{t(word.key)}</Pill>}
          <Button aria-label={t('settings.plugins.openNamed', { name })} onClick={() => onOpen(plugin.id)}>
            {t('settings.plugins.open')}
          </Button>
        </>
      )}
    >
      {servers.length > 0 && (
        <ul className="m-0 flex list-none flex-col gap-1 p-0">
          {servers.map((server) => {
            const accepted = (server.writes ?? []).filter((tool) => server.accepted?.includes(tool));
            const state = approval(server);
            return (
              <li key={server.name} className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5 text-small text-ink-soft">
                <code className="font-mono text-meta text-ink">{server.name}</code>
                <span>{t(`settings.plugins.server.${state}`)}</span>
                {state === 'approved' && accepted.length > 0 && (
                  <span>{t('plugin.help.writesAccepted', { tools: accepted.join(', ') })}</span>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </SettingRow>
  );
}

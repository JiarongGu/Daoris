import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, Pill, SettingRow, Tip } from '../ui';
import { PluginSourceLine, updatable, type PluginSourceShown } from './PluginUpdate';

/**
 * One installed plugin as the driver's `PLUGINS` answers it (D64): the fields its row draws. The
 * bridge's own type is the same shape; a molecule names its props here rather than reach the bridge.
 */
export type PluginShown = {
  id: string;
  name: string;
  version: string;
  description: string;
  /** The person's word (`plugins.json`), independent of whether the plugin is sound. */
  enabled: boolean;
  /** Why it contributes nothing, in the driver's own sentence; null when sound. */
  problem: string | null;
  harnesses: string[];
  points: string[];
  /** Whether its hook process is up right now. */
  running: boolean;
  folder: string;
  /** Where what it keeps lives, beside the install folder, which a removal leaves. */
  data: string;
  /** Where it came from (PLUG9 c). An older shell sends none. */
  source?: PluginSourceShown;
};

/**
 * An installed plugin's row in Settings → Plugins (D64): its name, version and state, what it declares
 * and speaks on, its folder and where it came from, and its acts. Props in, presses out: the domain
 * holds the catalogue and the mutations, and what sits under the row (a trial's report, an update's
 * plan) comes in as children.
 */
export function PluginRow({
  plugin, canTry = false, trying = false, updating = false, acting = false,
  onTry, onAskUpdate, onSwitch, onRemove, children,
}: {
  plugin: PluginShown;
  /** Whether this shell has the kit (PLUG8): an older one cannot try a plugin, and offers no Try. */
  canTry?: boolean;
  trying?: boolean;
  updating?: boolean;
  acting?: boolean;
  onTry: (id: string) => void;
  onAskUpdate: (id: string) => void;
  onSwitch: (id: string, action: 'enable' | 'disable') => void;
  onRemove: (id: string) => void;
  children?: ReactNode;
}) {
  const { t } = useTranslation();
  const what = [
    plugin.harnesses.length > 0 ? t('plugin.declares', { harnesses: plugin.harnesses.join(', ') }) : null,
    plugin.points.length > 0 ? t('plugin.speaks', { points: plugin.points.join(', ') }) : null,
  ].filter(Boolean).join('; ') || t('plugin.quiet');

  return (
    <SettingRow
      label={(
        <span className="flex flex-wrap items-center gap-2">
          <span>{plugin.name}</span>
          {plugin.version && <span className="font-mono text-small text-ink-faint">{plugin.version}</span>}
          {/* PLUG10 (P9): running is a state, not an outcome, so it wears the palette's in-progress hue,
              as a working session's pill does. It wore done's green, an outcome's (D41 §3). */}
          {plugin.running && <Pill tone="taken">{t('plugin.running')}</Pill>}
          {!plugin.enabled && <Pill tone="neutral">{t('plugin.off')}</Pill>}
        </span>
      )}
      hint={(
        <span className="flex flex-col gap-0.5">
          {/* A refused plugin declares nothing BECAUSE it was refused — saying "declares nothing"
              above the sentence that says why would be the same fact twice, the second time
              wrong. Its sentence stands alone beneath. */}
          {!plugin.problem && <span>{what}{plugin.description ? ` — ${plugin.description}` : ''}</span>}
          <span className="truncate font-mono text-meta">{plugin.folder}</span>
          {/* Where it came from (PLUG9 c): an older shell sends nothing, and nothing is said. */}
          <PluginSourceLine source={plugin.source} />
        </span>
      )}
      control={(
        <>
          {/* A plugin that speaks can be tried; one that only declares runs nothing to try. */}
          {canTry && !plugin.problem && plugin.points.length > 0 && (
            <Button
              variant="ghost"
              disabled={trying}
              aria-label={t('plugin.kit.tryNamed', { id: plugin.id })}
              onClick={() => onTry(plugin.id)}
            >
              {t('plugin.kit.try')}
            </Button>
          )}
          {/* Only where there is a source to read: one with no record has nothing to update from. */}
          {updatable(plugin.source) && (
            <Button
              variant="ghost"
              disabled={updating}
              aria-label={t('plugin.update.askNamed', { id: plugin.id })}
              onClick={() => onAskUpdate(plugin.id)}
            >
              {t('plugin.update.ask')}
            </Button>
          )}
          <Button
            variant="ghost"
            disabled={acting}
            onClick={() => onSwitch(plugin.id, plugin.enabled ? 'disable' : 'enable')}
          >
            {t(plugin.enabled ? 'plugin.disable' : 'plugin.enable')}
          </Button>
          <Tip content={t('plugin.forgetTip')}>
            <Button variant="ghost" disabled={acting} onClick={() => onRemove(plugin.id)}>
              {t('plugin.forget')}
            </Button>
          </Tip>
        </>
      )}
    >
      {/* The driver's own sentence, verbatim — a version this build does not speak, a
          conflict naming both sides, a manifest that would not parse. Content, not chrome. */}
      {plugin.problem && (
        <p className="max-w-prose border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
          <Inline text={plugin.problem} />
        </p>
      )}
      {children}
    </SettingRow>
  );
}

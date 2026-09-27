import i18n from '../i18n';
import type { PanelTab } from './frame';

/**
 * A stream as the driver lists it (CONSOLE2c): something a session runs beside itself — a subagent,
 * or background work — and the key its console is tailed by.
 */
export type StreamRow = { key: string; kind: string; name: string; live: boolean; state: string | null };

/**
 * The output panel's tabs for a session (CONSOLE2c): its own console first, then each stream in the
 * order it opened, so nothing it started runs out of sight. None when it runs nothing beside itself,
 * because one tab is no tabs.
 *
 * @remarks
 * A tab is named by how it stands, in words, as every mark is (D41 §6): the kind, then the state.
 * A state this build has no key for is shown as the wire's own word, never as a blank.
 */
export function panelTabs(sessionId: string, rows: StreamRow[]): PanelTab[] | undefined {
  if (rows.length === 0) return undefined;
  const t = i18n.t.bind(i18n);

  return [
    { key: sessionId, kind: 'session', label: t('work.panel.tab.session'), tone: 'idle', status: t('work.panel.tab.own') },
    ...rows.map((row): PanelTab => {
      const kind = row.kind === 'subagent' ? 'subagent' : 'task';
      const state = row.live
        ? t('work.panel.stream.live')
        : row.state
          ? t(`work.panel.stream.state.${row.state}`, { defaultValue: row.state })
          : t('work.panel.stream.ended');
      return {
        key: row.key,
        kind,
        label: row.name,
        tone: row.live ? 'live' : row.state === 'failed' ? 'failed' : 'ended',
        status: `${t(`work.panel.stream.${kind}`)} · ${state}`,
      };
    }),
  ];
}

import { useMutation } from '@tanstack/react-query';
import { call } from './call';
import type { SwitchWord } from '../plugins/interfaces';

/** The person's word on one interface or point of an installed plugin at one place. */
export type PluginSwitchPress = { id: string; what: string; place: string; on: boolean };

/**
 * The screen's half of `daoris plugin switch`: one word written to `plugins.json`'s `switches`, answered with the plugin's words as
 * they now stand. The catalogue is asked again in every window by the route's `STATE_CHANGED`, as the plugin's own switch is.
 */
export const usePluginSwitch = () => useMutation({
  mutationFn: (press: PluginSwitchPress) =>
    call('PLUGIN_SWITCH', press) as Promise<{ id: string; what: string; place: string; on: boolean; switches: SwitchWord[] }>,
});

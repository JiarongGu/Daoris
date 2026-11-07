import type { Channel } from '../channel';

/** Who posted a channel message, as the conversation that took it names them: their repository and their agent, as the person calls it. */
export type ChannelPoster = { repository: string | null; agent: string | null; outside?: boolean };

/**
 * A channel message's home, seen from the conversation that took it: the message is drawn whole in the
 * channel, and the turn that took it carries a sender chip that names its poster and opens the channel.
 */
export type ChannelHome = {
  /** The poster of a message, by its id, where the page holds the channel; undefined where it holds no such message. */
  posterOf?: (message: string) => ChannelPoster | undefined;
  /** Open the channel where the message is kept; absent where the page draws no channel for this session. */
  onOpen?: (message: string) => void;
};

export const NO_CHANNEL_HOME: ChannelHome = {};

/**
 * The home of the attended line's channel messages, from the channel the page already reads for its panel: each message's poster by
 * its id, and `open`, the panel's own press. None where the page holds no channel.
 */
export function channelHomeOf(
  channel: Channel | null | undefined, nameOf: (agent: string) => string, open: () => void,
): ChannelHome | undefined {
  if (!channel) return undefined;
  return {
    posterOf: (message) => {
      const item = [...channel.items, ...(channel.addressed ?? [])].find((each) => each.kind === 'message' && each.id === message);
      if (!item || item.person) return undefined;
      return { repository: item.repository ?? null, agent: item.agent ? nameOf(item.agent) : null, ...(item.outside ? { outside: true } : {}) };
    },
    onOpen: () => open(),
  };
}

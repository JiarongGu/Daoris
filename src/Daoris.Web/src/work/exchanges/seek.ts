/** Brings a channel message's row in the timeline into view and to focus: the door on the turn that took it. */
export function toChannelRow(message: string): void {
  const row = [...document.querySelectorAll<HTMLElement>('[data-channel-row]')].find((each) => each.dataset.channelRow === message);
  row?.scrollIntoView?.({ block: 'center' });
  row?.querySelector<HTMLElement>('li')?.focus();
}

/** Brings an exchange's newest row into view and to focus: the head's *Waiting on* door. */
export function toExchangeRow(exchange: string): void {
  const rows = [...document.querySelectorAll<HTMLElement>('[data-exchange-row]')].filter((each) => each.dataset.exchangeRow === exchange);
  const row = rows[rows.length - 1];
  row?.scrollIntoView?.({ block: 'center' });
}

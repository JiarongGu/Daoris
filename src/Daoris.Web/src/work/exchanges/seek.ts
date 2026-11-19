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

/** Brings the dock's line for `key` into view and its first press to focus: a waiting request's row leads to its press there. */
export function toDockLine(key: string): void {
  const line = [...document.querySelectorAll<HTMLElement>('[data-dock-line]')].find((each) => each.dataset.dockLine === key);
  line?.scrollIntoView?.({ block: 'nearest' });
  line?.querySelector<HTMLElement>('button')?.focus();
}

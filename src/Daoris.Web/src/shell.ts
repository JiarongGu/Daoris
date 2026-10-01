// The shell's half of the platform (D46 §6). In a browser none of this exists — the bridge is absent,
// the query never runs, and every control gated on it stays unrendered. That is the design, not a
// degradation: the controls act where a driver is attached, and only the desktop has one.
//
// The bridge by domain (MOD3): each domain's types and calls live in `bridge/<domain>.ts`, and this
// file is only the barrel, so no import moved when they did. It re-exports every domain and holds no
// code of its own, which `bridge/bridge.test.ts` holds. `bridge/call.ts` is the domains' own helper
// and is re-exported by nothing.
export * from './bridge/across';
export * from './bridge/agents';
export * from './bridge/browser';
export * from './bridge/console';
export * from './bridge/conversation';
export * from './bridge/driver';
export * from './bridge/help';
export * from './bridge/lines';
export * from './bridge/log';
export * from './bridge/plugins';
export * from './bridge/registry';
export * from './bridge/remotes';
export * from './bridge/rules';
export * from './bridge/sessions';
export * from './bridge/terminal';
export * from './bridge/tools';
export * from './bridge/trees';
export * from './bridge/windows';

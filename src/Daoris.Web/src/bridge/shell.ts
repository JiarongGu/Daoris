// The shell's half of the platform (D46 §6). In a browser none of this exists — the bridge is absent,
// the query never runs, and every control gated on it stays unrendered. That is the design, not a
// degradation: the controls act where a driver is attached, and only the desktop has one.
//
// The bridge by domain (MOD3): each domain's types and calls live in `bridge/<domain>.ts`, and this
// file is only the barrel, so no import moved when they did. It re-exports every domain and holds no
// code of its own, which `bridge/bridge.test.ts` holds. `bridge/call.ts` is the domains' own helper
// and is re-exported by nothing.
export * from './accounts';
export * from './across';
export * from './agents';
export * from './asks';
export * from './browser';
export * from './console';
export * from './conversation';
export * from './driver';
export * from './entries';
export * from './help';
export * from './history';
export * from './lines';
export * from './log';
export * from './plugins';
export * from './registry';
export * from './remotes';
export * from './rules';
export * from './sessions';
export * from './setup';
export * from './published';
export * from './stateHub';
export * from './system';
export * from './terminal';
export * from './tools';
export * from './git';
export * from './trace';
export * from './trees';
export * from './update';
export * from './windows';
export * from './work';
export * from './workflow';

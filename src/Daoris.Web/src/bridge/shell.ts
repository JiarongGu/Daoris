// The shell's half of the platform. In a browser none of this exists — the bridge is absent,
// the query never runs, and every control gated on it stays unrendered. That is the design, not a
// degradation: the controls act where a driver is attached, and only the desktop has one.
//
// The bridge by domain: each domain's types and calls live in `bridge/<domain>.ts`, or a scope's
// `bridge/<scope>/<domain>.ts`, and this file is only the barrel. It re-exports every domain and holds no
// code of its own, which `bridge/bridge.test.ts` holds. `bridge/call.ts` is the domains' own helper
// and is re-exported by nothing.
export * from './agents/accounts';
export * from './repositories/across';
export * from './repositories/files';
export * from './agents/agents';
export * from './sessions/asks';
export * from './sessions/atWork';
export * from './attention';
export * from './browser';
export * from './sessions/channelOutside';
export * from './sessions/console';
export * from './sessions/conversation';
export * from './driver';
export * from './sessions/entries';
export * from './help';
export * from './helpBeyond';
export * from './hold';
export * from './sessions/history';
export * from './repositories/lines';
export * from './log';
export * from './notices';
export * from './person';
export * from './plugins';
export * from './repositories/registry';
export * from './sessions/quests';
export * from './repositories/remotes';
export * from './agents/rules';
export * from './sessions/sessions';
export * from './stewardLand';
export * from './sessions/supervision';
export * from './sessions/together';
export * from './repositories/setup';
export * from './state/held';
export * from './state/published';
export * from './state/slices';
export * from './state/stateHub';
export * from './system';
export * from './terminal';
export * from './agents/tools';
export * from './repositories/git';
export * from './repositories/gitPush';
export * from './sessions/trace';
export * from './sessions/trees';
export * from './update';
export * from './windows';
export * from './sessions/work';
export * from './repositories/workflow';
export * from './repositories/workflowEdit';
export * from './repositories/integrate';

// What each work may call: the service's call table, which is code with no machine
// state, shown read-only. `callTable.test.ts` holds this file cell for cell to the service's `fixtures/call-table.json`, which the
// service's `McpRoleFilterTests` holds to `CallTable` over the host's registration; a tool added there fails one until placed here.

/** Every tool the connector registers. */
export const REGISTERED_TOOLS: readonly string[] = [
  'agent_propose', 'ask_propose', 'brainstorm_give', 'brainstorm_synthesize', 'browser_propose',
  'channel_post', 'channel_read', 'checkout_read', 'clear_propose', 'go_ahead_ask', 'go_propose',
  'handback_read', 'handover_propose', 'helper_answer', 'helper_ask', 'housekeeping_clean_up', 'housekeeping_discard_branch',
  'housekeeping_inspect', 'knowledge_convergence', 'knowledge_get', 'knowledge_refresh', 'knowledge_repositories', 'knowledge_search',
  'line_read', 'memory_end', 'memory_get', 'memory_propose', 'memory_search', 'memory_write', 'opinion_answer', 'opinion_give',
  'partner_answer', 'permission_propose', 'plugin_propose', 'quest_list', 'quest_publish', 'quest_respond', 'question_ask',
  'registry', 'review_ready', 'review_serve', 'role_propose', 'settings_cap',
  'settings_cooloff', 'settings_language', 'settings_notify', 'settings_quiet', 'setup_propose', 'sync_propose', 'workflow_propose',
];

type Column = { only: readonly string[] } | { allBut: readonly string[] };

const TURNS = ['brainstorm_give', 'brainstorm_synthesize'];
const READS = [
  'handback_read', 'knowledge_convergence', 'knowledge_get', 'knowledge_repositories', 'knowledge_search', 'line_read', 'memory_get',
  'memory_search', 'quest_list', 'registry',
];
const MEMORY_CHANGES = ['memory_end', 'memory_write'];
// What every session working in a repository is served, less what its work is refused.
const CORE = [
  ...READS, 'channel_post', 'channel_read', 'helper_ask', 'memory_propose', ...MEMORY_CHANGES, 'quest_publish', 'quest_respond',
];
const CONVERSING = ['go_ahead_ask', 'handover_propose', 'opinion_answer', 'permission_propose'];
const WORKING = [...CONVERSING, 'question_ask', 'review_ready', 'review_serve'];
const without = (tools: readonly string[], left: readonly string[]) => tools.filter((tool) => !left.includes(tool));

const COLUMNS: Record<string, Column> = {
  build: { only: [...CORE, ...WORKING] },
  design: { only: [...CORE, ...WORKING] },
  exact: { only: [...CORE, ...WORKING] },
  chat: { only: [...CORE, ...CONVERSING] },
  // Neither works in a repository of its own, so neither has a helper to ask.
  intake: { only: without(CORE, [...MEMORY_CHANGES, 'helper_ask']) },
  'ask-daoris': { allBut: [...TURNS, 'helper_ask', ...MEMORY_CHANGES] },
  help: {
    only: [
      ...without(CORE, ['channel_post', 'channel_read', 'memory_propose', 'quest_publish', 'quest_respond', ...MEMORY_CHANGES]),
      'helper_answer',
    ],
  },
  partner: {
    only: [
      'knowledge_convergence', 'knowledge_get', 'knowledge_repositories', 'knowledge_search', 'line_read', 'memory_get', 'memory_search',
      'partner_answer', 'registry',
    ],
  },
  review: {
    only: [
      'handback_read', 'knowledge_convergence', 'knowledge_get', 'knowledge_repositories', 'knowledge_search', 'memory_get', 'memory_search',
      'opinion_give', 'quest_list', 'registry',
    ],
  },
  supervise: { only: READS },
  'brainstorm:generator': { only: ['brainstorm_give'] },
  'brainstorm:synthesizer': { only: ['brainstorm_synthesize'] },
  unplaced: { only: READS },
  unread: { only: READS },
};

/** The eleven works, each with the table's columns that place it (a brainstorm is placed by its turn). */
export const WORK_COLUMNS: Readonly<Record<string, readonly string[]>> = {
  build: ['build'], design: ['design'], exact: ['exact'], chat: ['chat'], intake: ['intake'], 'ask-daoris': ['ask-daoris'], help: ['help'],
  partner: ['partner'], review: ['review'], supervise: ['supervise'], brainstorm: ['brainstorm:generator', 'brainstorm:synthesizer'],
};

const served = (column: Column, tool: string) => 'only' in column ? column.only.includes(tool) : !column.allBut.includes(tool);

/**
 * What a work may call: the tools it is served, and, where it is refused fewer than it is served, the tools it is refused (the
 * page says it as every tool but those). A work the table does not place is served nothing here.
 */
export function callsOf(work: string): { served: readonly string[]; refused: readonly string[]; allBut: boolean } {
  const columns = (WORK_COLUMNS[work] ?? []).map((name) => COLUMNS[name]!);
  const mine = REGISTERED_TOOLS.filter((tool) => columns.some((column) => served(column, tool)));
  const refused = REGISTERED_TOOLS.filter((tool) => !mine.includes(tool));
  return { served: mine, refused, allBut: refused.length > 0 && refused.length < mine.length };
}

// What each work may call (COMPANYVIEW1i, the company-view design §2 row 9): the service's call table, which is code with no machine
// state, shown read-only. `callTable.test.ts` holds this file cell for cell to the service's `fixtures/call-table.json`, which the
// service's `McpRoleFilterTests` holds to `CallTable` over the host's registration; a tool added there fails one until placed here.

/** Every tool the connector registers. */
export const REGISTERED_TOOLS: readonly string[] = [
  'accept_propose', 'agent_propose', 'agent_settings_propose', 'ask_propose', 'brainstorm_give', 'brainstorm_synthesize', 'browser_propose',
  'channel_post', 'channel_read', 'checkout_read', 'clear_propose', 'delete_propose', 'go_ahead_ask', 'go_propose', 'hand_propose',
  'handback_read', 'handover_propose', 'helper_answer', 'helper_ask', 'housekeeping_clean_up', 'housekeeping_discard_branch',
  'housekeeping_inspect', 'knowledge_convergence', 'knowledge_get', 'knowledge_refresh', 'knowledge_repositories', 'knowledge_search',
  'line_read', 'memory_end', 'memory_get', 'memory_propose', 'memory_search', 'memory_write', 'opinion_answer', 'opinion_give',
  'partner_answer', 'pause_propose', 'permission_propose', 'plugin_propose', 'quest_list', 'quest_publish', 'quest_respond', 'question_ask',
  'registry', 'repository_propose', 'review_ready', 'review_serve', 'role_propose', 'session_propose', 'setting_propose', 'settings_cap',
  'settings_cooloff', 'settings_language', 'settings_notify', 'settings_timeout', 'setup_propose', 'sync_propose', 'workflow_propose',
];

type Column = { only: readonly string[] } | { allBut: readonly string[] };

const ASK_DAORIS_ONLY = ['checkout_read', 'housekeeping_clean_up', 'housekeeping_discard_branch', 'housekeeping_inspect'];
const SETTINGS = ['settings_cap', 'settings_cooloff', 'settings_language', 'settings_notify', 'settings_timeout'];
const TURNS = ['brainstorm_give', 'brainstorm_synthesize'];
const OWN = [...TURNS, ...ASK_DAORIS_ONLY, ...SETTINGS];
const READS = [
  'handback_read', 'knowledge_convergence', 'knowledge_get', 'knowledge_repositories', 'knowledge_search', 'line_read', 'memory_get',
  'memory_search', 'quest_list', 'registry',
];

const COLUMNS: Record<string, Column> = {
  build: { allBut: OWN },
  design: { allBut: OWN },
  chat: { allBut: OWN },
  intake: { allBut: [...OWN, 'memory_end', 'memory_write'] },
  'ask-daoris': { allBut: [...TURNS, 'memory_end', 'memory_write'] },
  help: {
    allBut: [...OWN, 'channel_post', 'channel_read', 'memory_end', 'memory_propose', 'memory_write', 'quest_publish', 'quest_respond'],
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
  'brainstorm:generator': { only: ['brainstorm_give'] },
  'brainstorm:synthesizer': { only: ['brainstorm_synthesize'] },
  unplaced: { only: READS },
  unread: { only: READS },
};

/** The nine works, each with the table's columns that place it (a brainstorm is placed by its turn). */
export const WORK_COLUMNS: Readonly<Record<string, readonly string[]>> = {
  build: ['build'], design: ['design'], chat: ['chat'], intake: ['intake'], 'ask-daoris': ['ask-daoris'], help: ['help'],
  partner: ['partner'], review: ['review'], brainstorm: ['brainstorm:generator', 'brainstorm:synthesizer'],
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

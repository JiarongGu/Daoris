export function parse(argv) {
  const options = {};
  const values = new Set(['home', 'logs', 'since', 'until', 'service', 'quests', 'workspaces', 'work', 'merge-queue', 'merge-record', 'repo', 'case', 'transcripts', 'ids']);
  for (let i = 0; i < argv.length; i++) {
    const flag = argv[i];
    if (flag === '--json') { options.json = true; continue; }
    const name = flag.replace(/^--/, '');
    if (!flag.startsWith('--') || !values.has(name)) throw new Error(`unknown argument ${flag}`);
    const value = argv[++i];
    if (!value || value.startsWith('--')) throw new Error(`${flag} needs a value`);
    const key = name.replace(/-(\w)/g, (_, c) => c.toUpperCase());
    options[key] = key === 'case' && options.case ? `${options.case},${value}` : value;
  }
  if (options.service && options.quests) throw new Error('name either --service or --quests');
  for (const flag of ['since', 'until']) if (options[flag] && (!/^\d{4}-\d{2}-\d{2}$/.test(options[flag]) || new Date(`${options[flag]}T00:00:00Z`).toISOString().slice(0, 10) !== options[flag])) throw new Error(`--${flag} needs a calendar date YYYY-MM-DD`);
  if (options.since && options.until && options.since > options.until) throw new Error('--until is before --since');
  return options;
}

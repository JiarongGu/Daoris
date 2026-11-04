import { spawnSync } from 'node:child_process';

export const fail = (message, code = 2) => {
  console.error(message);
  process.exit(code);
};

export const run = (command, args, options = {}) => {
  const result = spawnSync(command, args, { stdio: 'inherit', shell: false, windowsHide: true, ...options });
  if (result.error) fail(`${command} did not start: ${result.error.message}`);
  if (result.status !== 0) fail(`${command} ${args[0] ?? ''} exited ${result.status}`, result.status ?? 2);
};

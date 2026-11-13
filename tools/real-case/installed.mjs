import { spawnSync } from 'node:child_process';

/**
 * The installed command as a function: `run(args)` answers `{ code, stdout, stderr }`. The home and the service reach it as the
 * environment the command reads; the command's path is an argument of the caller, with no default beyond the name `daoris`.
 * Tests pass a stand-in with the same shape.
 */
export function installedCommand({ command = 'daoris', home, service, spawn = spawnSync }) {
  const env = { ...process.env, DAORIS_HOME: home, ...(service ? { DAORIS_SERVICE_URL: service } : {}) };
  return (args) => {
    const r = spawn(command, args, { env, encoding: 'utf8', windowsHide: true, timeout: 120_000 });
    return { code: r.status ?? 2, stdout: r.stdout ?? '', stderr: r.error ? String(r.error.message) : r.stderr ?? '' };
  };
}

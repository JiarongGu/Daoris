namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The watcher's pipe-door stand-in (SESSWATCH1a, the session-watch design §7): one agent that can be told what to do, so a test of the
/// watch writes no script of its own. It takes its quest, does what its cue says, then finishes the quest (or not, for a crash) and
/// exits, or hangs where the cue is a hang. Told by its arguments, else by a <c>cue: …</c> line in its target, so one stand-in serves
/// quests with different cues. Never a real agent binary: <c>node</c> runs it, as every stand-in here does.
/// </summary>
/// <remarks>
/// <para>The cues, each with its numbers in milliseconds:</para>
/// <list type="bullet">
/// <item><c>stream &lt;every&gt; &lt;count&gt;</c>: a line on stdout every <c>every</c>, <c>count</c> times, then done.</item>
/// <item><c>stderr &lt;every&gt; &lt;count&gt;</c>: the same on stderr alone, its stdout silent.</item>
/// <item><c>silent</c>: one line, then nothing at all, its timer the only thing it runs: a hang.</item>
/// <item><c>hang-call</c>: one line, then a child that waits on a stdin nobody writes to, waited for: a call that never returns.</item>
/// <item><c>compute &lt;for&gt;</c>: one line, then a child computing for <c>for</c> while nothing is said, then done.</item>
/// <item><c>grandchild</c>: done, then exit leaving a detached grandchild holding its output (state E).</item>
/// <item><c>crash</c>: exit 3 without finishing its quest.</item>
/// </list>
/// The DONEEND1 stand-in (<c>DoneEndTickTests</c>) and the family rehearsal's pipe stub's cues are its exemplars.
/// </remarks>
internal static class WatchStandIn
{
    /// <summary>Write the stand-in into <paramref name="folder"/>: what a config's <c>commands</c> runs with <c>node</c>.</summary>
    public static string Write(string folder)
    {
        var script = Path.Combine(folder, "watch-stand-in.mjs");
        File.WriteAllText(script, """
            import { spawn } from 'node:child_process';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const told = process.argv.slice(2);
            const fromTarget = /^cue:\s*(.+)$/m.exec(process.env.DAORIS_TARGET ?? '')?.[1]?.trim().split(/\s+/) ?? [];
            const [cue, ...numbers] = told.length > 0 ? told : fromTarget;
            const [first = 1000, second = 5] = numbers.map(Number);
            const quest = /quest `#([0-9a-z]+)`/i.exec(process.env.DAORIS_TARGET ?? '')?.[1] ?? 'q1';
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/${quest}/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            const wait = (ms) => new Promise((done) => setTimeout(done, ms));
            const done = async () => { await respond({ action: 'done', reason: 'landed' }); process.exit(0); };
            // Heard first thing, so its own start-up is no quiet of its agent's.
            console.log(`started: ${cue ?? '(no cue)'}`);
            await respond({ action: 'take' });
            switch (cue) {
              case 'stream':
              case 'stderr':
                for (let at = 0; at < second; at++) {
                  (cue === 'stream' ? process.stdout : process.stderr).write(`line ${at}\n`);
                  await wait(first);
                }
                await done();
                break;
              case 'silent':
                console.log('taken; now nothing more');
                setInterval(() => {}, 1000);
                break;
              case 'hang-call': {
                console.log('running a command that waits on its input');
                const child = spawn(process.execPath, ['-e', 'process.stdin.resume()'], { stdio: ['pipe', 'ignore', 'ignore'] });
                await new Promise((ended) => child.on('exit', ended));
                await done();
                break;
              }
              case 'compute': {
                console.log('running a long computation');
                const child = spawn(process.execPath, ['-e', `const end = Date.now() + ${first}; while (Date.now() < end) {}`], { stdio: 'ignore' });
                await new Promise((ended) => child.on('exit', ended));
                await done();
                break;
              }
              case 'grandchild': {
                await respond({ action: 'done', reason: 'landed' });
                spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { stdio: 'inherit', detached: true, windowsHide: true }).unref();
                console.log('finished; a dev server keeps running');
                process.exit(0);
                break;
              }
              case 'crash':
                process.exit(3);
                break;
              default:
                console.log(`no cue named ${cue ?? '(none)'}`);
                await done();
            }
            """);
        return script;
    }

    /// <summary>
    /// Write the protocol door's stand-in into <paramref name="folder"/>, run with <c>node</c> under an <c>acp-stub</c> adapter. Its one cue,
    /// <c>background-compute &lt;for&gt;</c>: it takes its quest, starts a child beside itself that writes a line and computes for
    /// <c>for</c>, announces it as the adapter does, says it will pick up when it finishes and ends its turn; once the child ends it
    /// finishes its quest. It answers every request after, and never exits on its own (state C, then DONEEND1's end).
    /// </summary>
    public static string WriteAcp(string folder)
    {
        var script = Path.Combine(folder, "watch-acp-stand-in.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { spawn } from 'node:child_process';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const [cue = 'background-compute', computeFor = '15000'] = process.argv.slice(2);
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const update = (body) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-1', update: body } });
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            setInterval(() => {}, 1000);
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                await respond({ action: 'take' });
                const work = spawn(process.execPath, ['-e',
                  `console.log('computing'); const end = Date.now() + ${Number(computeFor)}; while (Date.now() < end) {}`], { stdio: 'ignore' });
                work.on('exit', () => { respond({ action: 'done', reason: 'landed' }); });
                update({ sessionUpdate: 'async_task_spawned', asyncTaskId: 't1', name: 'Run the tests', taskType: 'shell', canStop: true });
                update({ sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: `${cue}: the tests run in the background; I'll pick up when they finish.` } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            """);
        return script;
    }
}

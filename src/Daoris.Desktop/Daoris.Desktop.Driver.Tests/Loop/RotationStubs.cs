namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The stand-ins the account ticks run on (<see cref="AccountRotationTickTests"/>, <see cref="HeldWordsTickTests"/>): each script
/// written into the test's home, which its path is under.
/// </summary>
internal static class RotationStubs
{
    /// <summary>
    /// The protocol door's stand-in that resumes: it names its conversation <c>conv-&lt;session&gt;</c> and writes down
    /// each frame with the stub account it runs as, what it is prompted and what its record's note said. A first prompt takes
    /// the quest and, by its mode, closes it (<c>closes</c>) or fails its turn with the quest still taken (<c>fails</c>); a
    /// carry-on closes it; a resumed prompt answers.
    /// </summary>
    /// <param name="zone">The zone the limit's sentence names its reset in.</param>
    internal static string ResumingAgent(string home, TimeZoneInfo zone)
    {
        var script = Path.Combine(home, "resuming-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            const [log, mode] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const note = async () => {
              const sessions = await (await fetch(`${url}/api/sessions`)).json();
              return sessions.find((s) => s.id === session)?.note ?? null;
            };
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session, account }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            const LIMIT = "Internal error: You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your weekly limit resets Oct 3, 4pm (ZONE)";
            let conversation = null;
            let resumed = false;
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + session;
                heard({ method: 'session/new', conversation });
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                heard({ method: frame.method, conversation: frame.params?.sessionId });
                conversation = frame.params.sessionId;
                resumed = true;
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                const prompt = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text).join('\n');
                heard({ method: 'session/prompt', prompt, resumed, note: await note() });
                if (resumed && mode === 'limited-twice') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: LIMIT } });
                } else if (resumed) {
                  say('Logged the port, as you said.');
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                } else if (prompt.includes('carrying on quest')) {
                  await respond({ action: 'done', reason: 'carried on' });
                  say('Done.');
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                } else if (mode.startsWith('limited')) {
                  // LIMITRESUME1b: the first turn is refused by observation 4's sentence, which cools the account it ran on.
                  await respond({ action: 'take' });
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: LIMIT } });
                } else if (mode === 'fails') {
                  await respond({ action: 'take' });
                  say('I started the field.');
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error: the model fell over.' } });
                } else {
                  await respond({ action: 'take' });
                  await respond({ action: 'done', reason: 'served the report' });
                  say('Served.');
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                }
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            // stdin closed: the ending. Never process.exit (STUB1): the process ends when the loop drains.
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """.Replace("(ZONE)", $"({zone.Id})", StringComparison.Ordinal));
        return script;
    }

    /// <summary>
    /// The stand-in harness. Asked its version or an account's sign-in, it answers, signed out for the accounts named in
    /// its second argument. Run, it writes down where it ran, as which account, what it is handed and what its record's
    /// note said; then a first start takes the quest, says its last words and is cut off, and a carry-on closes it.
    /// </summary>
    internal static string Agent(string home)
    {
        var script = Path.Combine(home, "agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            const [log, signedOut] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) {
              console.log((signedOut ?? '').split(',').includes(account) ? 'logged-out' : `logged-in as ${account}`);
              process.exit(0);
            }
            const url = process.env.DAORIS_SERVICE_URL;
            const target = process.env.DAORIS_TARGET ?? '';
            const sessions = await (await fetch(`${url}/api/sessions`)).json();
            const own = sessions.find((s) => s.id === process.env.DAORIS_SESSION_ID);
            appendFileSync(log, JSON.stringify({ cwd: process.cwd(), account, target, note: own?.note ?? null }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            if (!target.includes('carrying on quest')) {
              await respond({ action: 'take' });
              console.log('I started the field.');
              process.exit(1);
            }
            await respond({ action: 'done', reason: 'finished on the next account of the order' });
            """);
        return script;
    }
}

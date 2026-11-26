import assert from 'node:assert/strict';
import { test } from 'node:test';
import { holdersOf, listenersOn, loopbackPortsOf } from './port-listeners.mjs';

const NETSTAT = [
  '  Proto  Local Address          Foreign Address        State           PID',
  '  TCP    0.0.0.0:4200           0.0.0.0:0              LISTENING       4242',
  '  TCP    [::]:4200              [::]:0                 LISTENING       4242',
  '  TCP    127.0.0.1:4200         127.0.0.1:50000        ESTABLISHED     77',
  '  TCP    0.0.0.0:42001          0.0.0.0:0              LISTENING       99',
  '  UDP    0.0.0.0:4200           *:*                                    55',
  '  TCP    [::1]:4200             [::]:0                 LISTENING       4300',
  '  TCP    127.0.0.1:9333         0.0.0.0:0              LISTENING       4242',
  '  TCP    127.0.0.1:9334         0.0.0.0:0              LISTENING       4300',
].join('\r\n');

test('the listeners of a port are read from netstat: either family, listening only, each pid once', () => {
  assert.deepEqual(listenersOn(4200, NETSTAT), [4242, 4300]);
  assert.deepEqual(listenersOn(9, NETSTAT), []);
  assert.deepEqual(listenersOn(4200, undefined), []);
});

test('the loopback ports a process listens on are read from netstat, no other process\'s and no other address\'s', () => {
  assert.deepEqual(loopbackPortsOf(4242, NETSTAT), [9333]);
  assert.deepEqual(loopbackPortsOf(4300, NETSTAT), [9334]);
  assert.deepEqual(loopbackPortsOf(1, NETSTAT), []);
});

test('a port\'s holders are named by their program and command line, by pid alone where the reader cannot say, and by nothing where none listens', () => {
  const run = (command) => ({ status: 0, stdout: command === 'netstat' ? NETSTAT : '' });
  const asked = [];
  const read = ({ pids }) => { asked.push(pids); return [{ pid: 4242, name: 'node.exe', command: 'node a.mjs' }]; };
  assert.equal(holdersOf(4200, { run, read }), 'pid 4242 node.exe node a.mjs; pid 4300');
  assert.deepEqual(asked, [[4242, 4300]]);
  assert.equal(holdersOf(4200, { run, read: () => { throw new Error('the machine reader failed'); } }), 'pid 4242; pid 4300');
  assert.equal(holdersOf(7, { run, read }), '');
  assert.deepEqual(asked, [[4242, 4300]], 'nothing is asked of the reader where none listens');
});

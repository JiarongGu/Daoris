// The commands this plugin declares under `interfaces.commands`, each one round of `command/run`: Daoris starts a process for the round,
// sends the answers given so far, and says these lines, asks what is asked, and ends where nothing is.
import { forget, keep, kept, refused, stored } from './credential.mjs';
import { NAMES, pullRequestCommand } from './pull-request-commands.mjs';

const ID = 'daoris-azure-devops';
const MAKE = 'Make a personal access token in Azure DevOps (User settings, Personal access tokens) with the scopes Code (Read & write), '
  + 'Build (Read), Release (Read, write & execute) and Work Items (Read & write).';
const ASK = { name: 'token', prompt: 'Personal access token', masked: true };

export { refused };

export async function run({ command, args = {}, answers = {} }) {
  if (NAMES.includes(command)) return pullRequestCommand(command, args);
  if (!stored()) return { said: ['Daoris handed this plugin no private storage: switch its storage on in Plugins.'], exit: 1 };
  if (command === 'login') {
    const token = typeof answers.token === 'string' ? answers.token.trim() : '';
    if (!token) return { said: [MAKE], ask: ASK };
    if (/\s/.test(token)) return { said: ['A personal access token is one word, with no spaces.'], exit: 1 };
    keep(token);
    return { said: ["The personal access token is kept in this plugin's own storage; each call to Azure DevOps carries it."], exit: 0 };
  }
  if (command === 'logout') return { said: [forget() ? 'The kept token is forgotten.' : 'No token was kept.'], exit: 0 };
  if (command === 'status') {
    const held = kept();
    if (!held) return { said: [`No token is kept: run \`daoris ${ID} login\`.`], exit: 1 };
    if (held.refused) return { said: [`Azure DevOps refused the kept token at ${held.refused}: run \`daoris ${ID} login\` with a new one.`], exit: 1 };
    return { said: [`A personal access token is kept, since ${held.kept}.`], exit: 0 };
  }
  return { said: [`This plugin has no command \`${command}\`.`], exit: 2 };
}

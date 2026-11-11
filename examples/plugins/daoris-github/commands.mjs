// The commands this plugin declares under `interfaces.commands`, each one round of `command/run`: Daoris starts a process for the round,
// sends the answers given so far, and says these lines, asks what is asked, and ends where nothing is.
import { forget, keep, kept, stored } from './credential.mjs';
import { NAMES, pullRequestCommand } from './pull-request-commands.mjs';

const ID = 'daoris-github';
const MAKE = 'Make a fine-grained personal access token on GitHub with Pull requests (Read and write) and Contents (Read) on the repositories it opens pull requests in.';

export async function run({ command, args = {}, answers = {} }) {
  if (!stored()) return { said: ['Daoris handed this plugin no private storage: switch its storage on in Plugins.'], exit: 1 };
  if (NAMES.includes(command)) return pullRequestCommand(command, args);
  if (command === 'login') {
    const token = typeof answers.token === 'string' ? answers.token.trim() : '';
    if (!token) return { said: [MAKE], open: 'https://github.com/settings/personal-access-tokens/new', ask: { name: 'token', prompt: 'Personal access token', masked: true } };
    if (/\s/.test(token)) return { said: ['A personal access token is one word, with no spaces.'], exit: 1 };
    keep(token);
    return { said: ["The token is kept in this plugin's own storage; each call to GitHub carries it."], exit: 0 };
  }
  if (command === 'logout') return { said: [forget() ? 'The kept token is forgotten.' : 'No token was kept.'], exit: 0 };
  if (command === 'status') {
    const held = kept();
    if (!held) return { said: [`No token is kept: run \`daoris ${ID} login\`.`], exit: 1 };
    if (held.refused) return { said: [`GitHub refused the kept token at ${held.refused}: run \`daoris ${ID} login\` with a new one.`], exit: 1 };
    return { said: [`A token is kept, since ${held.kept}.`], exit: 0 };
  }
  return { said: [`This plugin has no command \`${command}\`.`], exit: 2 };
}

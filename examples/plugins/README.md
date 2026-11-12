# Example plugins

Tracked plugins a person can read, copy and install. Each folder is a plugin with its own README. The pull-request and `local-*` plugins carry `node --test` tests; `hold-by-title`
is proved by the family rehearsal, and the two browsers are a manifest alone.

| Folder | What it shows |
|---|---|
| [hold-by-title](hold-by-title/README.md) | a landing decision; the family rehearsal's fixture |
| [daoris-github](daoris-github/README.md), [daoris-azure-devops](daoris-azure-devops/README.md) | Daoris's own plugins for GitHub and Azure DevOps: a pull request opened and read through the service's REST API, and on Azure DevOps its auto-complete and release followed |
| [daoris-dev-report](daoris-dev-report/README.md) | commands and storage alone: a session files a report, the person's development agent reads the inbox from a terminal |
| [browser](browser/README.md), [daoris-in-app-browser](daoris-in-app-browser/README.md) | a browser connection for sessions |

A plugin's id is `<family>-<name>`, lowercase with dashes. Daoris's own plugins are the `daoris-` family; yours takes your own
family, never `daoris-`. A plugin renamed says what it was in its manifest (`formerly`), and Daoris moves the old id's records to it once.
| [local-preview](local-preview/README.md) | one performed step with a temporary resource |
| [local-release](local-release/README.md) | publish and release as performed steps: find, release, publish |

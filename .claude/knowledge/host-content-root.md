---
name: host-content-root
applies_when: building a .NET generic or web host (Host.CreateApplicationBuilder, WebApplication.CreateBuilder, Host.CreateDefaultBuilder) that may start inside a repository checkout or an install's folder
enforces: a host sets its own content root, the folder of its binaries or the bundle's, and turns hostBuilder:reloadConfigOnChange off, because the defaults watch the working folder and everything below it and walk every folder renamed there
---

# A .NET host started inside a checkout or an install sets its own root and watches nothing

**The default content root is the working folder, and the default configuration watches `appsettings.json` there. Give a host
a root of its own and turn the reload off, or it holds every folder other work has to move.**

## Why

`Host.CreateApplicationBuilder`, `WebApplication.CreateBuilder` and `Host.CreateDefaultBuilder` add `appsettings.json` with
`reloadOnChange`. The file provider then watches the content root with `IncludeSubdirectories` set, so the host hears every
write under it, and its `PhysicalFilesWatcher.OnRenamed` lists a renamed folder with `SearchOption.AllDirectories`. A session
starts its knowledge connector in its checkout; the desktop starts the service from the install. Each host then walks each
folder a build, an install or an update's swap renames beneath it, and the walk holds that folder against the swap's next
move: the update's swap of `app/` was refused `busy` (the fix log's KNOWWALK1 and HTTPWATCH1). A repository's own
`appsettings.json` is also read as the host's own, which it is not.

## How to apply

- **Root:** `AppContext.BaseDirectory` where the host serves no files; the folder holding its `wwwroot` where it does
  (`HostRoot.ContentRoot`). Never the working folder where a checkout or an install can be it.
- **Reload:** `hostBuilder:reloadConfigOnChange` = `false`. `HostApplicationBuilderSettings.Configuration` takes it as an
  in-memory source (`KnowledgeServer.HostBuilder`); `WebApplicationOptions` has no configuration, so it travels as the first
  command-line argument (`HostRoot.Builder`), which the defaults read before the settings files. Environment variables and the
  command line stay read.
- **Prove it:** a test that every `FileConfigurationProvider` has `Source.ReloadOnChange` false and that the content root is
  not the working folder (`ConnectorHostTests`, `HostRootTests`). A new host is one more place to ask.
- A host that serves nothing from disk and reads no settings file still takes the root: the default is the watch.

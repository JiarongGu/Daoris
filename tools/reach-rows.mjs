/**
 * Reach rows of the lane table (`tools/merge-branch/reach-table.mjs`) that read the driver's readers, in a unit of their own.
 *
 * A provider's own sources: the tools' provider census runs the contracts' generator,
 * which reads each provider's sources (the words its vocabulary names) and the registry's reader of them, so a change there
 * reaches the driver's own readers and the tool tests.
 */
const sources = (driverReaders) => ({
  paths: [
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Providers/*/*.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Registry/Providers.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Registry/ProviderVocabulary.cs',
  ],
  gates: [...driverReaders, 'tool-tests'],
  why: "a provider's own sources and the registry's list and reader: the driver's own, and the tools' provider-census.test.ts runs the contracts' generator, which reads them (D251)",
});

/** A provider's declaration, its pending cases and its stand-in's program. */
const declarations = (driverReaders) => ({
  paths: [
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Providers/*/provider.json',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Providers/*/pending.json',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Providers/*/Program/**',
  ],
  gates: [...driverReaders, 'tool-tests'],
  why: "a provider's declaration, its pending cases and its stand-in's program: the driver's own (ProviderFilesTests and the conformance suite), and the tools' stand-ins.test.ts and the family rehearsal read them by `stand-ins.mjs` (AGENTPROVIDER1b)",
});

/** The version every desktop assembly is stamped with, which BuildVersionTests reads (UPDATEVERSION, e7e96cfaa6). */
const canonVersion = (driverReaders) => ({
  paths: ['canon/canon.json'],
  gates: [...driverReaders],
  why: 'the release version every desktop assembly is stamped with, which the driver test BuildVersionTests reads beside the stage manifest',
});

/** The build settings every .NET project builds with, each tree's importing them: where the commit is stamped. */
const rootBuild = (driverReaders) => ({
  paths: ['Directory.Build.props'],
  gates: [...driverReaders, 'service', 'service-process', 'devkit', 'code-map'],
  why: "every .NET project builds with it, each tree's settings importing it: the desktop's, the service's and the devkit's suites, the headless host the family rehearsal drives, and the deployed shell, whose publish stamps the commit (BUILDSTAMP1)",
});

/** The rows, in REACH's order: a provider's declaration's files, its own sources, the stamped version and the stamp's settings. */
export const reachRows = (driverReaders) =>
  [declarations(driverReaders), sources(driverReaders), canonVersion(driverReaders), rootBuild(driverReaders)];

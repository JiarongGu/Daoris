import { test } from 'node:test';
import assert from 'node:assert/strict';
import { propertiesSetTrue, resultText } from './wiring.ts';

const none = new Set<string>();
const project = (body: string): string => `<Project Sdk="Microsoft.NET.Sdk">${body}</Project>`;

test('a project reference, a Using, a comment and a global using of a namespace leave a build file\'s result text as it was', () => {
  const before = project('<PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>');
  const after = project(`<!-- the fakes --><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
    <ItemGroup><ProjectReference Include="..\\Daoris.Testing\\Daoris.Testing.csproj" /><Using Include="Daoris.Ports.Browser" /></ItemGroup>`);
  assert.equal(resultText('A/A.csproj', after, none), resultText('A/A.csproj', before, none));
  const usings = 'namespace X;\nglobal using Xunit;\n';
  assert.equal(resultText('A/GlobalUsings.cs', `${usings}global using Daoris.Testing;\n`, none), resultText('A/GlobalUsings.cs', usings, none));
});

test('a package, a property or a setting still changes it, and so does a global using that is an alias or static', () => {
  const base = project('<ItemGroup><PackageReference Include="xunit" Version="2.9.0" /></ItemGroup>');
  assert.notEqual(resultText('A.csproj', base.replace('2.9.0', '2.9.1'), none), resultText('A.csproj', base, none));
  assert.notEqual(resultText('A.csproj', base.replace('</Project>', '<PropertyGroup><Nullable>enable</Nullable></PropertyGroup></Project>'), none), resultText('A.csproj', base, none));
  assert.notEqual(resultText('G/GlobalUsings.cs', 'global using static System.Math;\n', none), resultText('G/GlobalUsings.cs', '', none));
});

test('a group conditioned on a property no file sets to true is inert; one a file sets is kept', () => {
  const group = '<ItemGroup Condition="\'$(DaorisBannedEffects)\' == \'true\'"><PackageReference Include="Banned" PrivateAssets="all" /></ItemGroup>';
  const plain = project('');
  assert.equal(resultText('Directory.Build.props', project(group), none), resultText('Directory.Build.props', plain, none));
  const set = propertiesSetTrue([project('<PropertyGroup><DaorisBannedEffects>true</DaorisBannedEffects></PropertyGroup>')]);
  assert.notEqual(resultText('Directory.Build.props', project(group), set), resultText('Directory.Build.props', plain, set));
});

test('a source file is its own text', () => {
  assert.equal(resultText('A/Thing.cs', 'class A {}', none), 'class A {}');
});

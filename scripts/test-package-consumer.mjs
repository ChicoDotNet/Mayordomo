import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

const [packageDirectoryArg, version] = process.argv.slice(2);

if (!packageDirectoryArg || !version) {
  console.error(
    "Usage: node scripts/test-package-consumer.mjs <package-directory> <version>");
  process.exit(2);
}

const packageDirectory =
  path.resolve(packageDirectoryArg);

if (!fs.existsSync(packageDirectory)) {
  console.error(
    `Package directory does not exist: ${packageDirectory}`);
  process.exit(2);
}

const packagePath = path.join(
  packageDirectory,
  `Mayordomo.Core.${version}.nupkg`);

if (!fs.existsSync(packagePath)) {
  console.error(
    `Consumer smoke package does not exist: ${packagePath}`);
  process.exit(1);
}

const temporaryRoot = fs.mkdtempSync(
  path.join(
    os.tmpdir(),
    "mayordomo-core-consumer-"));

const xmlEscape = value => value
  .replaceAll("&", "&amp;")
  .replaceAll('"', "&quot;")
  .replaceAll("<", "&lt;")
  .replaceAll(">", "&gt;");

try {
  fs.writeFileSync(
    path.join(
      temporaryRoot,
      "NuGet.Config"),
    `<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="${xmlEscape(packageDirectory)}" />
  </packageSources>
</configuration>
`,
    "utf8");

  fs.writeFileSync(
    path.join(
      temporaryRoot,
      "Consumer.csproj"),
    `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Mayordomo.Core" Version="${version}" />
  </ItemGroup>
</Project>
`,
    "utf8");

  fs.writeFileSync(
    path.join(
      temporaryRoot,
      "Program.cs"),
    `using Mayordomo.Core;

var initial = MatchState.Create(
  MatchId.Create("consumer-smoke"),
  randomSeed: 42UL);

var transition = MatchEngine.Execute(
  initial,
  JoinParticipantCommand.Create(
    CommandId.Create("join-001"),
    initial.MatchId,
    initial.Revision,
    ParticipantId.Create("participant-001")));

if (transition.State.Revision != Revision.Create(1))
{
  throw new InvalidOperationException(
    "Installed package did not execute the expected transition.");
}

var replayed = MatchEngine.Reduce(
  initial,
  transition.Events);

if (MatchStateHasher.Compute(transition.State) !=
    MatchStateHasher.Compute(replayed))
{
  throw new InvalidOperationException(
    "Installed package did not replay deterministically.");
}

Console.WriteLine(
  $"Mayordomo.Core consumer smoke passed at revision {transition.State.Revision}.");
`,
    "utf8");

  run(
    "dotnet",
    [
      "restore",
      "Consumer.csproj",
      "--configfile",
      "NuGet.Config"
    ]);

  run(
    "dotnet",
    [
      "run",
      "--project",
      "Consumer.csproj",
      "--configuration",
      "Release",
      "--no-restore"
    ]);
}
finally {
  fs.rmSync(
    temporaryRoot,
    {
      recursive: true,
      force: true
    });
}

function run(command, args) {
  const result = spawnSync(
    command,
    args,
    {
      cwd: temporaryRoot,
      encoding: "utf8",
      stdio: "inherit"
    });

  if (result.error) {
    throw result.error;
  }

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

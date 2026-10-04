# Release versioning

`Mayordomo.Core` uses a four-component NuGet package version:

```text
{major}.{minor}.{YYYYMMDD}.{build}
```

A first production release on 4 October 2026 with build number 1 is therefore:

```text
1.0.20261004.1
```

## Version surfaces

The NuGet package version is the public release identity.

.NET assembly metadata cannot use the eight-digit date as one numeric component because assembly/file version components are limited to 16-bit unsigned values. Release builds therefore map one release identity onto four version surfaces:

| Surface | Mapping |
| --- | --- |
| PackageVersion | `{major}.{minor}.{YYYYMMDD}.{build}` |
| AssemblyVersion | `{major}.{minor}.0.0` |
| FileVersion | `{major}.{minor}.{YYDDD}.{build}` |
| InformationalVersion | `{PackageVersion}+{source-sha}` |

`YYDDD` combines the final two digits of the release year and the UTC calendar day-of-year. It exists only to provide legal, inspectable file metadata; it is not the package identity.

For the example release:

```text
PackageVersion       1.0.20261004.1
AssemblyVersion      1.0.0.0
FileVersion          1.0.26277.1
InformationalVersion 1.0.20261004.1+<source-sha>
```

## Build number

The build number is a positive monotonically increasing release-attempt number for a given release date and must be <= 65535.

The release workflow owns this value. A human must not silently rewrite a published package version.

## Compatibility

Within the 1.x product line, `AssemblyVersion` remains `1.0.0.0` unless a deliberate compatibility decision requires otherwise. Public API and replay/event compatibility are separately governed release contracts and are not inferred from the file version.

## Deterministic implementation

`scripts/compute-release-version.mjs` is the executable contract used by CI/release automation. Its tests reject malformed dates, illegal build numbers, and releases without source traceability.

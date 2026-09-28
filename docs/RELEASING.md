# Releasing Mayordomo Engine

There is currently no supported public release.

When releases begin, a release candidate must:

1. originate from a certified stable `main` state;
2. pass all build/test/repository-policy gates;
3. preserve deterministic/replay compatibility or explicitly version breaking changes;
4. contain no private Mayordomo.Game material;
5. have dependency/license provenance reviewed;
6. update CHANGELOG;
7. produce reproducible package artifacts;
8. record exact source SHA and package version.

Do not publish packages from an unreviewed working branch.

The first public release additionally requires the public-release checklist in the engine roadmap.

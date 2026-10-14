# Archive

Git-backed Archive institution for the Aetheric Forge web shell.

The runtime contracts and provider implementations remain reusable dependencies, pinned as submodules. The shell loads this module independently and never references its implementation project.

```sh
git submodule update --init --recursive
dotnet test tests/Archive.Web.Tests --configuration Release
dotnet publish Archive.Web --configuration Release
```

The plugin uses shared runtime contracts and private infrastructure dependencies. It retains its collectible assembly context while hosted. Definitions and bindings are under `institution/`; bindings contain deployment examples, never credentials.

Credentials are institution-owned under `Archive:S3`.

This is a backend module with no new UI routes. The existing `MinIO` store and default `forge-campus-archive` bucket are preserved. Owner-approved providers can be contributed under the `Archive` DI key, including the Decisions `adr-campus` store and key prefix. The runtime's Archive contract and typed Archivist remain available to the owner.

# NeuroMod Integration API

This folder contains the NeuroMod API surface used by other parts of the mod and by tests.

Public types
- `IApiClient` — abstraction for sending messages through the Neuro SDK websocket.
- `ApiClient` — static wrapper around a pluggable `IApiClient` instance (default: `ApiClientImpl`).
- `ApiClientImpl` — default implementation that forwards to `NeuroSdk.Websocket.WebsocketConnection`.

Guidelines
- Prefer depending on `IApiClient` in non-Unity testable classes and use DI to supply `ApiClient.Instance` or a mock in tests.
- Avoid direct references to `NeuroSdk.Websocket.WebsocketConnection` outside of `ApiClientImpl` to keep higher-level code testable.
- Use `ApiClient.BuildContextMessage`/`SendContext` for simple user-facing messages, and `ApiClient.Send`/`SendImmediate` for arbitrary SDK messages.

Testing
- Replace `ApiClient.Instance` with a test double in unit tests. `ApiClientImpl` also exposes a `TestSendOverride` action for quick interception but prefer full mocks/stubs.

Next steps (suggested)
- Add XML docs to other public types in `NeuroMod` to establish a clear API contract.
- Consolidate public API types under a single `NeuroMod.Integration.Api` namespace (already used here).
- Create unit tests that verify `ApiClient` forwarding behavior and `TestSendOverride`.

If you want, I can now:
- add XML doc comments across the public API types in `NeuroMod` (small batch),
- or consolidate static usages across the codebase to prefer `IApiClient` injection.

Which would you like next?
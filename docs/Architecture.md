# Architecture

The application has three production .NET projects and an optional React frontend. Features own their contracts and behavior; the host composes them for the selected frontend.

| Owner | Responsibility and dependencies |
| --- | --- |
| `GrumPlates.Core` | Feature domain rules, API contracts and request/response DTOs. No ASP.NET Core, EF Core or browser transport dependency. |
| `GrumPlates.Client` | Blazor routes, components, client API implementations and feature state. References Core; does not reference the server, EF Core or Npgsql. |
| `GrumPlates` | HTTP endpoints, server feature operations, EF persistence and hosting. References Core and, only for BlazorAuto, Client. |
| `GrumPlates.React` | React routes and feature UI, using the server's generated OpenAPI contract over HTTP. It is not a .NET assembly. |

`frontend-profile.txt` selects BlazorAuto or React. Profile-specific composition and publish inputs exclude the other renderer's runtime dependencies and assets. See [frontend profiles](FrontendProfiles.md), [server features](Features/Server.md), [Blazor features](Features/Blazor.md) and [React development](Frontend/React.md).

## Accounts and persistence

Server account composition, endpoints, security, Identity integration, email, account persistence and Blazor Identity UI live under `GrumPlates/Features/Accounts`. Shared account DTOs live in `GrumPlates.Core/Features/Accounts/Contracts`. React account session and private-client decoration remain under `app/features/accounts`.

The shared `AppDbContext`, design-time factory and migrations stay under server `Infrastructure/Persistence`. Books mappings live with Books. Accounts owns its outbox entity and operations, while the outbox mapping remains inline in the shared context. `ApplicationUser` lives in `Features/Accounts/Identity`, in the matching namespace.

Books deliberately consumes Accounts' current-user abstraction and protected endpoint policy for private data. Accounts owns cookie identity, security-stamp revocation, captured-owner and antiforgery protection. Books owns its queries, mutations and feature cache invalidation. Keep authorization and owner scoping on the server. [Accounts](Accounts.md) describes enabled/disabled behavior, private state, cookies and Data Protection recovery.

## Frontend operations

React's generated-client factory and result helper are neutral transport operations in `app/api/api-client.ts` and `app/api/api-result.ts`. Public catalog requests explicitly omit credentials. Private features use the Accounts-decorated provider client with its session, captured-owner, cancellation and antiforgery behavior. Keep generated schemas in sync through the [documented generation workflow](Frontend/React.md#change-a-typed-public-feature).

The Blazor Books UI uses ordinary routes for the public catalog and private list, details, create, and edit flows. `BookForm` is shared by create and edit; `BookDetailsView` is shared by public and private details. Public and private list state remain separate, and private state clears and reloads when the authenticated identity changes. Components use the existing Books API contracts and feature state rather than issuing raw HTTP requests.

## Verification boundaries

The architecture suite identifies Core, Server and Client through assembly markers in `GrumPlates.Test/Architecture/Support/ArchitectureAssemblies.cs`. It checks dependency direction, feature placement, DTO ownership, HTTP use, DI/profile composition, endpoint metadata and persistence mapping. Accounts checks keep account types and source out of the retired `Login` namespaces and folders.

Architecture checks complement behavior tests. Use the complete [local gate](Test.md#local-gate), both frontend profiles and the relevant explicitly enabled [browser checks](Testing/Browser.md). Keep generated React contracts, runtime endpoint metadata, profile-specific publishing, migration tooling and deployment audits consistent with the same source. Runtime secrets and machine-specific settings belong outside these architectural descriptions.

# Architecture illustrations (3+1 views)

Status: in progress (A00–A05 done). Prepared 2026-11-10 from source baseline `2c55a7ccd9bc2c4b3f85574e672e969a9ce80b72`. This plan changes documentation only and does not authorize deployment.

This plan is written so that an executor with no prior context can finish it. Do the steps in order. Do not skip a **Verify** block. When a fact in this plan no longer matches the code, the code wins: update the fact in this plan in the same PR and draw what the code does.

### How to run this plan

- Start it with `/goal Plans/ArchitectureIllustrations.md` (or by reading this file) from the primary GrumPlates checkout. Read `AGENTS.md` first; it overrides nothing here but sets repository rules.
- Work autonomously. The owner authorized branches, commits, the documentation PR(s), squash merges and closeout for this plan. Do not stop to ask for confirmation between steps. Ask only if a step's **Done when** cannot be reached after fixing what you can. Deployment is out of scope; never dispatch one.
- Progress lives in this file: tick each step's box and add a line to section 10 as you go, so a later session can resume from the first unticked step.
- The goal is complete when A19 is done: all 11 diagrams, the READMEs and links are on `origin/main`, and this plan is marked complete with SHAs.

## 1. Goal and scope

Add draw.io diagrams under `docs/illustrations/` that describe GrumPlates using the **3+1 approach** to architecture description (Christensen, Corry and Hansen, "An Approach to Software Architecture Description Using UML"). The owner learned this terminology in a Software Architecture course, so labels, titles and README prose use it exactly as defined in section 2.

In scope:

- Module viewpoint (development view): packages, classes, interfaces and their dependencies.
- Component & Connector viewpoint (runtime view): runtime components, connectors and connector protocols, including HTTP status codes of the Books API.
- Allocation viewpoint (deployment view): software elements allocated to hardware nodes for the supported deployment targets.
- The "+1": architecturally significant requirements (scenarios and quality attributes) in README prose that ties the views together.
- A small validator, links from existing docs, and a removal rule for the React-only clone guide.

Out of scope: changing source code, tests, CI, deployment files or the API; Accounts feature internals beyond what Books uses; diagrams of the test project; hand-drawing every endpoint (OpenAPI is the complete contract); porting to downstream repositories.

### Frontend rule (important)

A fork selects **one** frontend profile in `frontend-profile.txt`: `BlazorAuto` or `React`. A diagram must never show both frontends side by side.

- **Shared diagrams** show only what is identical for both profiles: Core domain/contracts/use cases, server services, endpoints, persistence, the HTTP API contract and deployment. They must not name a Blazor or React type, file or mechanism. Where a client appears, call it "API client" or "Frontend assets (selected profile)".
- **Profile diagrams** show one profile only and live in that profile's folder, so a fork can delete the folder it does not use.

## 2. Terminology (use these words)

| Term | Meaning in this plan |
| --- | --- |
| Structure | The set of elements and relations that exist in the system. Architects design structures. |
| View | A representation of a structure, drawn for stakeholders. Each `.drawio.svg` file is one view. |
| Viewpoint | The rules for making a kind of view: concerns, stakeholders, element and relation types. |
| Module viewpoint (development view) | How functionality maps to static development units. Elements: package, class, interface. Relations: association, generalization, realization, dependency. It is fine to omit elements if that communicates better. Stakeholders: architects and developers. |
| Component & Connector (C&C) viewpoint (runtime view) | Runtime mapping of functionality to components. A **component** is a black-box functional unit with a well-defined responsibility (the "what"). A **connector** is a first-class communication path between components that defines how control and data are exchanged (the "how"). A connector's **protocol** defines incoming and outgoing operations and their order; sequence diagrams describe protocols. The diagram cannot stand alone: the README lists component responsibilities and connector protocols. |
| Allocation viewpoint (deployment view) | How software elements (from the C&C view) map to environmental elements (hardware nodes). Concerns: what software requires (CPU, memory, network bandwidth) and what hardware provides. Relations: allocated-to (software on node), dependency (between software elements), protocol link (between nodes). Stakeholders: maintainers, operators, developers, architects. |
| +1 | Architecturally significant requirements (ASRs): scenarios (a specific path through a use case) and quality attributes (modifiability, availability, performance, security …). |
| Layered pattern (module) | Uses-relation strictly in one direction. GrumPlates is a relaxed form: every arrow points towards `GrumPlates.Core`. |
| Client-server pattern (C&C) | Clients request services from a server over a protocol. |
| Shared-data / repository pattern (C&C) | Components create, store and access persistent data through a data connector such as SQL. |
| Publish-subscribe (C&C) | Components publish messages to a channel; subscribers receive them. Used for cross-node cache invalidation. |
| Multi-tier pattern (allocation) | Components distributed over tiers of hardware connected by a communication medium. |

## 3. Deliverables

```text
docs/illustrations/
  README.md                                  terminology, +1, shared views, links to profile folders
  shared/
    module-books-core-and-server.drawio.svg  Module: Books decomposition (Core + server)
    cc-protocol-get-book.drawio.svg          C&C: protocol of GET /api/books/{id}
    cc-protocol-create-book.drawio.svg       C&C: protocol of POST /api/books
    allocation-single-node.drawio.svg        Allocation: LocalSingleNode
    allocation-four-node.drawio.svg          Allocation: LocalCluster and Cloud
  blazorauto/
    README.md                                element catalog for BlazorAuto views
    module-packages.drawio.svg               Module: package overview
    module-books-client.drawio.svg           Module: Books decomposition (Blazor client)
    cc-runtime.drawio.svg                    C&C: runtime overview
  react/
    README.md                                element catalog for React views
    module-packages.drawio.svg
    module-books-client.drawio.svg
    cc-runtime.drawio.svg
```

Also edited: `docs/Architecture.md` (one link), `docs/Frontend/ReactOnlyClone.md` (one inventory row), and this plan (status and integration record).

## 4. Decisions

| Decision | Reason |
| --- | --- |
| Commit only `.drawio.svg`; do not commit separate `.drawio` sources. | One file is both the image GitHub renders and the editable diagram (draw.io embeds the model in the SVG `content` attribute). Two copies would drift. |
| Export with `--embed-svg-fonts false`. | A probe was 64 KB with fonts and 13 KB without. Fonts are not needed for GitHub rendering. |
| Never set `fillColor`, `strokeColor` or `fontColor` to a hex value. | draw.io 26 exports default colours as `light-dark(...)`, so diagrams follow light/dark mode. Custom colours break that. The validator enforces it. Distinguish things with dashed lines, bold text, stereotypes and frames instead of colour. |
| One draw.io page per file. | GitHub renders only the first page. The validator enforces it. |
| No IPs, domains, hostnames other than template role names, or port numbers. Use setting names such as `app_port`. | AGENTS.md forbids hard-coded cluster values; forks read theirs from configuration. Role names such as `node-main` and `cloud-app1` come from tracked example files and are allowed. |
| Do not show CPU/memory numbers unless the repository configures them; label the source. | Allocation concerns include resources, but invented numbers mislead. Section 7.6 lists the configured ones. |
| Protocol diagrams cover two representative operations; the README holds the Books status table and links the full OpenAPI file. | OpenAPI (`GrumPlates.React/api/openapi.json`) is the complete, CI-checked contract. Hand-drawing every endpoint would drift. |
| English labels and prose. | The repository is English. |
| `.svg`/`.drawio` under `docs/` count as documentation-only (`[skip ci]`). | Changed in AGENTS.md together with `Deployment/Common/Scripts/verify-operation-source.py` (same PR as this plan). Nothing builds from `docs/`; the Docker build never copies it. |

## 5. Tooling (verified 2026-11-10)

- draw.io desktop 26.1.1 was installed and its CLI export worked. The default Windows install is `C:\Program Files\draw.io\draw.io.exe`; on Linux the command is `drawio`. Newer versions should work the same; re-run A02 to confirm.
- In Git Bash, call it as `"/c/Program Files/draw.io/draw.io.exe"`. Each export takes a few seconds and prints `<input> -> <output>`. Chromium lines such as `ERROR:command_buffer_proxy_impl.cc … GPU state invalid` are harmless when the `->` line appears and the output file exists.
- The exported SVG was UTF-8 with LF line endings, no NUL bytes and no trailing whitespace, so it passes `git diff --check` and the documentation-only content checks.
- Optional for humans: the VS Code extension "Draw.io Integration" (`hediet.vscode-drawio`) opens `.drawio.svg` files directly.

Commands (run from the repository root; `SRC` is the scratch source, `OUT` the committed file):

```bash
DRAWIO="/c/Program Files/draw.io/draw.io.exe"   # Linux: DRAWIO=drawio
SRC=Plans.local/ArchitectureIllustrations/src/shared-cc-protocol-get-book.drawio
OUT=docs/illustrations/shared/cc-protocol-get-book.drawio.svg
PNG=Plans.local/ArchitectureIllustrations/preview/shared-cc-protocol-get-book.png

"$DRAWIO" -x -f svg -e --embed-svg-fonts false -b 10 -o "$OUT" "$SRC"   # committed, editable SVG
"$DRAWIO" -x -f png -b 10 -o "$PNG" "$SRC"                               # preview to look at
```

To edit an existing committed diagram without the GUI, extract its model first:

```bash
python -I -c "import html,re,sys; t=open(sys.argv[1],encoding='utf-8').read(); open(sys.argv[2],'w',encoding='utf-8').write(html.unescape(re.search(r'content=\"([^\"]*)\"',t).group(1)))" docs/illustrations/shared/cc-protocol-get-book.drawio.svg Plans.local/ArchitectureIllustrations/src/shared-cc-protocol-get-book.drawio
```

`Plans.local/` is git-ignored; keep sources and previews there and never commit them.

## 6. Drawing conventions

### 6.1 Every diagram

1. A title text cell at top-left: `<Viewpoint> (<view>) · <what>`, for example `Module viewpoint (development view) · Package overview — BlazorAuto profile`. Shared diagrams end with `— both frontend profiles`. Profile diagrams end with `— BlazorAuto profile` or `— React profile`.
2. A legend (note shape) at bottom-left, listing only the notations used in that diagram.
3. Canvas at most about 1200 px wide; keep 40 px gaps between boxes; labels must not overlap lines or other labels.
4. Human-readable cell ids (`pkg-core`, `cmp-web`, `msg-get`), unique within the file.
5. Arrows: a dependency points **from** the element that depends **to** the element it uses. A realization points from the class to the interface.

### 6.2 Style catalog (all verified to render with draw.io 26.1.1)

| Key | Use | `style` attribute |
| --- | --- | --- |
| `title` | Title text | `text;html=1;align=left;verticalAlign=middle;fontSize=14;` with label `<b>…</b>` |
| `package` | UML package | `shape=folder;fontStyle=1;spacingTop=10;tabWidth=40;tabHeight=14;tabPosition=left;html=1;whiteSpace=wrap;` |
| `package-list` | Package with contents listed | `shape=folder;tabWidth=40;tabHeight=14;tabPosition=left;html=1;whiteSpace=wrap;align=left;verticalAlign=top;spacingTop=16;spacingLeft=8;` with label `<b>Name</b><hr>line<br>line` |
| `class` | Class or interface | `rounded=0;whiteSpace=wrap;html=1;verticalAlign=top;` with label `«interface»<br><b>IBooksApi</b><hr><div style="text-align:left">+ GetAsync()<br>…</div>` |
| `component` | C&C component | `html=1;whiteSpace=wrap;` with label `«component»<br><b>Name</b>`, plus a child icon cell (see skeleton) |
| `external` | External system | `html=1;whiteSpace=wrap;dashed=1;` with label `«external»<br><b>Name</b>` |
| `node` | Hardware node | `verticalAlign=top;align=left;spacingTop=8;spacingLeft=2;spacingRight=12;shape=cube;size=10;direction=south;html=1;whiteSpace=wrap;` with label `«device»<br><b>node-db</b>` |
| `artifact` | Deployed software element | `shape=note;size=12;html=1;whiteSpace=wrap;` with label `«artifact»<br><b>web container</b>` |
| `lifeline` | Sequence lifeline | `shape=umlLifeline;perimeter=lifelinePerimeter;whiteSpace=wrap;html=1;container=0;collapsible=0;size=40;` |
| `frame` | `alt` / `opt` fragment, or group | `shape=umlFrame;whiteSpace=wrap;html=1;width=50;height=20;` |
| `guard` | Guard text in a fragment | `text;html=1;align=left;fontSize=11;` with label `[condition]` |
| `note` | Legend or note | `shape=note;whiteSpace=wrap;html=1;size=14;align=left;spacingLeft=6;fontSize=11;` |
| `uses` | Dependency | `endArrow=open;endSize=12;dashed=1;html=1;rounded=0;` label `«uses»` |
| `realizes` | Realization | `endArrow=block;endFill=0;endSize=12;dashed=1;html=1;rounded=0;` |
| `association` | Association | `endArrow=open;endSize=12;html=1;rounded=0;` (label the role, for example `Book 1`) |
| `connector` | C&C connector | `endArrow=none;html=1;rounded=0;strokeWidth=2;` label = protocol name |
| `connector-dir` | Directed connector (one-way messages) | `endArrow=open;endSize=10;html=1;rounded=0;strokeWidth=2;` |
| `allocated` | Allocated-to (when not drawn by nesting) | `endArrow=open;endSize=10;dashed=1;html=1;rounded=0;` label `«allocated-to»` |
| `protocol-link` | Link between nodes | `endArrow=none;html=1;rounded=0;strokeWidth=2;` label = protocol |
| `msg` | Synchronous message | `html=1;verticalAlign=bottom;endArrow=block;endFill=1;rounded=0;` |
| `reply` | Reply message | `html=1;verticalAlign=bottom;endArrow=open;dashed=1;endSize=8;rounded=0;` |
| `separator` | Operand separator in `alt` | `endArrow=none;dashed=1;html=1;rounded=0;` |

Sequence messages, replies and separators are edges **without** `source`/`target`; give absolute `sourcePoint`/`targetPoint` (x of each lifeline centre = lifeline x + width/2). Prefer allocated-to by nesting: set an artifact's `parent` to the node cell, with coordinates relative to the node.

In `value` attributes, escape HTML: `<` → `&lt;`, `>` → `&gt;`, `&` → `&amp;amp;` inside HTML labels (as in `C&amp;amp;C`), `"` → `&quot;`. `«` and `»` can be written directly.

### 6.3 Skeleton (copy for every diagram)

```xml
<mxfile host="drawio" version="26.1.1">
  <diagram id="REPLACE-file-name" name="REPLACE title">
    <mxGraphModel grid="1" gridSize="10" page="1" pageWidth="1200" pageHeight="800" math="0" shadow="0">
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />
        <mxCell id="title" value="&lt;b&gt;Module viewpoint (development view) · Package overview — BlazorAuto profile&lt;/b&gt;" style="text;html=1;align=left;verticalAlign=middle;fontSize=14;" vertex="1" parent="1">
          <mxGeometry x="20" y="10" width="900" height="30" as="geometry" />
        </mxCell>
        <!-- Package -->
        <mxCell id="pkg-core" value="GrumPlates.Core" style="shape=folder;fontStyle=1;spacingTop=10;tabWidth=40;tabHeight=14;tabPosition=left;html=1;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="40" y="60" width="160" height="80" as="geometry" />
        </mxCell>
        <mxCell id="pkg-client" value="GrumPlates.Client" style="shape=folder;fontStyle=1;spacingTop=10;tabWidth=40;tabHeight=14;tabPosition=left;html=1;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="300" y="60" width="160" height="80" as="geometry" />
        </mxCell>
        <!-- Dependency between two vertices -->
        <mxCell id="dep-client-core" value="«uses»" style="endArrow=open;endSize=12;dashed=1;html=1;rounded=0;" edge="1" parent="1" source="pkg-client" target="pkg-core">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <!-- Component with UML component icon -->
        <mxCell id="cmp-web" value="«component»&lt;br&gt;&lt;b&gt;Web host&lt;/b&gt;" style="html=1;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="40" y="200" width="180" height="60" as="geometry" />
        </mxCell>
        <mxCell id="cmp-web-icon" value="" style="shape=module;jettyWidth=8;jettyHeight=4;" vertex="1" parent="cmp-web">
          <mxGeometry x="1" width="20" height="20" relative="1" as="geometry"><mxPoint x="-27" y="7" as="offset" /></mxGeometry>
        </mxCell>
        <!-- Sequence message with absolute points -->
        <mxCell id="msg-1" value="GET /api/books/{id}" style="html=1;verticalAlign=bottom;endArrow=block;endFill=1;rounded=0;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="360" y="130" as="sourcePoint" /><mxPoint x="660" y="130" as="targetPoint" /></mxGeometry>
        </mxCell>
        <!-- Legend -->
        <mxCell id="legend" value="&lt;b&gt;Notation&lt;/b&gt;&lt;br&gt;folder = package&lt;br&gt;dashed open arrow = «uses» dependency" style="shape=note;whiteSpace=wrap;html=1;size=14;align=left;spacingLeft=6;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="20" y="680" width="260" height="80" as="geometry" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
```

Delete skeleton cells you do not use. Every `edge` with `source`/`target` must reference ids that exist.

## 7. Fact sheets (research completed 2026-11-10)

Re-check in A03. Paths are repository-relative.

### 7.1 Projects and dependencies

| Package | Kind | References | Contents to list |
| --- | --- | --- | --- |
| `GrumPlates.Core` | .NET class library | none (no ASP.NET Core, EF Core or browser transport) | `Features/Accounts/Contracts`, `Features/Books/Contracts`, `Features/Books/Domain`, `Features/Books/UseCases` |
| `GrumPlates` (server host) | ASP.NET Core web app | Core; **Client only when `FrontendProfile == BlazorAuto`** (`GrumPlates/GrumPlates.csproj`, conditional ItemGroup) | `Features/Accounts`, `Features/Books`, `Infrastructure/Hosting`, `Infrastructure/Persistence`, `Frontend` |
| `GrumPlates.Client` (BlazorAuto only) | Blazor WebAssembly library | Core | `Features/AppShell`, `Features/Accounts`, `Features/Books` |
| `GrumPlates.React` (React only) | TypeScript SPA (React Router, TanStack Query, openapi-fetch); not a .NET assembly | no compile-time reference; uses the build artifact `GrumPlates.React/api/openapi.json` and generated `app/api/generated/schema.d.ts`, both generated from the server by `scripts/openapi-contract.mjs` and checked in CI | `app/routes`, `app/features/books`, `app/features/accounts`, `app/api`, `app/components` |
| `GrumPlates.Test` | tests | all three .NET projects | omit from diagrams; mention in the legend |

Rules enforced by `GrumPlates.Test/Architecture` (put the relevant ones in a note on each package overview): Core does not reference ASP.NET Core or EF Core; Client does not reference EF Core or Npgsql; Client references Core and not the server; request/response DTOs live only in Core (`UseCases` or Account `Contracts`); Core API interfaces live in `Contracts`; Blazor components do not inject `HttpClient`; the server exposes no `HttpClient` surface; every Core API interface has a server and a client implementation; entity configurations live under feature `Persistence` namespaces and target Core domain entities.

### 7.2 Books: Core and server (shared)

- Core `Features/Books/Contracts`: `«interface» IBooksApi` with `GetAsync`, `SearchAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`; `«interface» IAuthorBooksApi` with `GetAsync`, `GetByIdAsync`; static class `BookRules`.
- Core `Features/Books/Domain`: `Book`; `UserBook` and `AuthorBook`, each with association `Book` (property `Book Book`). These are associations, not generalization.
- Core `Features/Books/UseCases`: request/response DTO folders `CreateBook`, `DeleteBook`, `GetAuthorBook`, `GetAuthorBooks`, `GetBook`, `GetBooks` (includes `SearchBooksRequest/Response`), `UpdateBook`, `Shared` (`BookUrlValidation`). Draw as one package listing the folders, not one class per DTO.
- Server `Features/Books/Services`: `BooksServerService` realizes `IBooksApi`; it uses `AppDbContext` (via `IDbContextFactory`), `HybridCache`, `ICacheInvalidator`, `ICurrentUserAccessor`. `AuthorBooksServerService` realizes `IAuthorBooksApi`; uses `AppDbContext`, `HybridCache`.
- Server `Features/Books/Endpoints`: `BooksEndpoints` (`/api/books`) uses `IBooksApi`; `AuthorBooksEndpoints` (`/api/author-books`) uses `IAuthorBooksApi`.
- Server `Features/Books/Persistence`: `BookEntityTypeConfiguration` configures `Book`.
- Server `Features/Books/Caching`: `BooksCacheKeys`, `AuthorBooksCacheKeys`, `BooksCacheOptions`. `AuthorBookcase/Seed`: `AuthorBookSeeder`.
- Cross-feature and platform dependencies: `ICurrentUserAccessor` is internal to `GrumPlates/Features/Accounts/Identity` (Books uses Accounts, as `docs/Architecture.md` says). `AppDbContext` is in `GrumPlates/Infrastructure/Persistence`. `ICacheInvalidator` is in `GrumPlates/Infrastructure/Hosting/CacheInvalidation`.

### 7.3 Books client: BlazorAuto profile

- `GrumPlates.Client/Features/Books`: `BooksClientService` realizes `IBooksApi` over `HttpClient`; `AuthorBookcase/AuthorBooksClientService` realizes `IAuthorBooksApi`; `BooksAntiforgeryHandler` (a `DelegatingHandler`) adds `X-Expected-User` and `X-CSRF-TOKEN` to private requests. Registration: `GrumPlates.Client/Program.cs`.
- State: `UserBookcase/UserBookcaseState`, `AuthorBookcase/AuthorBookcaseState`.
- Route components (`Routes/`): `Index.razor` (`/`, `/books`), `BookCreate.razor` (`/books/new`), `BookEdit.razor` (`/books/{Id:int}/edit`), `UserBookDetails.razor` (`/books/{Id:int}`), `AuthorBookDetails.razor` (`/books/catalog/{Id:int}`), `AuthorDetails.razor` (`/books/author/{SeedKey}`).
- Shared components (`Shared/`): `BookForm`, `BookFormModel`, `BookDetailsView`, `BookListRow`; plus `UserBookcase.razor`, `UserBookcaseLoginPrompt.razor`, `AuthorBookcase.razor`.
- Dependencies (checked in A08): route components inject `IBooksApi` or `IAuthorBooksApi`, and the private routes also `UserBookcaseState`; `Index.razor` renders the bookcase components; `UserBookcase.razor` and `AuthorBookcase.razor` inject the two states.
- Key point for the diagram: the same Core interface `IBooksApi` is realized twice. During server rendering (SSR/prerender and interactive server) components resolve `BooksServerService` directly; in WebAssembly they resolve `BooksClientService`, which calls the HTTP API. Components depend only on `IBooksApi` or feature state. Show `BooksServerService` as a reference box labelled "(server, see shared view)".

### 7.4 Books client: React profile

- `app/api/api-client.ts` creates an `openapi-fetch` client typed by `app/api/generated/schema.d.ts`. `app/api/client.ts` is the public client (`credentials: "omit"`). `app/api/queries.ts` holds TanStack Query keys/queries for the public catalog. `app/api/api-result.ts` and `errors.ts` turn responses into results and safe errors.
- `app/features/accounts/account-session-controller.ts` (`AccountSessionController`) and `account-provider.tsx` give private features a session-decorated client: `credentials: "same-origin"`, headers `X-Expected-User` and `X-CSRF-TOKEN`.
- Books UI: `app/features/books/catalog-page.tsx`, `author-book-page.tsx`, `book-card.tsx`, `private-book-list.tsx` (`GET /api/books`, `DELETE /api/books/{id}`), `read-states.tsx`.
- Routes (`app/routes.ts`): `/` home, `/books` catalog, `/books/author/:seedKey`, `/my-books` (create/update via `POST /api/books`, `PUT /api/books/{id}`), `/auth`, `/auth/:action`.
- Accounts are disabled by default in React (see `docs/FrontendProfiles.md`); the private part appears only when enabled. Say this in a note.
- Module relation to draw: React modules depend on the generated contract types (`schema.d.ts`), which are derived from `openapi.json` («trace» or «generated from», dashed), which is derived from the server endpoints. There is no compile-time dependency on any .NET project.

### 7.5 Runtime components and connectors

Shared by both profiles (server side):

| Component | Responsibility | Source |
| --- | --- | --- |
| Web app (ASP.NET Core host) | Serves the selected frontend, JSON APIs under `/api`, health under `/health`; runs feature services | `GrumPlates/Program.cs` |
| Account mail outbox worker | Hosted service in the web app; sends queued account emails | `GrumPlates/Features/Accounts/Email/AccountMailOutboxWorker.cs` |
| PostgreSQL | Persistent data: books, identity, mail outbox, Data Protection keys (`PersistKeysToDbContext`) | `GrumPlates/Infrastructure/Hosting/AppCachingExtensions.cs` |
| Redis | Distributed cache (HybridCache L2) and pub/sub channel for cross-node cache invalidation | `Infrastructure/Hosting/CacheInvalidation/Redis*` |
| SMTP server «external» | Delivers account email | `AccountMailTransport.cs` (MailKit) |
| Reverse proxy (Caddy) | Edge HTTP proxy; load balances app nodes in four-node targets | `Deployment/*/ansible/roles/*caddy*/templates/app.caddy.j2` |
| Observability (optional) | Alloy agent receives OTLP; backend Prometheus, Loki, Tempo, Grafana, Alertmanager | `docs/Observability/Architecture.md` |

| Connector | Protocol | Notes |
| --- | --- | --- |
| Browser ↔ Reverse proxy ↔ Web app | HTTPS to the edge, HTTP behind it | Cookie authentication; private APIs need `X-Expected-User` and `X-CSRF-TOKEN` |
| Web app → PostgreSQL | SQL over the PostgreSQL wire protocol (EF Core + Npgsql) | shared-data pattern |
| Web app ↔ Redis | Redis protocol: cache get/set; publish/subscribe invalidation | publish-subscribe pattern |
| Outbox worker → SMTP | SMTP (MailKit) | |
| Web app → Alloy | OTLP | optional; must fail open |

BlazorAuto only: the browser first receives server-rendered HTML (SSR/prerender, public data via `PersistentComponentState`); interactive components run first on the server over a **SignalR circuit (WebSocket)**, then in **WebAssembly** after the runtime downloads; WebAssembly calls the JSON API over HTTPS. Account pages are server-rendered Razor Identity pages under `/Account`, including passkeys and optional Google sign-in (OAuth 2.0 «external», package referenced only in this profile).

React only: the web app serves a static SPA (`index.html` fallback for HTML navigations only, `ReactFrontendHosting.cs`); there is no server rendering and no Node server in the image. The SPA calls `GET /api/client-config`, the public catalog (`/api/author-books`, no credentials), and, when accounts are enabled, `/api/auth/*` and `/api/books` with cookies.

### 7.6 Deployment facts

| Target | Nodes (role names) | Allocation |
| --- | --- | --- |
| LocalSingleNode | one Linux PC | Host services: Caddy (site bound to `127.0.0.1`, origin port setting `public_origin_port`), cloudflared tunnel connector. Docker Compose project per app: `web` (app image `APP_IMAGE_REF`, published on `127.0.0.1:${APP_PORT}`), `postgres`, `redis` (both published on loopback only). Telemetry disabled. Nightly local PostgreSQL dump. Other app instances can share Caddy, cloudflared and Docker. |
| LocalCluster | `node-main`, `node-app1`, `node-app2`, `node-db` | `node-main`: Caddy load balancer + cloudflared + optional observability backend. `node-app1`/`node-app2`: `web` container + observability agent. `node-db`: PostgreSQL, Redis, postgres-exporter, redis-exporter. |
| Cloud (Hetzner, OpenTofu + Ansible) | `cloud-main`, `cloud-app1`, `cloud-app2`, `cloud-db` | Same roles as LocalCluster. Only `cloud-main` has public networking (Hetzner firewall); private nodes use its NAT; it is also the SSH bastion. Server type from variable `server_type` (currently `cx23` in `Deployment/Cloud/infra/opentofu/terraform.tfvars`). |

Protocol links (four-node): Internet → Cloudflare edge → cloudflared on main (outbound tunnel) → Caddy on main via loopback HTTP → app nodes via HTTP on `app_port` over the LAN / private network; `lb_policy cookie` (sticky), health check `/health/ready` every 5 s → data node via PostgreSQL protocol and Redis protocol (private network, host firewall).

Configured resources (show these only; label "Compose limit"):

| Service | Memory | CPU |
| --- | --- | --- |
| alloy (agent, every node) | 192m | 0.35 |
| node-exporter (every node) | 64m | 0.10 |
| prometheus | 384m | 0.50 |
| alertmanager | 128m | 0.15 |
| loki | 256m | 0.50 |
| tempo | 384m | 0.35 |
| grafana | 256m | 0.35 |
| postgres-exporter | 128m | 0.15 |
| redis-exporter | 96m | 0.10 |

`web`, `postgres` and `redis` have no Compose limits; do not invent any. LocalSingleNode readiness planning floors (`Deployment/LocalSingleNode/Readiness.md`): 4 logical CPUs, 7 GiB usable memory, 4 GiB available memory, 40 GiB free disk. If you show Hetzner `cx23` specs, take them from Hetzner's current catalog and label them "provider spec".

### 7.7 Books HTTP contract (from `GrumPlates.React/api/openapi.json`)

| Operation | Route | Responses |
| --- | --- | --- |
| ListBooks | `GET /api/books` | 200 401 403 409 429 503 |
| SearchBooks | `GET /api/books/search` | 200 400 401 403 409 429 503 |
| GetBook | `GET /api/books/{id}` | 200 401 403 404 409 429 503 |
| CreateBook | `POST /api/books` | 201 400 401 403 409 429 503 |
| UpdateBook | `PUT /api/books/{id}` | 204 400 401 403 404 409 429 503 |
| DeleteBook | `DELETE /api/books/{id}` | 204 401 403 404 409 429 503 |
| ListAuthorBooks | `GET /api/author-books` | 200 429 |
| GetAuthorBook | `GET /api/author-books/{id}` | 200 404 429 |

Meaning and source of each code on `/api/books`:

| Code | When | Source |
| --- | --- | --- |
| 200 / 201 / 204 | Success. 201 sets `Location: /api/books/{id}`. Private responses are `Cache-Control: private, no-store`. | `BooksEndpoints.cs` |
| 400 | Validation problem; route/body id mismatch on PUT ("Book id mismatch"); invalid antiforgery token on writes (`CsrfInvalid`) | `BooksEndpoints.cs`, `AccountPrincipalGuard.cs` |
| 401 | Not signed in. The authorization challenge returns 401 for `/api` instead of a login redirect; `AccountPrincipalGuard` repeats the check (`AuthenticationRequired`). | `IdentityServiceCollectionExtensions.cs` (`ConfigureApiCookieChallenges`), `AccountPrincipalGuard.cs` |
| 403 | Authorization policy denies an authenticated user (access-denied returns 403 for `/api`, never a redirect) | `IdentityServiceCollectionExtensions.cs`, `docs/Requirements.md` |
| 404 | Book does not exist **or belongs to another user** (does not reveal private records) | `BooksEndpoints.cs`, `docs/Features/Server.md` |
| 409 | `X-Expected-User` missing or not the signed-in user (`AccountChanged`: account switched in another tab) | `AccountPrincipalGuard.cs` |
| 429 | Rate limit (API default 60 requests/minute per client IP) | `AppRateLimiting.cs`, `docs/Requirements.md` |
| 503 | Database/persistence failure during the request (`AccountServiceUnavailable`) | `AccountApplicationBuilderExtensions.cs`, `AccountResults.cs` |

Server pipeline order (`GrumPlates/Program.cs`, verified 2026-11-10): account request policies (wrap the rest and turn persistence failures into 503) → authentication → rate limiter (429) → authorization (401 challenge, 403) → `AccountPrincipalGuard` (409; then antiforgery 400 for non-GET) → endpoint (validation 400, 404, success) → `BooksServerService` → PostgreSQL. Re-check in A03 and draw what the code does.

`CreateAsync`, `UpdateAsync` and `DeleteAsync` call `InvalidateAsync` after `SaveChangesAsync`, which goes through `ICacheInvalidator` (HybridCache removal plus Redis publish to other nodes).

### 7.8 The +1: scenarios and quality attributes

Scenarios (write each as a short paragraph and name the views it passes through):

- **S1 Create a private book.** A signed-in user opens the create form, enters title, author and URL, and saves. The client sends `POST /api/books` with the session cookie, `X-Expected-User` and `X-CSRF-TOKEN`; the server validates, stores the book for that owner, invalidates cached lists on all app nodes, and returns 201. Views: module (Books decomposition), C&C (runtime, create-book protocol), allocation (app node → data node).
- **S2 Browse the public catalog.** An anonymous visitor opens the books page. Public data comes from `/api/author-books` (or server rendering in BlazorAuto) through HybridCache; no credentials are sent. Views: C&C runtime, allocation.
- **S3 Another user's book.** A signed-in user requests a book id owned by someone else and receives 404, exactly as for a missing book. View: get-book protocol.

Quality attributes (one line each, with the tactic and where it shows):

- **Modifiability:** feature slices (Core/server/client per feature); swappable frontend profile; arrows point towards Core. Module views.
- **Availability:** two app nodes behind a health-checked load balancer; rolling app updates. Four-node allocation view.
- **Security:** owner-scoped 404, antiforgery, captured-owner header (409), rate limits (429), `private, no-store`, no public data-store ports. Protocol views and allocation views.
- **Performance:** HybridCache with Redis, bounded payloads, server rendering of public pages (BlazorAuto). Runtime views.
- **Deployability:** one image, three targets. Allocation views.

## 8. Steps

Each step lists **Do**, **Verify** and **Done when**. Tick the box in this file when a step is done and commit the plan with your work.

### Common diagram procedure (P)

Use for every `.drawio.svg`. `<folder>` is `shared`, `blazorauto` or `react`; `<name>` is the file name without `.drawio.svg`.

1. Copy the skeleton (6.3) to `Plans.local/ArchitectureIllustrations/src/<folder>-<name>.drawio`. Set `diagram id` to `<folder>-<name>` and `name` to the title.
2. Add every element and relation from the step's spec using the style keys in 6.2. Follow the layout sketch.
3. Export SVG and PNG with the commands in section 5 (`OUT=docs/illustrations/<folder>/<name>.drawio.svg`, `PNG=Plans.local/ArchitectureIllustrations/preview/<folder>-<name>.png`).
4. Open the PNG (look at the image; an AI executor reads the PNG file with its image-capable file reader). Check the review checklist below. Fix the source and re-export until every item passes.
5. Run the validator (A02) on `docs/illustrations`.

Review checklist (all must be true):

- [ ] Title follows 6.1 and names the right profile or "both frontend profiles".
- [ ] Every element and relation in the spec is present with the exact name; nothing else is added except helpful notes.
- [ ] Arrow directions are correct (dependent → dependency; class → interface for realization; request solid, reply dashed).
- [ ] No overlapping boxes or labels; no label crosses a line in a way that hides text; text is readable at 100 %.
- [ ] Legend lists every notation used.
- [ ] A profile diagram shows nothing from the other profile; a shared diagram names no Blazor or React type, file or mechanism.
- [ ] No IP address (write "loopback", not `127.0.0.1`), domain, port number, personal hostname or secret.

### A00 Preconditions

- [x] **Do:** `git fetch origin` and confirm that `origin/main` contains the documentation-rule change: `git show origin/main:AGENTS.md | grep -n "drawio"` prints the documentation-only line, and `git show origin/main:Deployment/Common/Scripts/verify-operation-source.py | grep -n DIAGRAM_SUFFIXES` prints matches.
- **Verify:** both commands print at least one line.
- **Done when:** both are true. If not, the prerequisite PR (branch `chore/template-batch-20261110`, which carries this plan and the rule) is not merged yet. Finish it instead of stopping:
  1. `gh pr list -R Grumlebob/GrumPlates --state all --head chore/template-batch-20261110 --json number,state,mergedAt` to find it. If no PR exists, push the branch and open one with `gh pr create -R Grumlebob/GrumPlates --base main --head chore/template-batch-20261110`.
  2. Wait for `build-test-push` on its exact head (`gh pr checks <n> -R Grumlebob/GrumPlates --watch`). If it fails, read the failing log, fix it on that branch, run the relevant local checks, push, and wait again.
  3. When green, `gh pr merge <n> -R Grumlebob/GrumPlates --squash --delete-branch`, then `git fetch origin` and repeat the A00 commands.
  Do not commit diagrams with `[skip ci]` before the rule is on `origin/main`.

### A01 Workspace

- [x] **Do:** create a branch from the latest `origin/main`: `git switch -c docs/architecture-illustrations origin/main` (or a separate worktree if the primary checkout is busy; never touch someone else's uncommitted work). Create the scratch folders `Plans.local/ArchitectureIllustrations/src` and `Plans.local/ArchitectureIllustrations/preview`.
- **Verify:** `git status --short` is empty; `git check-ignore Plans.local/ArchitectureIllustrations/src` prints the path (ignored).
- **Done when:** clean branch exists and scratch folders are ignored.

### A02 Toolchain and validator

- [x] **Do:**
  1. Run `"$DRAWIO" --version` (section 5). If draw.io is missing, install draw.io desktop from the official release (Windows: `winget install JGraph.Draw`).
  2. Save the validator below as `Plans.local/ArchitectureIllustrations/validate_illustrations.py`.
  3. Export the 6.3 skeleton unchanged to `Plans.local/ArchitectureIllustrations/probe/probe.drawio.svg` and run the validator on that folder.

```python
import html, pathlib, re, sys

root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else 'docs/illustrations')
files = sorted(root.rglob('*.drawio.svg'))
failed = not files
for path in files:
    data = path.read_bytes()
    text = data.decode('utf-8')
    match = re.search(r'content="([^"]*)"', text)
    model = html.unescape(match.group(1)) if match else ''
    labels = re.sub(r'<mxfile[^>]*>', '', model, count=1)
    checks = {
        'at most 2 MiB': len(data) <= 2 * 1024 * 1024,
        'no NUL or CR bytes': b'\0' not in data and b'\r' not in data,
        'editable diagram embedded': '<mxGraphModel' in model,
        'exactly one page': model.count('<diagram ') == 1,
        'no script content': not re.search(r'<script|javascript:|on(load|click|error)=', text, re.I),
        'no IPv4 literal': not re.search(r'\b(?:\d{1,3}\.){3}\d{1,3}\b', labels),
        'no custom colours': not re.search(r'(fillColor|strokeColor|fontColor)=#', labels),
    }
    for name, ok in checks.items():
        failed |= not ok
        print(f"{'ok  ' if ok else 'FAIL'} {path.relative_to(root)}: {name}")
sys.exit(1 if failed else 0)
```

- **Verify:** `python -I Plans.local/ArchitectureIllustrations/validate_illustrations.py Plans.local/ArchitectureIllustrations/probe` prints only `ok` lines and exits 0. (This validator was tested on 2026-11-10: it passed two good probes and failed a file with an IP literal and CR bytes.)
- **Done when:** export and validator work. Delete the probe folder.

### A03 Re-inventory facts

- [x] **Do:** run each command and compare the output with section 7. Update section 7 in this plan for any difference.

```bash
grep -n -E "ProjectReference|Condition=" GrumPlates/GrumPlates.csproj GrumPlates.Client/GrumPlates.Client.csproj
find GrumPlates.Core/Features/Books GrumPlates/Features/Books GrumPlates.Client/Features/Books -name "*.cs" -o -name "*.razor" | sort
grep -rn "@page" GrumPlates.Client/Features/Books/Routes/
cat GrumPlates.React/app/routes.ts
ls GrumPlates.React/app/features/books GrumPlates.React/app/api
python -I -c "import json;d=json.load(open('GrumPlates.React/api/openapi.json',encoding='utf-8'));[print(m.upper(),p,o.get('operationId'),' '.join(sorted(o['responses']))) for p,ops in d['paths'].items() for m,o in ops.items() if p.startswith('/api/books') or p.startswith('/api/author-books')]"
grep -n -E "^app\.Use|Use[A-Z][A-Za-z]+\(|Map[A-Z][A-Za-z]+\(" GrumPlates/Program.cs
grep -n -E "reverse_proxy|lb_policy|health_" Deployment/*/ansible/roles/*caddy*/templates/app.caddy.j2
```

- **Verify:** every row in 7.1–7.7 matches, or the plan was updated.
- **Done when:** the fact sheets are current. Record "A03 re-inventory at `<sha>`: no drift" (or the changes) in section 10.

### A04 Scaffold folders and README skeletons

- [x] **Do:** create `docs/illustrations/README.md`, `docs/illustrations/blazorauto/README.md`, `docs/illustrations/react/README.md` with headings only (content in A16). Root README headings: `# Architecture illustrations`, `## How to read these views` (terminology), `## Architecturally significant requirements (+1)`, `## Shared views`, `## Frontend profile views`, `## Editing diagrams`. Profile README headings: `# <Profile> profile views`, `## Package overview`, `## Books client decomposition`, `## Runtime (C&C) overview`.
- **Verify:** `git status` shows three new files under `docs/illustrations/`.
- **Done when:** scaffold exists.

### A05 `shared/module-books-core-and-server`

- [x] **Do:** procedure P. Title: `Module viewpoint (development view) · Books decomposition: Core and server — both frontend profiles`.
  - Outer `package` frames (large folders, contents drawn inside with `parent` set to the folder): `GrumPlates.Core › Features/Books` (left) and `GrumPlates › Features/Books` (right).
  - Inside Core: package `Contracts` with `class` boxes `«interface» IBooksApi` (six methods), `«interface» IAuthorBooksApi` (two methods), `BookRules` («static»); package `Domain` with `Book`, `UserBook`, `AuthorBook`; `package-list` `UseCases` listing the eight folders from 7.2.
  - Inside server: `BooksServerService`, `AuthorBooksServerService` (Services); `BooksEndpoints` (label `/api/books`), `AuthorBooksEndpoints` (`/api/author-books`) (Endpoints); `BookEntityTypeConfiguration` (Persistence); `package-list` `Caching` (three classes) and `AuthorBookcase/Seed` (`AuthorBookSeeder`).
  - Outside both, right edge: `class` boxes `AppDbContext` (label "Infrastructure/Persistence"), `ICacheInvalidator` ("Infrastructure/Hosting"), `«interface» ICurrentUserAccessor` ("Features/Accounts"), `HybridCache` ("Microsoft.Extensions.Caching.Hybrid").
  - Relations: `realizes` BooksServerService → IBooksApi; AuthorBooksServerService → IAuthorBooksApi. `association` UserBook → Book and AuthorBook → Book (label `Book 1`). `uses` BooksEndpoints → IBooksApi; AuthorBooksEndpoints → IAuthorBooksApi; BooksEndpoints → UseCases; BookEntityTypeConfiguration → Book; BooksServerService → AppDbContext, HybridCache, ICacheInvalidator, ICurrentUserAccessor, UseCases; AuthorBooksServerService → AppDbContext, HybridCache.
  - Note: "Endpoints depend on the Core interface, not on the service class. Each frontend profile adds a client-side realization of the same interfaces; see the profile views."
  - Layout sketch:

```text
+-- GrumPlates.Core › Features/Books --------+      +-- GrumPlates › Features/Books --------------+
| [Contracts] IBooksApi  IAuthorBooksApi     |<-----| BooksEndpoints      AuthorBooksEndpoints     |
|             BookRules                      |      | BooksServerService  AuthorBooksServerService | --> AppDbContext
| [Domain]    Book <- UserBook, AuthorBook   |<-----| BookEntityTypeConfiguration                  | --> HybridCache
| [UseCases]  CreateBook … UpdateBook        |      | Caching   AuthorBookcase/Seed                | --> ICacheInvalidator
+--------------------------------------------+      +----------------------------------------------+ --> ICurrentUserAccessor
```

- **Verify:** checklist in P; validator passes.
- **Done when:** file committed on the branch (`git add docs/illustrations/shared/module-books-core-and-server.drawio.svg`; commit message `docs: add Books core and server module view [skip ci]`).

### A06 `blazorauto/module-packages`

- [x] **Do:** procedure P. Title: `Module viewpoint (development view) · Package overview — BlazorAuto profile`.
  - `package-list` boxes: `GrumPlates (server host)` with its five sub-folders; `GrumPlates.Client (Blazor WebAssembly)` with its three sub-folders; `GrumPlates.Core` with its four sub-folders (7.1).
  - Relations: `uses` server → Client (label `«uses» (BlazorAuto profile)`); `uses` server → Core; `uses` Client → Core.
  - `note` "Enforced by GrumPlates.Test/Architecture": the rules from 7.1 that mention Core, Client or the server.
  - Legend also says: "GrumPlates.Test (omitted) references all three projects. Relaxed layering: all arrows point towards Core."
  - Layout: server top-left, Client top-right, Core bottom-centre.
- **Verify / Done when:** as A05 (commit `docs: add BlazorAuto package view [skip ci]`).

### A07 `react/module-packages`

- [x] **Do:** procedure P. Title: `Module viewpoint (development view) · Package overview — React profile`.
  - `package-list` boxes: `GrumPlates (server host)`, `GrumPlates.Core`, `GrumPlates.React (TypeScript SPA)` listing `app/routes`, `app/features/books`, `app/features/accounts`, `app/api`, `app/components`.
  - `artifact` boxes: `openapi.json` (label "GrumPlates.React/api/openapi.json") and `schema.d.ts` (label "app/api/generated").
  - Relations: `uses` server → Core. Dashed `uses` with label `«generated from»`: openapi.json → server; schema.d.ts → openapi.json. `uses` GrumPlates.React → schema.d.ts. No arrow from GrumPlates.React to any .NET package.
  - Note: "No compile-time dependency between the SPA and .NET. The contract is regenerated by `scripts/openapi-contract.mjs` and checked in CI." Plus the architecture rules from 7.1 that mention Core and the server.
- **Verify / Done when:** as A05 (commit `docs: add React package view [skip ci]`).

### A08 `blazorauto/module-books-client`

- [x] **Do:** procedure P. Title: `Module viewpoint (development view) · Books decomposition: Blazor client — BlazorAuto profile`.
  - Reference boxes (dashed `class`) on the left: `«interface» IBooksApi`, `«interface» IAuthorBooksApi` (label "GrumPlates.Core, see shared view"), `BooksServerService`, `AuthorBooksServerService` (label "server, see shared view").
  - Package `GrumPlates.Client › Features/Books` containing: `BooksClientService`, `AuthorBooksClientService`, `BooksAntiforgeryHandler` («DelegatingHandler»), `UserBookcaseState`, `AuthorBookcaseState`, `package-list` `Routes` (six components with their routes from 7.3), `package-list` `Shared` (four items), `package-list` `Bookcases` (`UserBookcase`, `UserBookcaseLoginPrompt`, `AuthorBookcase`).
  - Relations: `realizes` BooksClientService → IBooksApi; AuthorBooksClientService → IAuthorBooksApi; and (from the reference boxes) BooksServerService → IBooksApi; AuthorBooksServerService → IAuthorBooksApi. `uses` UserBookcaseState → IBooksApi; AuthorBookcaseState → IAuthorBooksApi; Routes → IBooksApi, IAuthorBooksApi, UserBookcaseState, Bookcases, Shared; Bookcases → UserBookcaseState, AuthorBookcaseState, Shared (corrected in A08 from the `@inject` and component usages); BooksClientService → BooksAntiforgeryHandler (label `«uses» HttpClient pipeline`).
  - Note: "Interactive Auto: during server rendering components get the server realization; in WebAssembly they get the client realization, which calls the HTTP API. Components never inject HttpClient."
- **Verify / Done when:** as A05 (commit `docs: add Blazor Books client module view [skip ci]`).

### A09 `react/module-books-client`

- [x] **Do:** procedure P. Title: `Module viewpoint (development view) · Books decomposition: React client — React profile`.
  - Package `app/routes`: `home.tsx`, `books.tsx`, `author-book.tsx`, `my-books.tsx` (with their paths from 7.4).
  - Package `app/features/books`: `catalog-page.tsx`, `author-book-page.tsx`, `book-card.tsx`, `private-book-list.tsx`, `read-states.tsx`.
  - Package `app/features/accounts`: `AccountSessionController`, `account-provider.tsx`.
  - Package `app/api`: `api-client.ts`, `client.ts` (public, no credentials), `queries.ts`, `api-result.ts`, `errors.ts`, `generated/schema.d.ts`.
  - Relations (`uses`): books.tsx → catalog-page; author-book.tsx → author-book-page; my-books.tsx → private-book-list, account-context; catalog-page and author-book-page → queries; queries → client.ts; private-book-list → account-context; `root.tsx` → account-provider; account-provider → account-context («provides»), AccountSessionController (corrected in A09 from the imports: private code reads `account-context.ts`, which `account-provider.tsx`, mounted in `root.tsx`, supplies); AccountSessionController → api-client.ts; client.ts → api-client.ts; api-client.ts → schema.d.ts; pages → api-result/errors (one arrow from the package is fine).
  - Note: "Public catalog requests omit credentials. Private requests use the session-decorated client (cookie, X-Expected-User, X-CSRF-TOKEN). Accounts are disabled by default in the React profile."
- **Verify / Done when:** as A05 (commit `docs: add React Books client module view [skip ci]`).

### A10 `blazorauto/cc-runtime`

- [x] **Do:** procedure P. Title: `Component & Connector viewpoint (runtime view) · Runtime overview — BlazorAuto profile`.
  - Components: `Browser: Blazor WebAssembly client`, `Reverse proxy`, `Web app (ASP.NET Core: SSR, interactive server, JSON API)`, `Account mail outbox worker` (draw inside the web app component as a nested component), `PostgreSQL`, `Redis`. Externals: `SMTP server`, `Google OAuth (optional)`. Optional group (dashed `frame` labelled `opt [observability enabled]`): `Alloy agent`, `Observability backend`.
  - Connectors with labels: Browser–Reverse proxy `HTTPS: HTML (SSR), JSON API, SignalR WebSocket circuit, WebAssembly assets`; Reverse proxy–Web app `HTTP`; Web app–PostgreSQL `SQL (EF Core + Npgsql)`; Web app–Redis `Redis: cache + pub/sub invalidation`; worker → SMTP `SMTP`; Web app → Google `OAuth 2.0 redirect flow` (the browser follows the redirect; a note may say so); Web app → Alloy `OTLP`; Alloy → backend `OTLP / logs`.
  - Note listing patterns: client-server, shared-data (PostgreSQL), publish-subscribe (Redis invalidation).
- **Verify / Done when:** as A05 (commit `docs: add BlazorAuto runtime view [skip ci]`).

### A11 `react/cc-runtime`

- [x] **Do:** procedure P. Title: `Component & Connector viewpoint (runtime view) · Runtime overview — React profile`.
  - Components as A10, but the browser component is `Browser: React SPA`, the web app is `Web app (ASP.NET Core: static SPA host, JSON API)`, and there is no Google OAuth and no SignalR.
  - Browser–Reverse proxy label: `HTTPS: static SPA assets, JSON API (public: no credentials; private: cookie + X-Expected-User + X-CSRF-TOKEN)`.
  - Note: "HTML navigations fall back to index.html; API, health and reserved account paths never do. Accounts are disabled by default."
- **Verify / Done when:** as A05 (commit `docs: add React runtime view [skip ci]`).

### A12 `shared/cc-protocol-get-book`

- [ ] **Do:** procedure P. Title: `Component & Connector viewpoint (runtime view) · Connector protocol: GET /api/books/{id} — both frontend profiles`.
  - Lifelines (left to right, 240 px apart): `API client (selected frontend)`, `Web app pipeline` (rate limiter, authentication, account guard, authorization), `Books endpoint`, `BooksServerService`, `PostgreSQL`.
  - First message: `GET /api/books/{id}` + second line `Cookie, X-Expected-User` from client to pipeline.
  - One `alt` frame with operands separated by `separator` lines, each with a guard and reply. Replies go back to the client:
    1. `[rate limit exceeded]` → `429 Too Many Requests`
    2. `[not signed in]` → `401 Unauthorized`
    3. `[authorization policy denies]` → `403 Forbidden`
    4. `[X-Expected-User ≠ signed-in user]` → `409 Conflict · AccountChanged`
    5. `[book missing or owned by another user]`: pipeline → endpoint `GetBookAsync`, endpoint → service `GetByIdAsync(id)`, service → PostgreSQL `SELECT … WHERE id AND owner`, replies `no row` / `null`, endpoint → client `404 Not Found (problem+json)`
    6. `[owner]`: same calls; replies `row` / `GetBookResponse`; endpoint → client `200 OK · GetBookResponse · Cache-Control: private, no-store`
    7. `[database failure]` → `503 Service Unavailable · AccountServiceUnavailable`
  - Note: "Operand order follows the server pipeline. 404 is the same for a missing book and another user's book, so private records are not revealed."
  - Canvas may be up to 1300 px tall.
- **Verify / Done when:** as A05 (commit `docs: add get-book protocol view [skip ci]`).

### A13 `shared/cc-protocol-create-book`

- [ ] **Do:** procedure P. Title: `Component & Connector viewpoint (runtime view) · Connector protocol: POST /api/books — both frontend profiles`.
  - Lifelines: `API client (selected frontend)`, `Web app pipeline`, `Books endpoint`, `BooksServerService`, `PostgreSQL`, `Redis`.
  - First message: `POST /api/books {title, author, url}` + `Cookie, X-Expected-User, X-CSRF-TOKEN`.
  - `alt` operands: `[rate limit, sign-in, policy or account check fails]` (one operand with a single reply `429 · 401 · 403 · 409 — see GET protocol`), `[invalid antiforgery token]` → `400 Bad Request · CsrfInvalid`, `[validation fails]` endpoint → client `400 Bad Request · ValidationProblem`, `[valid]`: endpoint → service `CreateAsync(request)`, service → PostgreSQL `INSERT book for owner`, service → Redis `publish invalidation (book lists)`, replies, endpoint → client `201 Created · Location: /api/books/{id} · CreateBookResponse`; `[database failure]` → `503`.
  - Note: "After saving, the service invalidates the changed book's cache keys; Redis pub/sub tells every app node to drop them (publish-subscribe pattern)." (Verified in 7.7; if A03 finds the invalidation removed, drop the Redis lifeline and this note.)
- **Verify / Done when:** as A05 (commit `docs: add create-book protocol view [skip ci]`).

### A14 `shared/allocation-single-node`

- [ ] **Do:** procedure P. Title: `Allocation viewpoint (deployment view) · LocalSingleNode — both frontend profiles`.
  - Nodes: `«device» User device` containing `«execution environment» Browser` containing artifact `Frontend assets (selected profile)`; `«external» Cloudflare edge`; `«device» Linux PC (single node)`.
  - Inside the Linux PC: host services `Caddy (site on loopback:public_origin_port)` and `cloudflared`; an `«execution environment» Docker Engine` frame containing a frame `Compose project (one per app)` with artifacts `web (APP_IMAGE_REF)`, `postgres`, `redis`, and a `«volume»` artifact `postgres_data`.
  - Protocol links: User device – Cloudflare edge `HTTPS`; Cloudflare edge – cloudflared `outbound tunnel`; cloudflared – Caddy `HTTP (loopback)`; Caddy – web `HTTP loopback:APP_PORT`; web – postgres `PostgreSQL protocol (loopback)`; web – redis `Redis protocol (loopback)`.
  - Note "Requirements": the readiness floors from 7.6 labelled "planning floor (Readiness.md)". Note: "Telemetry disabled. Other app instances may share Caddy, cloudflared and Docker. Nightly PostgreSQL dump."
- **Verify / Done when:** as A05 (commit `docs: add single-node allocation view [skip ci]`).

### A15 `shared/allocation-four-node`

- [ ] **Do:** procedure P. Title: `Allocation viewpoint (deployment view) · LocalCluster and Cloud (multi-tier) — both frontend profiles`.
  - Nodes: `User device` (as A14), `Cloudflare edge` («external»), `node-main / cloud-main`, `node-app1 / cloud-app1`, `node-app2 / cloud-app2`, `node-db / cloud-db`.
  - Allocations: main → `Caddy load balancer`, `cloudflared`, opt frame `observability backend` (prometheus 384m/0.50, alertmanager 128m/0.15, loki 256m/0.50, tempo 384m/0.35, grafana 256m/0.35). Each app node → Docker Engine → `web (APP_IMAGE_REF)`, opt `alloy 192m/0.35`, `node-exporter 64m/0.10`. db → Docker Engine → `postgres`, `redis`, `postgres-exporter 128m/0.15`, `redis-exporter 96m/0.10`, opt agent.
  - Protocol links: User – edge `HTTPS`; edge – cloudflared `outbound tunnel`; Caddy – each `web` `HTTP app_port · sticky cookie · health /health/ready every 5 s`; each web – postgres `PostgreSQL protocol (private network)`; each web – redis `Redis protocol (private network)`; agents → backend `telemetry (private)`.
  - Note "Target differences": LocalCluster = four Linux Mint PCs on a LAN. Cloud = four Hetzner servers (type from `server_type`); only `cloud-main` has public networking and is the SSH bastion; private nodes use its NAT; Hetzner and host firewalls. Note: "Resource labels are Compose limits; web, postgres and redis have none."
- **Verify / Done when:** as A05 (commit `docs: add four-node allocation view [skip ci]`).

### A16 README content

- [ ] **Do:** fill the README headings from A04. Rules: link instead of repeating long rules; use relative links; embed each diagram with `![<title>](<relative path>)` followed by 2–6 sentences.
  - Root `README.md`:
    - *How to read these views*: the terminology table from section 2 in shortened form (Viewpoint, View, Module, C&C, Allocation, +1) and one sentence on the 3+1 approach and its source.
    - *+1*: S1–S3 and the quality attributes from 7.8, each linking the views it uses.
    - *Shared views*: embed the five shared diagrams. Under the protocol diagrams add the Books status table from 7.7 (both tables), and link `../../GrumPlates.React/api/openapi.json` as the complete contract. Under allocation add the target table from 7.6 in one or two sentences per target, linking `../../Deployment/README.md`.
    - *Frontend profile views*: one sentence each linking `blazorauto/README.md` and `react/README.md`, and: "A fork that removes a frontend deletes that profile's folder and this link."
    - *Editing diagrams*: open `.drawio.svg` in draw.io desktop or the VS Code Draw.io extension and save in place; keep default colours; one page per file; no IPs/ports/hostnames; link to this plan for the export command and validator.
  - Each profile `README.md`: embed its three diagrams. Under the runtime view add the **element catalog** required by the C&C viewpoint: a component-responsibility table and a connector-protocol table (from 7.5, filtered to that profile). Under the package view list the enforced rules. Under the client decomposition explain how components reach the API.
- **Verify:** every embedded path exists (`grep -o '([^)]*\.drawio\.svg)' -r docs/illustrations --include=*.md` and check each with `ls`); every Markdown link target exists.
- **Done when:** READMEs are complete; commit `docs: describe architecture illustrations [skip ci]`.

### A17 Links from existing docs

- [ ] **Do:**
  1. `docs/Architecture.md`: after the first table, add one sentence: `Diagrams of these structures, using module, component-and-connector and allocation views, are in [architecture illustrations](illustrations/README.md).`
  2. `docs/Frontend/ReactOnlyClone.md`: add a row to the inventory table near "Docs and rules": `| Illustrations | docs/illustrations/blazorauto/ and its link in docs/illustrations/README.md | Delete |` (format paths as code like the other rows).
- **Verify:** `git diff` shows only these two small additions in those files.
- **Done when:** commit `docs: link architecture illustrations [skip ci]`.

### A18 Validation

- [ ] **Do:**
  1. `python -I Plans.local/ArchitectureIllustrations/validate_illustrations.py docs/illustrations` → exit 0, 11 files.
  2. Documentation-only proof (uses the same rule CI operations use):

```bash
python -I -c "import importlib.util,subprocess,sys; s=importlib.util.spec_from_file_location('m','Deployment/Common/Scripts/verify-operation-source.py'); m=importlib.util.module_from_spec(s); s.loader.exec_module(m); base=subprocess.check_output(['git','merge-base','origin/main','HEAD']).decode().strip(); ok=m.documentation_diff(base,'HEAD'); print('documentation-only:',ok); sys.exit(0 if ok else 1)"
```

  3. `git diff --check origin/main...HEAD` prints nothing.
  4. Look at all 11 PNG previews once more side by side: titles consistent, same font sizes, legends in the same corner.
  5. `git diff --name-status origin/main...HEAD` lists only `docs/illustrations/**`, `docs/Architecture.md`, `docs/Frontend/ReactOnlyClone.md` and `Plans/ArchitectureIllustrations.md`.
- **Verify:** all five pass.
- **Done when:** recorded in section 10 with the HEAD SHA.

### A19 PR, merge, verify, close out

- [ ] **Do:**
  1. Push the branch and open a PR to `main` (`gh pr create -R Grumlebob/GrumPlates`). Title: `[skip ci] docs: add 3+1 architecture illustrations`. Body: what was added, the A18 results, and a note that every changed file is documentation (Markdown or diagrams under `docs/`).
  2. Review the complete PR diff in GitHub, including that each SVG renders in the "Files changed" view.
  3. Squash-merge with the `[skip ci]` title and `--delete-branch`.
  4. `git fetch origin`; verify each file on `origin/main` equals the PR head: `git diff <pr-head-sha> origin/main -- docs/illustrations docs/Architecture.md docs/Frontend/ReactOnlyClone.md Plans/ArchitectureIllustrations.md` prints nothing.
  5. Set this plan's status to complete with the merge SHA (another small `[skip ci]` docs PR, or include it before merging if you already know the result).
  6. Closeout per AGENTS.md: delete the local branch, remove any worktree you created, delete `Plans.local/ArchitectureIllustrations/` (sources can be re-extracted from the SVGs with the section 5 command).
- **Verify:** `origin/main` contains the files; no leftover branch or worktree.
- **Done when:** plan marked complete with SHAs.

## 9. Pitfalls

- An `edge` with a `source`/`target` id that does not exist makes draw.io drop or misplace it. Check ids after every edit.
- Unescaped `<`, `>` or `&` inside a `value` breaks the XML; draw.io then exports nothing or an empty image. Validate by looking at the PNG every time.
- Do not set `container=1` on lifelines; the probe used `container=0` so messages with absolute points stay put.
- Do not copy names from memory. Use the fact sheets or the code.
- Do not show both frontends in one diagram. Do not add a Blazor or React name to a shared diagram.
- Do not add colour. Use stereotypes, dashed lines, frames and notes.
- A `[skip ci]` commit must contain only documentation (A18 step 2). If you had to change anything else, drop `[skip ci]` and run the full gate from AGENTS.md.

## 10. Progress and integration record

- 2026-11-10: plan prepared. The documentation-rule change (AGENTS.md, `verify-operation-source.py` and its tests, `.gitattributes`, `docs/Test.md`, `Deployment/LocalSingleNode/Operations.md`) ships in the same PR as this plan. Verified locally before commit: `test_operation_source.py` 12 tests passed in WSL; deployment audit passed. Toolchain probes (draw.io 26.1.1 SVG/PNG export, validator positive and negative cases) passed.
- 2026-11-10: the prerequisite PR is the batched branch `chore/template-batch-20261110`. Besides this plan and the rule, it moves `docker/` and local scripts into `Scripts/LocalDevelopment`, `Scripts/LocalObservability` and `Scripts/Quality`, documents why Blazor stale-result checks need no `Interlocked`/`Volatile`, and records fork lessons. None of that changes the facts in section 7. It was rebased onto `33d51451` (#459, which simplified the Blazor Books async guards and added the internal helper `BookUserIdentity`; the helper is too small for the package diagrams). Section 7 facts were rechecked after that rebase.
- 2026-11-10: A00 passed (`origin/main` = `fc2aaacd`, #460 carries the rule). A01 branch `docs/architecture-illustrations`. A02 draw.io 26.1.1 and validator probe passed. A03 re-inventory at `fc2aaacd`: no drift (pipeline order, Books files, routes, OpenAPI responses, Caddy load balancing, Compose limits and `server_type` all match section 7). The `Program.cs` grep in A03 failed in this Git Bash because of the `\(` escape; `grep -n -E "app\.|Use[A-Z]|Map[A-Z]" GrumPlates/Program.cs` gives the same information. Sources are generated by small Python scripts in `Plans.local/ArchitectureIllustrations/` (scratch only).

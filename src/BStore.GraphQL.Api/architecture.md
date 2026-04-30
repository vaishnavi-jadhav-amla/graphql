# BStore.GraphQL — Architecture Guide

## Overview

BStore.GraphQL is a Hot Chocolate 15 GraphQL API that exposes B-store management operations from the Znode platform. It wraps the Znode service layer (REST-aligned) and adds a direct EF Core path for high-performance filtered/sorted reads.

---

## Project Structure

```
src/BStore.GraphQL.Api/
│
├── GraphQL/                          # Schema surface
│   ├── Queries/
│   │   ├── BStoreQuery.cs            # Service-backed B-store reads (root query type)
│   │   ├── BStoreDatabaseQuery.cs    # EF Core–backed reads (IQueryable + HC middleware)
│   │   ├── BStoreUserQuery.cs        # B-store user access & role reads
│   │   ├── UserQuery.cs              # General user reads (IUserService)
│   │   └── ProductQuery.cs           # Product catalogue reads (IProductApiClient)
│   ├── Mutations/
│   │   ├── BStoreMutation.cs         # B-store CRUD + file upload (root mutation type)
│   │   ├── BStoreUserMutation.cs     # B-store user access & role writes
│   │   ├── UserMutation.cs           # User profile writes
│   │   └── AuthMutation.cs           # Token validation
│   └── Types/
│       ├── BStoreDbTypes.cs          # Flat EF projection DTOs
│       ├── BStoreUserTypes.cs        # B-store user access DTOs + input types
│       ├── UserTypes.cs              # User DTOs
│       └── ProductTypes.cs           # Product DTOs (DummyJSON)
│
├── Services/                         # External HTTP clients
│   ├── IProductApiClient.cs          # Product API contract
│   └── DummyJsonProductApiClient.cs  # DummyJSON implementation (swap for Znode catalog)
│
├── Caching/                          # Distributed cache abstraction
│   ├── ICacheService.cs              # Interface (swap InMemory ↔ Redis without resolver changes)
│   ├── DistributedCacheService.cs    # IDistributedCache impl with graceful degradation
│   └── CacheKeys.cs                  # Deterministic key builders for all query types
│
├── Common/                           # Shared cross-cutting helpers
│   ├── ErrorMapper.cs                # ZnodeException → GraphQLException with structured extensions
│   └── ErrorCodes.cs                 # Named error code constants (extensions.code in responses)
│
├── Configuration/
│   └── GraphQLOptions.cs             # Strongly-typed options (TTLs, MaxPageSize, Redis flag)
│
├── DataLoaders/
│   └── BStorePortalDataLoader.cs     # BatchDataLoader<int, BStorePortalDbRow?> — N+1 prevention
│
├── Infrastructure/
│   └── BStoreGraphQlZnodeHosting.cs  # Znode EF + service DI registration; static resolver snapshot
│
├── Middleware/
│   └── CorrelationIdMiddleware.cs    # X-Correlation-ID propagation and generation
│
├── Program.cs                        # Host bootstrap, DI wiring, GraphQL server config
├── appsettings.json                  # Runtime configuration
└── architecture.md                   # This file
```

---

## Request Flow

```
HTTP Request
    │
    ▼
CorrelationIdMiddleware          — ensures X-Correlation-ID on every request
    │
    ▼
Hot Chocolate GraphQL Engine
    │
    ├─► Query Resolver (BStoreQuery / BStoreDatabaseQuery)
    │       │
    │       ├─► ICacheService.GetOrSetAsync()   — cache read
    │       │       ├── HIT  → return cached JSON
    │       │       └── MISS → Znode Service / EF Core → cache write → return
    │       │
    │       └─► ErrorMapper.ToGraphQL()         — on ZnodeException
    │
    └─► Mutation Resolver (BStoreMutation)
            │
            ├─► Znode Service call
            ├─► ICacheService.RemoveAsync()     — invalidate stale keys on success
            └─► ErrorMapper.ToGraphQL()         — on ZnodeException
```

---

## Architecture Layers

### 1. GraphQL Layer (`GraphQL/`)

**Extension pattern** — all additional query/mutation groups use `[ExtendObjectType]` so the schema has a single `Query` root and a single `Mutation` root:

```
Query root       = BStoreQuery
  ├── extended by BStoreDatabaseQuery  (EF Core fields)
  ├── extended by BStoreUserQuery      (user access fields)
  ├── extended by UserQuery            (user management fields)
  └── extended by ProductQuery         (product catalogue fields)

Mutation root    = BStoreMutation
  ├── extended by BStoreUserMutation   (user access writes)
  ├── extended by UserMutation         (user profile writes)
  └── extended by AuthMutation         (token validation)
```

**BStoreQuery** — service-backed query root.
- Resolvers are thin: validate args → call cache → call Znode service → return.
- Constructor injects `ILogger`, `ICacheService`, `IOptions<GraphQLOptions>` (all singleton-safe).
- Scoped Znode services (`IBStoresService`, `IBStoresWebStoreService`, `IDomainService`) are injected via `[Service]` method parameters to avoid HC singleton scope issues.

**BStoreDatabaseQuery** — EF Core extension fields (`[ExtendObjectType(typeof(BStoreQuery))]`).
- Returns `IQueryable<T>`; HC middleware translates `where` / `order` / field selection to SQL.
- Uses flat projection DTOs (`BStoreDbTypes.cs`) to avoid exposing navigation graph.
- No caching — results are already composable SQL, caller narrows via `where`.

**BStoreMutation** — write operations.
- Constructor injects `ILogger`, `ICacheService`.
- Every successful write calls `cache.RemoveAsync(CacheKeys.ForBStore(storeId))`.
- `[Authorize]` attributes present as comments — uncomment and configure JWT before production.

---

### 2. Caching Layer (`Caching/`)

| Class | Responsibility |
|---|---|
| `ICacheService` | Contract: `GetOrSetAsync`, `RemoveAsync` |
| `DistributedCacheService` | JSON-serialised `IDistributedCache` wrapper. Cache errors log + degrade gracefully — never throws. |
| `CacheKeys` | Deterministic key builders. `ForBStore(id)` returns the invalidation set for a given portal. |

**Switching to Redis (production):**
1. Add `Microsoft.Extensions.Caching.StackExchangeRedis` NuGet package.
2. Uncomment `AddStackExchangeRedisCache(...)` in `Program.cs`.
3. Set env vars: `GraphQL__UseRedis=true`, `GraphQL__RedisConnectionString=<connection string>`.
4. No resolver code changes needed.

**Cache TTLs** (all configurable via `appsettings.json → "GraphQL"`):

| Query | Default TTL | Key |
|---|---|---|
| BStoreList | 30 s | `bstore:list:{portalId}:{userId}:{page}:{size}` |
| BStore | 60 s | `bstore:portal:{storeId}` |
| BStoreTheme | 60 s | `bstore:theme:{storeId}` |
| BStoreCatalogs | 120 s | `bstore:catalogs:{portalId}:{associated}:{page}:{size}` |
| BStorePriceLists | 120 s | `bstore:pricelists:{portalId}:{associated}:{page}:{size}` |
| BStoreDomainNameSuffix | 120 s | `bstore:domain-suffix:{portalId}` |
| DomainList | 120 s | `bstore:domains:{page}:{size}` |

**Mutation invalidation:**  
`BStoreUpdate`, `BStoreCopy`, `BStoreSetActivation`, `BStoreThemeUpdate` each call  
`cache.RemoveAsync(CacheKeys.ForBStore(storeId))` → evicts `bstore:portal:*` and `bstore:theme:*`.  
`BStoreList` keys expire naturally via TTL (combinatorial key space makes exhaustive invalidation impractical).

---

### 3. Error Handling (`Common/`)

All resolvers convert Znode domain exceptions through a single path:

```
ZnodeException
    └─► ErrorMapper.ToGraphQL(ex)
            └─► ErrorBuilder
                    .SetMessage(ex.Message)
                    .SetCode("BSTORE_ERROR")
                    .SetExtension("source", "ZnodeService")
```

GraphQL error response shape:
```json
{
  "errors": [{
    "message": "...",
    "extensions": {
      "code": "BSTORE_ERROR",
      "source": "ZnodeService"
    }
  }]
}
```

| Code | Meaning |
|---|---|
| `BSTORE_ERROR` | General Znode service / domain error |
| `BSTORE_NOT_FOUND` | Resource does not exist |
| `BSTORE_VALIDATION` | Input failed validation |
| `BSTORE_UPLOAD_ERROR` | File upload failure |
| `BSTORE_NO_HTTP_CONTEXT` | HTTP context missing during upload |

---

### 4. External Services (`Services/`)

`IProductApiClient` / `DummyJsonProductApiClient` wraps the DummyJSON external product API used by `znode10-bstore-web` as a demo catalogue. It is registered as a typed `HttpClient`:

```csharp
builder.Services.AddHttpClient<IProductApiClient, DummyJsonProductApiClient>(c =>
    c.BaseAddress = new Uri(config["DummyJson:BaseUrl"]));
```

**Swapping to a real product service:**  
Implement `IProductApiClient` against the Znode catalog/publish API and change the DI registration — no resolver code changes needed.

---

### 5. DataLoader (`DataLoaders/`)

`BStorePortalDataLoader` is a `BatchDataLoader<int, BStorePortalDbRow?>` that loads portal rows by `PortalId` in a single SQL `WHERE PortalId IN (...)` query. It eliminates N+1 patterns when portal details are needed for multiple B-stores in one GraphQL operation.

**Usage in a resolver:**
```csharp
public async Task<BStorePortalDbRow?> MyField(
    int portalId,
    [Service] BStorePortalDataLoader loader,
    CancellationToken ct)
    => await loader.LoadAsync(portalId, ct);
```

HC dispatches all pending `LoadAsync` calls for a batch round before executing the next resolver level.

---

### 5. Configuration (`Configuration/`)

Bound from `appsettings.json → "GraphQL"`:

| Property | Default | Purpose |
|---|---|---|
| `DefaultCacheExpirySeconds` | 60 | TTL for single-entity reads |
| `ListCacheExpirySeconds` | 30 | TTL for list queries |
| `LookupCacheExpirySeconds` | 120 | TTL for catalog / price list / domain reads |
| `MaxPageSize` | 500 | Hard cap on in-memory pagination |
| `UseRedis` | false | Switch to Redis when `true` |
| `RedisConnectionString` | `""` | Redis connection string (set via env/secrets) |

---

### 6. Infrastructure (`Infrastructure/`)

`BStoreGraphQlZnodeHosting` registers the Znode EF + service stack. **Call order is mandatory:**

```
builder.AddZnodeStackForBStoreGraphQl()   // 1. Znode DI
builder.Services.Add*(...)                // 2. App services (cache, HC, etc.)
builder.FinalizeZnodeStaticResolver()     // 3. Snapshot DI container — MUST BE LAST
```

`FinalizeZnodeStaticResolver` calls `ZnodeDependencyResolver._staticServiceProvider = builder.Services.BuildServiceProvider()`. Any service registered after this point will not be visible to Znode's static resolver.

---

### 7. Middleware (`Middleware/`)

`CorrelationIdMiddleware` runs before all other middleware:
- If `X-Correlation-ID` is absent, generates a UUID and injects it into the request headers.
- Echoes the ID back in response headers.
- Logs generated IDs at `Debug` level via `ILogger<CorrelationIdMiddleware>`.

---

## Schema Overview

### Query Fields

#### B-store (BStoreQuery + BStoreDatabaseQuery)

| Field | Source | Backing Service | REST Equivalent |
|---|---|---|---|
| `bStoreList` | Service | `IBStoresWebStoreService` | GET v2/b-stores/parent-portal/{portalId}/users/{userId}/stores |
| `bStore` | Service | `IBStoresService` | GET v2/b-stores/{storeId} |
| `bStoreTheme` | Service | `IBStoresService` | GET v2/b-stores/{storeId}/theme |
| `bStoreCatalogs` | Service | `IBStoresService` | GET v2/b-stores/parent-portal/{portalId}/catalogs |
| `bStorePriceLists` | Service | `IBStoresService` | GET v2/b-stores/parent-portal/{portalId}/price-list |
| `bStoreDomainNameSuffix` | Service | `IBStoresService` | GET v2/b-stores/parent-portal/{portalId}/domain-name |
| `domainList` | Service | `IDomainService` | GET Domain/List |
| `bStorePortalsFromDatabase` | EF Core | `Znode_Entities` | — |
| `bStoreCatalogAssignmentsFromDatabase` | EF Core | `Znode_Entities` | — |
| `bStorePriceListAssignmentsFromDatabase` | EF Core | `Znode_Entities` | — |
| `domainsFromDatabase` | EF Core | `Znode_Entities` | — |

#### B-store User Access (BStoreUserQuery)

| Field | Backing Service | REST Equivalent |
|---|---|---|
| `bStoreUserRoleAccess` | `IBStoresUserService` | GET BStoresUser/GetUserBStoreRoleAccess/{userId} |
| `bStoreUserAccessList` | `IBStoresUserService` | GET BStoresUser/GetAssociatedUnAssociatedUserAccessBStoresList |

#### User (UserQuery)

| Field | Backing Service | REST Equivalent |
|---|---|---|
| `user` | `IUserService` | GET /users/{userId} |
| `userByUsername` | `IUserService` | GET /users?username=&storeCode= |

#### Products / Catalogue (ProductQuery)

| Field | Backing Service | REST Equivalent |
|---|---|---|
| `productList` | `IProductApiClient` (DummyJSON) | GET /products |
| `product` | `IProductApiClient` (DummyJSON) | GET /products/{id} |
| `productSearch` | `IProductApiClient` (DummyJSON) | GET /products/search?q= |
| `productCategories` | `IProductApiClient` (DummyJSON) | GET /products/categories |
| `productsByCategory` | `IProductApiClient` (DummyJSON) | GET /products/category/{slug} |

---

### Mutation Fields

#### B-store (BStoreMutation)

| Field | Backing Service | REST Equivalent |
|---|---|---|
| `bStoreCreate` | `IBStoresWebStoreService` | POST v2/b-stores/parent-portal/{portalId}/users/{userId}/setup |
| `bStoreCopy` | `IBStoresService` | POST v2/b-stores/{sourcePortalId}/copy |
| `bStoreSetActivation` | `IBStoresWebStoreService` | POST v2/b-stores/{storeId}/users/{userId}/set-activation |
| `bStoreUpdate` | `IBStoresService` | PUT v2/b-stores/{storeId} |
| `bStoreThemeUpdate` | `IBStoresService` | PUT v2/b-stores/{storeId}/theme |
| `bStoreUploadFile` | `IFileUploader` | POST FileUpload/PostAsync |
| `bStoreRemoveUploadedFile` | `IMediaManagerServices` | POST FileUpload/Remove |

#### B-store User Access (BStoreUserMutation)

| Field | Backing Service | REST Equivalent |
|---|---|---|
| `bStoreUserRoleAccessSave` | `IBStoresUserService` | POST BStoresUser/SaveUserBStoreRoleAccess |
| `bStoreUserAccessToggle` | `IBStoresUserService` | POST BStoresUser/AssociateUnAssociateBStoresToUserAccess |

#### User (UserMutation)

| Field | Backing Service | REST Equivalent |
|---|---|---|
| `userUpdate` | `IUserService` | PUT /users/{id} |
| `userToggleActive` | `IUserService` | PATCH /v2/members/{id}/deactivate |

#### Auth (AuthMutation)

| Field | Backing Service | REST Equivalent |
|---|---|---|
| `bStoreValidateToken` | `IAuthService` | POST /v2/b-stores/validate-token |

---

## Adding a New Query

1. Add a public method to `BStoreQuery` (or a new `[ExtendObjectType(typeof(BStoreQuery))]` class for grouping).
2. Inject the required Znode service via `[Service]` on the method parameter.
3. Wrap in `cache.GetOrSetAsync(CacheKeys.MyNewKey(...), ...)`.
4. Add a key builder to `CacheKeys.cs`.
5. Catch `ZnodeException` and rethrow via `ErrorMapper.ToGraphQL(ex)`.

## Adding a New Mutation

1. Add a public method to `BStoreMutation`.
2. After a successful write, call `await cache.RemoveAsync(CacheKeys.ForBStore(storeId), ct)` (or add targeted key(s) to `CacheKeys`).
3. Catch `ZnodeException` and rethrow via `ErrorMapper.ToGraphQL(ex)`.
4. Uncomment `[Authorize]` when JWT is configured.

---

## Production Checklist

- [ ] Enable JWT authentication: configure `Jwt` section in `appsettings.json` and uncomment `AddAuthentication` + `[Authorize]` attributes on all mutations.
- [ ] Switch to Redis: add `Microsoft.Extensions.Caching.StackExchangeRedis`, uncomment `AddStackExchangeRedisCache` in `Program.cs`, set `GraphQL__UseRedis=true`.
- [ ] Disable Banana Cake Pop IDE: already gated to `IsDevelopment()` — no change needed.
- [ ] Enable HC offset paging: upgrade `HotChocolate.*` packages past the 15.0.0 `QueryableOffsetPagingProvider` bug and restore `[UseOffsetPaging]` on EF-backed fields.
- [ ] Review `MaxPageSize` (default 500) and per-TTL values for production load.
- [ ] Ensure `IncludeExceptionDetails = false` in production (already gated to `IsDevelopment()`).

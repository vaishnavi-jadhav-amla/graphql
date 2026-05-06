# Support-staff Debug Playbook (ADR-028)

Use this when a customer reports an empty result, a slow query, or a 5xx from the GraphQL API.

## 1. Capture the correlation id

Every response sets `X-Correlation-ID` and includes `extensions.correlationId`. Ask the customer
to share either header, or open the browser network tab.

```
X-Correlation-ID: 7c3f2a... (32 hex chars)
```

## 2. Pull the request log

Search Splunk / Application Insights for `CorrelationId="7c3f2a..."`. Every log line emitted by
this service includes the correlation id, the operation name, and the user id.

## 3. Re-run the operation with admin debug

Re-issue the failing GraphQL request with the admin token plus `X-Debug-Level: Detailed`:

```
POST /graphql
X-Bstore-Admin-Token: <admin token>
X-Debug-Level: Detailed
```

The response now contains `extensions.timings`, `extensions.dataSources`,
`extensions.pipeline`, `extensions.providers`, and `extensions.emptyResultReasons`.

## 4. If the result is empty

Check `extensions.emptyResultReasons` first. If the diagnoser says `PORTAL_INACTIVE` or
`CATEGORY_NOT_FOUND` you have your answer. Otherwise call `diagnose(operation, argsJson)`:

```graphql
query {
  diagnose(operation: "productList", argsJson: "{\"limit\":24,\"category\":\"shoes\"}") {
    operation
    reasons { code message }
    providers { provider calls errors lastError }
    correlationId
  }
}
```

## 5. If the request is slow

Look at `extensions.timings`: every stage is named after the resolver path
(`product.list.cacheGetOrSet`, `dataLoader.productById`, `catalog.materializedTree.ef`, etc.).
If `cacheGetOrSet` is fast but `ef` dominates, the index in `Documentation/SQL_INDEXES.md`
is missing.

## 6. If a backing system is degraded

`GET /health/providers` (admin only) returns p50/p95 and last error per provider:
`Sql:Znode_Entities`, `Sql:ZnodePublish_Entities`, `Cache:L2-Distributed`,
`AzureCognitiveSearch`, `AMQP:RabbitMQ`. Cross-check with the provider's status page.

## 7. If the service threw

The error envelope (ADR-019) carries `code`, `category`, `correlationId`, `details`,
`operation`, `timestamp`. Map `category` to the runbook:

| category         | Runbook |
| ---------------- | ------- |
| `VALIDATION`     | Reject input fix; no on-call |
| `AUTHORIZATION`  | Customer needs an admin token; no on-call |
| `NOT_FOUND`      | Data drift; check publish has run |
| `DATABASE`       | DBA on-call |
| `PROVIDER`       | Vendor outage; check `/health/providers` |
| `RATE_LIMITED`   | Customer is over quota |
| `TIMEOUT`        | Network / index page; DBA on-call |
| `INTERNAL`       | Engineering on-call |

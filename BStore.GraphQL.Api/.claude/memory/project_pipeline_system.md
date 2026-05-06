---
name: Pipeline System Details
description: Order pipeline uses IPipelineStep with 7 ordered steps (100-700), configurable disable list, and replaceable steps for complex multi-step operations
type: project
---

The Pipeline pattern handles complex multi-step operations like order creation. Steps implement `IPipelineStep<TContext>` with `StepName`, `Order`, and `ExecuteAsync`.

**Order Pipeline Steps (7 steps):**
1. `ValidateCartStep` (Order 100) — Validates cart has items
2. `CalculatePricingStep` (Order 200) — REPLACEABLE "CorePricing" — calculates subtotal
3. `ApplyDiscountsStep` (Order 300) — Applies discount codes
4. `CalculateTaxStep` (Order 400) — REPLACEABLE "CoreTax" — calculates tax
5. `CreateOrderRecordStep` (Order 500) — Persists order to DB
6. `ProcessPaymentStep` (Order 600) — REPLACEABLE "CorePayment" — processes payment
7. `SendConfirmationStep` (Order 700) — Sends confirmation email

**Replaceable** means clients can swap the core step with their own implementation (e.g., external tax provider replaces CoreTax).

**Why:** Order creation has 7 distinct phases. Clients need to inject custom logic at specific points (e.g., their own tax calculator at step 400) without touching other steps.

**How to apply:** New pipeline steps use `Order` multiples of 100. Steps can be disabled via `appsettings.json` → `GraphQL:Pipeline:DisabledSteps`. `PipelineExecutor` discovers steps from DI, filters disabled, sorts by Order, and logs execution time.

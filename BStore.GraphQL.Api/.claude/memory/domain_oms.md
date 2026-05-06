---
name: OMS Domain — Data & Workflow Reference
description: Tables, columns, business rules, state machines, and workflows for Cart, Orders, Quotes, Shipping, Coupons. DATA ONLY — coding patterns live in .claude/rules/. Never copy v1/v2 code style.
type: project
---

> **Scope of this file:** What data exists, how it relates, and what the business rules are.
> How to write EF Core queries, services, or resolvers is in `.claude/rules/`.

---

## Cart

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodeOmsSavedCart` | OmsSavedCartId, UserId, PortalId, OmsCookieMappingId, CartNumber | Cart header |
| `ZnodeOmsSavedCartLineItem` | OmsSavedCartId, SKU, Quantity, UnitPrice, ProductName, AddedDate | Cart line items |
| `ZnodeOmsCookieMapping` | OmsCookieMappingId, AnonymousId, UserId | Tracks guest sessions |
| `ZnodeOmsSavedCartCoupon` | OmsSavedCartId, CouponCode | Applied coupons |

### Guest vs Registered Cart

| User type | Cart identified by |
|---|---|
| Guest (not logged in) | `OmsCookieMappingId` — anonymous session token from cookie |
| Registered user | `UserId` — linked to account |

Both cases use `PortalId` filter — a cart belongs to one store.

### Cart Merge Workflow (On Login)

When a guest logs in and has an existing guest cart:
```
1. Guest has cart via OmsCookieMappingId (anonymous)
2. User logs in → system knows UserId
3. Merge: guest cart line items move to user's existing cart (or become the user's cart)
4. OmsCookieMappingId.UserId updated to link to the now-authenticated user
5. Duplicate SKUs: add quantities together (not create two line items)
```

### Add-to-Cart Business Rules

These must all be checked before adding an item:
- `MinimumQuantity` from product — reject if quantity below minimum
- `MaximumQuantity` from product — reject if quantity above maximum
- `OutOfStockOptions`:
  - `AllowBackOrdering` → allow add
  - `DisableAddToCart` → reject, return error
  - `HideProductOnFrontEnd` → item should not be reachable at all
- Portal country whitelist must be verified before shipping address is set (but not at add-to-cart)

### Cart Total Fields

```
SubTotal         = SUM(lineItem.Quantity × lineItem.UnitPrice)
DiscountAmount   = coupon/promo discount total
TaxAmount        = calculated by provider or ZnodeTaxRule lookup
ShippingCost     = from selected shipping method
OrderTotal       = SubTotal - DiscountAmount + TaxAmount + ShippingCost
```

---

## Orders

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodeOmsOrder` | OmsOrderId, OrderNumber, AccountId, UserId, PortalId, OrderDate, IsQuoteOrder | Order header |
| `ZnodeOmsOrderDetail` | OmsOrderDetailId, OmsOrderId, SubTotal, ShippingCost, TaxCost, Total, DiscountAmount | Financial summary |
| `ZnodeOmsOrderLineItem` | OmsOrderLineItemId, OmsOrderId, SKU, ProductName, Quantity, UnitPrice, ExtendedPrice | Line items |
| `ZnodeOmsOrderShipment` | OmsOrderShipmentId, OmsOrderId, TrackingNumber, CarrierName, ShippedDate | Shipment info |
| `ZnodeOrderPayment` | OmsOrderId, PaymentType, Amount, TransactionId, PaymentStatus | Payment record |
| `ZnodeOmsTaxOrderDetail` | OmsOrderId, TaxAmount, TaxRate | Order-level tax |
| `ZnodeOmsTaxOrderLineDetail` | OmsOrderLineItemId, TaxAmount, TaxRate | Line-item tax |
| `ZnodeOmsOrderDiscount` | OmsOrderId, DiscountCode, DiscountType, DiscountAmount | Applied discounts |
| `ZnodeOmsHistory` | OmsOrderId, ChangedBy, ChangedDate, OldStatus, NewStatus | State change audit |
| `ZnodeOmsNote` | OmsOrderId, Note, CreatedBy, CreatedDate | Admin notes |
| `ZnodeOmsDownloadableProductKey` | OmsOrderLineItemId, ProductKey | Digital product license keys |

`OrderNumber` is the human-readable reference (e.g., `"ORD-2024-000123"`). `OmsOrderId` is the internal DB key. Customer-facing lookups use `OrderNumber`.

### Order State Machine

```
INPROGRESS  →  PENDING
PENDING     →  PROCESSING  |  CANCELLED
PROCESSING  →  SHIPPED     |  CANCELLED
SHIPPED     →  DELIVERED
DELIVERED   →  [SEALED — no transitions]
CANCELLED   →  [SEALED — no transitions]
```

Attempting to transition from a sealed state (`DELIVERED`, `CANCELLED`) is a business rule violation.

Returned orders go through RMA (`ZnodeRmaReturnDetail`), not a direct status transition from `DELIVERED`.

### Storefront vs Admin Visibility

| Who | What they can see |
|---|---|
| Storefront customer | Only their own orders — `UserId` must match JWT claim |
| Admin (portal-scoped) | All orders for a portal — `PortalId` filter only |
| Admin (platform-level) | All orders across all portals |

### Payment Status Values

| Status | Meaning |
|---|---|
| `AUTHORIZED` | Payment held/reserved, not yet captured |
| `CAPTURED` | Payment completed, funds collected |
| `DECLINED` | Rejected by gateway |
| `REFUNDED` | Funds returned to customer |
| `VOIDED` | Auth cancelled before capture |
| `PENDING` | Awaiting gateway confirmation |
| `FAILED` | Technical failure |

### Order Creation Workflow (PlaceOrder Pipeline)

```
Step 100 — ValidateCart
  Check cart exists, is not empty, quantities within min/max limits
  ↓
Step 200 — CalculatePricing
  Apply price lists (user → account → profile → default order)
  ↓
Step 300 — ApplyDiscounts
  Validate and apply coupons/promotions
  ↓
Step 400 — CalculateTax
  Call Tax provider or look up ZnodeTaxRule by destination state/zip
  ↓
Step 500 — CreateOrderRecord [CRITICAL]
  Write ZnodeOmsOrder + ZnodeOmsOrderDetail + ZnodeOmsOrderLineItem
  Clear ZnodeOmsSavedCartLineItem
  ↓
Step 600 — ProcessPayment [CRITICAL]
  Call payment gateway
  Write ZnodeOrderPayment with TransactionId and PaymentStatus
  ↓
Step 700 — SendConfirmation
  Email customer — fire-and-forget (non-critical)
  Issue digital product keys in ZnodeOmsDownloadableProductKey if applicable
```

Steps marked CRITICAL halt the pipeline on failure. Step 700 failure does not roll back the order.

### Tax Calculation

Tax is stored at two levels — use line-item level for itemized receipts:
- Order-level: `ZnodeOmsTaxOrderDetail.TaxAmount` (total)
- Line-item level: `ZnodeOmsTaxOrderLineDetail.TaxAmount` per item

Tax source: external Tax provider when `ProviderRegistry["Tax"]` is enabled. Otherwise: look up `ZnodeTaxRule` by state code + postal code range.

---

## Quotes (B2B Only)

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodeOmsQuote` | OmsQuoteId, AccountId, PortalId, QuoteNumber, Status, ExpiryDate | Quote header |
| `ZnodeOmsQuoteLineItem` | OmsQuoteId, SKU, Quantity, UnitPrice, NegotiatedPrice | Quote lines |
| `ZnodeOMSQuoteApproval` | OmsQuoteId, ApproverId, ApprovalStatus, ApprovalLevel | Approval chain |
| `ZnodeOmsQuoteComment` | OmsQuoteId, Comment, CreatedBy, CreatedDate | Comments |

### Quote → Order Conversion Workflow

```
B2B user creates quote from cart
  ↓
Quote sent for approval (ZnodeOMSQuoteApproval)
  ↓
Each approver at each ApprovalLevel approves/rejects
  ↓
Final approval → Quote status = "Approved"
  ↓
User converts quote to order → creates ZnodeOmsOrder with IsQuoteOrder = true
  ↓
NegotiatedPrice per line item overrides standard pricing
```

`ExpiryDate` must be checked before allowing conversion. Expired quotes cannot be ordered.

### Approval Level Rules

- `ApprovalLevel` is hierarchical — Level 1 approves first, then Level 2, etc.
- If any level rejects, quote is rejected and cannot be converted
- Order approval workflow (not just quote) also uses `ZnodeApprovalLevel` for B2B accounts with spending limits

---

## Shipping

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodePortalShipping` | PortalId, ShippingId, IsActive, DisplayOrder | Portal → shipping method |
| `ZnodeShipping` | ShippingId, ShippingName, ShippingCode, CarrierName | Shipping method |
| `ZnodeShippingRate` | ShippingId, MinWeight, MaxWeight, Rate | Fallback rate table |
| `ZnodePortalCountry` | PortalId, CountryCode | Allowed shipping countries |

### Shipping Resolution

```
Client requests shipping options for cart
  ↓
Filter ZnodePortalShipping WHERE PortalId = @id AND IsActive = true
  ↓
For each method:
  If ProviderRegistry["Shipping"] is enabled:
    → call external carrier API with cart weight + destination
  Else:
    → look up ZnodeShippingRate by weight range
  ↓
Return options ordered by DisplayOrder
```

Before saving shipping address: validate destination country is in `ZnodePortalCountry` for this portal.

---

## Coupons & Promotions

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodePromotion` | PromotionId, PromotionType, DiscountType, DiscountValue, StartDate, EndDate | Promotion rules |
| `ZnodeVoucher` | VoucherId, VoucherCode, MaxUsage, CurrentUsage, DiscountValue | Voucher codes |
| `ZnodeCoupon` | CouponId, CouponCode, DiscountAmount, DiscountType, ExpiryDate | Coupon codes |
| `ZnodeOmsSavedCartCoupon` | OmsSavedCartId, CouponCode | Applied to cart |
| `ZnodeOmsOrderDiscount` | OmsOrderId, DiscountCode, DiscountType, DiscountAmount | Applied to order |

### Discount Types

| Code | Meaning |
|---|---|
| `CSRDISCOUNT` | Manual discount applied by customer service rep |
| `VOUCHERNUMBER` | Gift voucher code redemption |
| `COUPONCODE` | Checkout coupon code |
| `PROMOCODE` | Promotional code (auto-applied or entered) |

### Coupon Validation Rules

Before applying a coupon:
1. Code exists in `ZnodeCoupon` or `ZnodeVoucher`
2. Not expired (`ExpiryDate >= today`)
3. Usage count < `MaxUsage` (if MaxUsage is set)
4. Applies to the items in the cart (product/category restrictions may exist)
5. External Coupons provider (`ProviderRegistry["Coupons"]`) validates when enabled

Typically one coupon per order unless portal config allows stacking.

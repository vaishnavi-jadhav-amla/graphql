namespace BStore.GraphQL.Api.Pipeline.Order;

/// <summary>
/// Context flowing through the order creation pipeline.
/// Each step enriches this context with its results.
/// </summary>
public sealed class OrderPipelineContext
{
    /// <summary>The input from the GraphQL mutation.</summary>
    public required CreateOrderInput Input { get; init; }

    /// <summary>The portal/store id for this order.</summary>
    public int PortalId { get; set; }

    /// <summary>The authenticated user id.</summary>
    public int UserId { get; set; }

    /// <summary>Subtotal before discounts and tax.</summary>
    public decimal SubTotal { get; set; }

    /// <summary>Total discount amount.</summary>
    public decimal DiscountTotal { get; set; }

    /// <summary>Total tax amount.</summary>
    public decimal TaxTotal { get; set; }

    /// <summary>Final total (sub - discount + tax + shipping).</summary>
    public decimal GrandTotal { get; set; }

    /// <summary>Shipping total.</summary>
    public decimal ShippingTotal { get; set; }

    /// <summary>Whether validation passed.</summary>
    public bool IsValid { get; set; } = true;

    /// <summary>Validation error messages (populated by ValidateCartStep).</summary>
    public List<string> ValidationErrors { get; set; } = [];

    /// <summary>The created order id (populated by CreateOrderRecordStep).</summary>
    public int? CreatedOrderId { get; set; }

    /// <summary>Whether payment was processed successfully.</summary>
    public bool PaymentProcessed { get; set; }

    /// <summary>Payment transaction id.</summary>
    public string? PaymentTransactionId { get; set; }
}

/// <summary>Input for the order creation mutation.</summary>
public sealed class CreateOrderInput
{
    public int CartId { get; set; }
    public int ShippingAddressId { get; set; }
    public int BillingAddressId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentToken { get; set; }
    public string? CouponCode { get; set; }
    public string? Notes { get; set; }
}

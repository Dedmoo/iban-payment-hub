namespace IbanPaymentHub.Api.Domain;

public enum PaymentRail
{
    Fast = 0,
    Eft = 1
}

public enum PaymentStatus
{
    Pending = 0,
    Processing = 1,
    Settled = 2,
    Failed = 3
}

public sealed class PaymentOrder
{
    public required string PaymentId { get; init; }
    public required string DebtorIban { get; init; }
    public required string CreditorIban { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required PaymentRail Rail { get; init; }
    public required string Reference { get; init; }
    public bool SimulateFailure { get; init; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SettledAt { get; set; }
}

public sealed record CreatePaymentRequest(
    string DebtorIban,
    string CreditorIban,
    decimal Amount,
    string Currency,
    string Rail,
    string Reference,
    bool SimulateFailure = false);

public sealed record ValidateIbanRequest(string Iban);

public sealed record ReconciliationReport(
    int Total,
    int Settled,
    int Failed,
    int PendingOrProcessing,
    decimal SettledAmount);

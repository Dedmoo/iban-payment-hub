namespace IbanPaymentHub.Api.Domain;

public sealed class PaymentHubService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PaymentOrder> _payments = new(StringComparer.Ordinal);

    public object ValidateIban(string iban)
    {
        var normalized = IbanValidator.Normalize(iban);
        var valid = IbanValidator.IsValid(iban);
        return new
        {
            iban = normalized,
            valid,
            country = normalized.Length >= 2 ? normalized[..2] : null
        };
    }

    public PaymentOrder CreatePayment(CreatePaymentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Amount <= 0)
            throw new InvalidOperationException("Amount must be greater than zero.");
        if (!IbanValidator.IsValid(request.DebtorIban))
            throw new InvalidOperationException("Debtor IBAN is invalid.");
        if (!IbanValidator.IsValid(request.CreditorIban))
            throw new InvalidOperationException("Creditor IBAN is invalid.");
        if (string.Equals(
                IbanValidator.Normalize(request.DebtorIban),
                IbanValidator.Normalize(request.CreditorIban),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Debtor and creditor IBAN must differ.");
        }

        if (!Enum.TryParse<PaymentRail>(request.Rail, ignoreCase: true, out var rail))
            throw new InvalidOperationException("Rail must be Fast or Eft.");

        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? "TRY"
            : request.Currency.Trim().ToUpperInvariant();

        if (rail == PaymentRail.Fast && request.Amount > 100_000m)
            throw new InvalidOperationException("FAST rail demo limit is 100000.");

        var order = new PaymentOrder
        {
            PaymentId = $"PAY-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            DebtorIban = IbanValidator.Normalize(request.DebtorIban),
            CreditorIban = IbanValidator.Normalize(request.CreditorIban),
            Amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            Currency = currency,
            Rail = rail,
            Reference = string.IsNullOrWhiteSpace(request.Reference) ? "N/A" : request.Reference.Trim(),
            SimulateFailure = request.SimulateFailure,
            Status = PaymentStatus.Pending
        };

        lock (_gate)
        {
            _payments[order.PaymentId] = order;
        }

        return order;
    }

    public PaymentOrder Process(string paymentId)
    {
        lock (_gate)
        {
            var order = GetRequired(paymentId);
            if (order.Status is PaymentStatus.Settled or PaymentStatus.Failed)
                return order;

            order.Status = PaymentStatus.Processing;

            if (order.SimulateFailure)
            {
                order.Status = PaymentStatus.Failed;
                order.FailureReason = "Downstream rail rejected the payment (simulated).";
            }
            else
            {
                order.Status = PaymentStatus.Settled;
                order.SettledAt = DateTimeOffset.UtcNow;
            }

            return order;
        }
    }

    public PaymentOrder? Get(string paymentId)
    {
        lock (_gate)
        {
            return _payments.TryGetValue(paymentId, out var order) ? order : null;
        }
    }

    public IReadOnlyList<PaymentOrder> List()
    {
        lock (_gate)
        {
            return _payments.Values.OrderByDescending(p => p.CreatedAt).ToList();
        }
    }

    public ReconciliationReport Reconcile()
    {
        lock (_gate)
        {
            var all = _payments.Values.ToList();
            return new ReconciliationReport(
                Total: all.Count,
                Settled: all.Count(p => p.Status == PaymentStatus.Settled),
                Failed: all.Count(p => p.Status == PaymentStatus.Failed),
                PendingOrProcessing: all.Count(p =>
                    p.Status is PaymentStatus.Pending or PaymentStatus.Processing),
                SettledAmount: all.Where(p => p.Status == PaymentStatus.Settled).Sum(p => p.Amount));
        }
    }

    private PaymentOrder GetRequired(string paymentId)
    {
        if (!_payments.TryGetValue(paymentId, out var order))
            throw new KeyNotFoundException($"Payment not found: {paymentId}");
        return order;
    }
}

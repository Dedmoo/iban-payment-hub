using IbanPaymentHub.Api.Domain;

namespace IbanPaymentHub.Tests;

public class PaymentHubTests
{
    // Official valid sample IBAN (Germany)
    private const string ValidDebtor = "DE89370400440532013000";
    private const string ValidCreditor = "GB82WEST12345698765432";

    [Theory]
    [InlineData("DE89 3704 0044 0532 0130 00", true)]
    [InlineData("GB82WEST12345698765432", true)]
    [InlineData("DE89370400440532013001", false)]
    [InlineData("TR", false)]
    public void IbanValidator_ChecksMod97(string iban, bool expected)
    {
        Assert.Equal(expected, IbanValidator.IsValid(iban));
    }

    [Fact]
    public void CreateAndProcess_SettlesPayment()
    {
        var hub = new PaymentHubService();
        var created = hub.CreatePayment(new CreatePaymentRequest(
            ValidDebtor,
            ValidCreditor,
            150.75m,
            "TRY",
            "Fast",
            "INV-100"));

        Assert.Equal(PaymentStatus.Pending, created.Status);
        var processed = hub.Process(created.PaymentId);
        Assert.Equal(PaymentStatus.Settled, processed.Status);
        Assert.NotNull(processed.SettledAt);
    }

    [Fact]
    public void Process_WithFailReference_MarksFailed()
    {
        var hub = new PaymentHubService();
        var created = hub.CreatePayment(new CreatePaymentRequest(
            ValidDebtor,
            ValidCreditor,
            10m,
            "TRY",
            "Eft",
            "FORCE-FAIL-CASE"));

        var processed = hub.Process(created.PaymentId);
        Assert.Equal(PaymentStatus.Failed, processed.Status);
        Assert.False(string.IsNullOrWhiteSpace(processed.FailureReason));
    }

    [Fact]
    public void CreatePayment_RejectsInvalidIban()
    {
        var hub = new PaymentHubService();
        Assert.Throws<InvalidOperationException>(() =>
            hub.CreatePayment(new CreatePaymentRequest(
                "DE00INVALID",
                ValidCreditor,
                10m,
                "TRY",
                "Fast",
                "X")));
    }

    [Fact]
    public void Reconcile_CountsStatuses()
    {
        var hub = new PaymentHubService();
        var ok = hub.CreatePayment(new CreatePaymentRequest(
            ValidDebtor, ValidCreditor, 20m, "TRY", "Fast", "OK"));
        var bad = hub.CreatePayment(new CreatePaymentRequest(
            ValidDebtor, ValidCreditor, 30m, "TRY", "Eft", "FAIL-ME"));
        hub.Process(ok.PaymentId);
        hub.Process(bad.PaymentId);

        var report = hub.Reconcile();
        Assert.Equal(2, report.Total);
        Assert.Equal(1, report.Settled);
        Assert.Equal(1, report.Failed);
        Assert.Equal(20m, report.SettledAmount);
    }
}

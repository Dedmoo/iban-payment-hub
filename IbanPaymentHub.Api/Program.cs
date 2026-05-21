using IbanPaymentHub.Api.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSingleton<PaymentHubService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Redirect("/openapi/v1.json"));

app.MapPost("/api/iban/validate", (ValidateIbanRequest request, PaymentHubService hub) =>
    Results.Ok(hub.ValidateIban(request.Iban)));

app.MapPost("/api/payments", (CreatePaymentRequest request, PaymentHubService hub) =>
{
    try
    {
        var payment = hub.CreatePayment(request);
        return Results.Created($"/api/payments/{payment.PaymentId}", payment);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/payments/{paymentId}/process", (string paymentId, PaymentHubService hub) =>
{
    try
    {
        return Results.Ok(hub.Process(paymentId));
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
});

app.MapGet("/api/payments/{paymentId}", (string paymentId, PaymentHubService hub) =>
{
    var payment = hub.Get(paymentId);
    return payment is null ? Results.NotFound() : Results.Ok(payment);
});

app.MapGet("/api/payments", (PaymentHubService hub) => Results.Ok(hub.List()));

app.MapGet("/api/reconciliation", (PaymentHubService hub) => Results.Ok(hub.Reconcile()));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "iban-payment-hub" }));

app.Run();

public partial class Program;

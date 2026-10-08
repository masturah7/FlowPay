using FlowPay.BuildingBlocks;
using FlowPay.Transfers;
using FlowPay.Transfers.Clients;
using FlowPay.Transfers.Data;
using FlowPay.Transfers.Features.Beneficiaries;
using FlowPay.Transfers.Features.Transfers;
using Microsoft.EntityFrameworkCore;

const string ServiceName = "FlowPay.Transfers";

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowPaySerilog(ServiceName);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing connection string 'Default'.");

builder.Services.AddControllers().AddFlowPayJsonDefaults();
builder.Services.AddOpenApi();
builder.Services.AddFlowPayApiVersioning();
builder.Services.AddFlowPayProblemDetails();
builder.Services.AddFlowPayJwtBearer(builder.Configuration);
builder.Services.AddFlowPayInternalApiKey(builder.Configuration);

var transferPolicyOptions = builder.Configuration.GetSection(TransferPolicyOptions.SectionName).Get<TransferPolicyOptions>()
    ?? throw new InvalidOperationException($"Missing configuration section '{TransferPolicyOptions.SectionName}'.");
builder.Services.AddSingleton(transferPolicyOptions);

builder.Services.AddDbContext<TransfersDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<ITransferRepository, TransferRepository>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<IBeneficiaryRepository, BeneficiaryRepository>();
builder.Services.AddScoped<IBeneficiaryService, BeneficiaryService>();

var walletBaseUrl = builder.Configuration["Services:Wallet"]
    ?? throw new InvalidOperationException("Missing configuration 'Services:Wallet'.");
var ledgerBaseUrl = builder.Configuration["Services:Ledger"]
    ?? throw new InvalidOperationException("Missing configuration 'Services:Ledger'.");
var notificationsBaseUrl = builder.Configuration["Services:Notifications"]
    ?? throw new InvalidOperationException("Missing configuration 'Services:Notifications'.");

builder.Services.AddHttpClient<IWalletApiClient, WalletApiClient>(client =>
{
    client.BaseAddress = new Uri(walletBaseUrl);
});
builder.Services.AddHttpClient<ILedgerApiClient, LedgerApiClient>(client =>
{
    client.BaseAddress = new Uri(ledgerBaseUrl);
});
builder.Services.AddHttpClient<INotificationApiClient, NotificationApiClient>(client =>
{
    client.BaseAddress = new Uri(notificationsBaseUrl);
});

builder.Services.AddFlowPayHealthChecks()
    .AddNpgSql(connectionString, name: "postgres", tags: new[] { FlowPayPlatform.ReadyTag });

var app = builder.Build();

// Scaffold-stage convenience: apply migrations on startup so `docker compose
// up` gives a working schema with no manual step. Revisit for multi-replica
// production rollouts (migrate-then-deploy as a separate step) once this
// service actually runs more than one instance.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TransfersDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseFlowPayPlatform();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

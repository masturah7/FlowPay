using FlowPay.BuildingBlocks;
using FlowPay.Ledger.Data;
using FlowPay.Ledger.Features.Ledger;
using Microsoft.EntityFrameworkCore;

const string ServiceName = "FlowPay.Ledger";

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowPaySerilog(ServiceName);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing connection string 'Default'.");

builder.Services.AddControllers().AddFlowPayJsonDefaults();
builder.Services.AddOpenApi();
builder.Services.AddFlowPayApiVersioning();
builder.Services.AddFlowPayProblemDetails();
builder.Services.AddFlowPayInternalApiKey(builder.Configuration);

builder.Services.AddDbContext<LedgerDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<ILedgerRepository, LedgerRepository>();
builder.Services.AddScoped<ILedgerService, LedgerService>();

builder.Services.AddFlowPayHealthChecks()
    .AddNpgSql(connectionString, name: "postgres", tags: new[] { FlowPayPlatform.ReadyTag });

var app = builder.Build();

// Scaffold-stage convenience: apply migrations on startup so `docker compose
// up` gives a working schema with no manual step. Revisit for multi-replica
// production rollouts (migrate-then-deploy as a separate step) once this
// service actually runs more than one instance.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseFlowPayPlatform();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

using FlowPay.BuildingBlocks;
using FlowPay.Identity.Data;
using FlowPay.Identity.Domain;
using FlowPay.Identity.Features.Accounts;
using FlowPay.Identity.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

const string ServiceName = "FlowPay.Identity";

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowPaySerilog(ServiceName);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing connection string 'Default'.");

builder.Services.AddControllers().AddFlowPayJsonDefaults();
builder.Services.AddOpenApi();
builder.Services.AddFlowPayApiVersioning();
builder.Services.AddFlowPayProblemDetails();
builder.Services.AddFlowPayJwtBearer(builder.Configuration);

builder.Services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IPasswordHasher<Account>, PasswordHasher<Account>>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddFlowPayHealthChecks()
    .AddNpgSql(connectionString, name: "postgres", tags: new[] { FlowPayPlatform.ReadyTag });

var app = builder.Build();

// Scaffold-stage convenience: apply migrations on startup so `docker compose
// up` gives a working schema with no manual step. Revisit for multi-replica
// production rollouts (migrate-then-deploy as a separate step) once this
// service actually runs more than one instance.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
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

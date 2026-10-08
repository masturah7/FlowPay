using FlowPay.BuildingBlocks;

const string ServiceName = "FlowPay.Notifications";

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowPaySerilog(ServiceName);

builder.Services.AddControllers().AddFlowPayJsonDefaults();
builder.Services.AddOpenApi();
builder.Services.AddFlowPayApiVersioning();
builder.Services.AddFlowPayProblemDetails();
builder.Services.AddFlowPayHealthChecks();

var app = builder.Build();

app.UseFlowPayPlatform();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();

using FlowPay.BuildingBlocks;

const string ServiceName = "FlowPay.Gateway";

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowPaySerilog(ServiceName);

builder.Services.AddFlowPayProblemDetails();
builder.Services.AddFlowPayHealthChecks();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseFlowPayPlatform();

app.UseHttpsRedirection();

app.MapReverseProxy();

app.Run();

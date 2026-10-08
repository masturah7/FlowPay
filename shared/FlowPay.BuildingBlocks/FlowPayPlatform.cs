using System.Text;
using System.Text.Json.Serialization;
using Asp.Versioning;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace FlowPay.BuildingBlocks;

/// <summary>
/// Cross-cutting wiring shared by every FlowPay service: structured logging,
/// correlation ids, health checks (liveness vs. readiness), API versioning,
/// and ProblemDetails error responses. Each service calls these from
/// Program.cs instead of re-wiring the same concerns independently.
/// </summary>
public static class FlowPayPlatform
{
    /// <summary>Readiness checks (e.g. database connectivity) should use this tag.</summary>
    public const string ReadyTag = "ready";

    public static WebApplicationBuilder AddFlowPaySerilog(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console());

        return builder;
    }

    /// <summary>
    /// Enums serialize as their readable name (e.g. "PendingVerification"),
    /// never the underlying number — a bare int in a public API response is
    /// meaningless to callers and silently renumbers if cases are reordered.
    /// </summary>
    public static IMvcBuilder AddFlowPayJsonDefaults(this IMvcBuilder builder)
    {
        builder.AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        return builder;
    }

    /// <summary>
    /// Registers JWT bearer authentication (+ authorization services) using
    /// the "Jwt" configuration section. Any service that needs to validate
    /// FlowPay-issued tokens — not just the one that issues them — calls this
    /// so the validation rules (issuer/audience/signing key/lifetime) stay
    /// identical everywhere. Pair with `app.UseAuthentication()` before
    /// `app.UseAuthorization()` in Program.cs.
    /// </summary>
    public static IServiceCollection AddFlowPayJwtBearer(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException($"Missing configuration section '{JwtOptions.SectionName}'.");

        services.AddSingleton(jwtOptions);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Without this, the handler silently remaps short claim
                // names ("sub") to long legacy XML-schema URIs, so code
                // reading claims by their original issued name finds nothing.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Registers InternalApiKeyOptions from the "InternalApi" configuration
    /// section, for services that expose or call internal-only,
    /// service-to-service endpoints (see RequireInternalApiKeyAttribute).
    /// </summary>
    public static IServiceCollection AddFlowPayInternalApiKey(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(InternalApiKeyOptions.SectionName).Get<InternalApiKeyOptions>()
            ?? throw new InvalidOperationException($"Missing configuration section '{InternalApiKeyOptions.SectionName}'.");

        services.AddSingleton(options);

        return services;
    }

    public static IHealthChecksBuilder AddFlowPayHealthChecks(this IServiceCollection services)
    {
        return services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy());
    }

    public static IServiceCollection AddFlowPayApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        return services;
    }

    public static IServiceCollection AddFlowPayProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                if (context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var correlationId))
                {
                    context.ProblemDetails.Extensions["correlationId"] = correlationId;
                }
            };
        });

        return services;
    }

    /// <summary>
    /// Wires correlation id propagation, request logging, the global
    /// exception handler, and the /health/live + /health/ready endpoints.
    /// Call after routing/auth middleware is registered, before MapControllers.
    /// </summary>
    public static WebApplication UseFlowPayPlatform(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging();

        // Liveness: is the process up at all? No dependency checks — must stay
        // cheap and fast so orchestrators don't restart a healthy-but-busy pod.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        });

        // Readiness: can this instance actually serve traffic right now
        // (database reachable, etc.)? Only checks tagged `ReadyTag` run here.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        });

        return app;
    }
}

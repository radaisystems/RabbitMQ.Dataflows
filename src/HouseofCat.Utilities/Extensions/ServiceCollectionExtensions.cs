﻿using HouseofCat.Utilities.Errors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace HouseofCat.Utilities.Extensions;

public static class ServiceCollectionExtensions
{
    public static string DeploymentEnvironmentKey { get; set; } = "deployment.environment";

    public static void AddOpenTelemetryExporter(
        this IServiceCollection services,
        IConfiguration config)
    {
        var assembly = Assembly.GetEntryAssembly();
        var sourceName = assembly.GetName().Name;
        var sourceVersion = assembly.GetSemanticVersion();

        bool.TryParse(config["OpenTelemetry:Enabled"] ?? "false", out var enabled);
        if (!enabled) return;

        var environment = config["OpenTelemetry:Environment"] ?? "Dev";
        var otlpServiceName = config["OpenTelemetry:ServiceName"] ?? sourceName;
        var otlpServiceNamespace = config["OpenTelemetry:ServiceNamespace"] ?? "hoc";
        var otlpServiceVersion = config["OpenTelemetry:ServiceVersion"] ?? sourceVersion;

        var otlpEndpoint = config["OpenTelemetry:EndpointUrl"];

        var otlpHeaderFormat = config["OpenTelemetry:HeaderFormat"] ?? "{0}={1}";
        var otlpHeaderKey = config["OpenTelemetry:HeaderKey"];
        var otlpApiKey = config["OpenTelemetry:ApiKey"];

        Guard.AgainstNull(otlpEndpoint, nameof(otlpEndpoint));

        var otlpBuilder = services.AddOpenTelemetry()
            .ConfigureResource(
                resource => resource.AddService(
                    serviceName: otlpServiceName,
                    serviceNamespace: otlpServiceNamespace,
                    serviceVersion: otlpServiceVersion)
                .AddAttributes(
                    new[]
                    {
                        new KeyValuePair<string, object>(DeploymentEnvironmentKey, environment)
                    }));

        otlpBuilder
            .WithTracing(
                tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(
                    otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(otlpEndpoint);
                        otlpOptions.Protocol = OtlpExportProtocol.Grpc;
                        otlpOptions.Headers = string.Format(otlpHeaderFormat, otlpHeaderKey, otlpApiKey);
                    })
#if DEBUG
                .AddSource(otlpServiceName)
                .AddConsoleExporter());
#else
                .AddSource(otlpServiceName));
#endif
    }
}

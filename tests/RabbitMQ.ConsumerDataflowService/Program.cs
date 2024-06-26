﻿using HouseofCat.RabbitMQ;
using HouseofCat.RabbitMQ.Services;
using HouseofCat.Utilities.Extensions;
using HouseofCat.Utilities.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.ConsumerDataflowService;
using System.Text;

var loggerFactory = LogHelpers.CreateConsoleLoggerFactory(LogLevel.Information);
LogHelpers.LoggerFactory = loggerFactory;
var logger = loggerFactory.CreateLogger<Program>();
var logMessage = true;

var builder = WebApplication.CreateBuilder(args);
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .Build();

builder.Services.AddOpenTelemetryExporter(configuration);

using var app = builder.Build();

var rabbitService = await Shared.SetupRabbitServiceAsync(loggerFactory, "RabbitMQ.ConsumerDataflows.json");
var dataflowService = new ConsumerDataflowService<CustomWorkState>(
    rabbitService,
    "TestConsumer");

dataflowService.AddDefaultFinalization();
dataflowService.AddDefaultErrorHandling();

// Set the CreateSendMessage step. This builds and assigns the WorkState.SendMessage property.
dataflowService.Dataflow.WithCreateSendMessage(
    (state) =>
    {
        if (string.IsNullOrEmpty(dataflowService.Options.SendQueueName)) return state;

        var message = new Message
        {
            Exchange = "",
            RoutingKey = dataflowService.Options.SendQueueName,
            Body = Encoding.UTF8.GetBytes("New Secret Message"),
            Metadata = new Metadata
            {
                PayloadId = Guid.NewGuid().ToString(),
            },
            ParentSpanContext = state.WorkflowSpan?.Context,
        };

        state.SendMessage = message;
        return state;
    });

// Add custom step to Dataflow using Service helper methods.
dataflowService.AddStep(
    "write_message_to_log",
    (state) =>
    {
        string message;
        if (state.ReceivedMessage.Message is null)
        { message = Encoding.UTF8.GetString(state.ReceivedMessage.Body.Span); }
        else
        { message = Encoding.UTF8.GetString(state.ReceivedMessage.Message.Body.Span); }
        if (message == "throw")
        {
            throw new Exception("Throwing an exception!");
        }

        if (logMessage)
        { logger.LogInformation(message); }

        return state;
    });

await dataflowService.StartAsync();

app.Lifetime.ApplicationStarted.Register(
    () =>
    {
        logger.LogInformation("Listening for Messages! Press CTRL+C to initiate graceful shutdown and stop consumer...");
    });

app.Lifetime.ApplicationStopping.Register(
    async () =>
    {
        logger.LogInformation("ConsumerDataflowService stopping...");

        await dataflowService.StopAsync(
            immediate: false,
            shutdownService: true);

        logger.LogInformation("All stopped! Press return to exit...");
    });

await app.RunAsync();

﻿using HouseofCat.Compression;
using HouseofCat.Dataflows;
using HouseofCat.Dataflows.Extensions;
using HouseofCat.Encryption;
using HouseofCat.RabbitMQ.Services;
using HouseofCat.Serialization;
using HouseofCat.Utilities.Errors;
using HouseofCat.Utilities.Helpers;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using static HouseofCat.Dataflows.Extensions.WorkStateExtensions;

namespace HouseofCat.RabbitMQ.Dataflows;

public interface IConsumerDataflow<TState> where TState : class, IRabbitWorkState, new()
{
    string WorkflowName { get; }
    string TimeFormat { get; set; }

    TState BuildState(IReceivedMessage receivedMessage);

    ConsumerDataflow<TState> SetCompressionProvider(ICompressionProvider provider);
    ConsumerDataflow<TState> SetEncryptionProvider(IEncryptionProvider provider);
    ConsumerDataflow<TState> SetSerializationProvider(ISerializationProvider provider);
    ConsumerDataflow<TState> UnsetSerializationProvider();

    ConsumerDataflow<TState> WithBuildState(int? maxDoP = null, bool? ensureOrdered = null, int? boundedCapacity = null, TaskScheduler taskScheduler = null);
    ConsumerDataflow<TState> WithDecompressionStep(int? maxDoP = null, bool? ensureOrdered = null, int? boundedCapacity = null, TaskScheduler taskScheduler = null);
    ConsumerDataflow<TState> WithDecryptionStep(int? maxDoP = null, bool? ensureOrdered = null, int? boundedCapacity = null, TaskScheduler taskScheduler = null);
    ConsumerDataflow<TState> AddStep(Func<TState, Task<TState>> suppliedStep, string stepName, int? maxDoP = null, bool? ensureOrdered = null, int? boundedCapacity = null, TaskScheduler taskScheduler = null);
    ConsumerDataflow<TState> AddStep(Func<TState, TState> suppliedStep, string stepName, int? maxDoP = null, bool? ensureOrdered = null, int? boundedCapacity = null, TaskScheduler taskScheduler = null);

    ConsumerDataflow<TState> WithDefaultErrorHandling(int boundedCapacity = 100, int? maxDoP = null, bool? ensureOrdered = null, TaskScheduler taskScheduler = null);
    ConsumerDataflow<TState> WithErrorHandling(Action<TState> action, int boundedCapacity, int? maxDoP = null, bool? ensureOrdered = null, TaskScheduler taskScheduler = null);
    ConsumerDataflow<TState> WithErrorHandling(Func<TState, Task> action, int boundedCapacity, int? maxDoP = null, bool? ensureOrdered = null, TaskScheduler taskScheduler = null);

    ConsumerDataflow<TState> WithDefaultFinalization(int? maxDoP = null, bool? ensureOrdered = null, int? boundedCapacity = null, TaskScheduler taskScheduler = null);
    ConsumerDataflow<TState> WithFinalization(Action<TState> action, int? maxDoP = null, bool? ensureOrdered = null, int? boundedCapacity = null, TaskScheduler taskScheduler = null);
    ConsumerDataflow<TState> WithFinalization(Func<TState, Task> action, int? maxDoP = null, bool? ensureOrdered = null, int? boundedCapacity = null, TaskScheduler taskScheduler = null);

    ConsumerDataflow<TState> WithPostProcessingBuffer(int boundedCapacity, TaskScheduler taskScheduler = null);
    ConsumerDataflow<TState> WithReadyToProcessBuffer(int boundedCapacity, TaskScheduler taskScheduler = null);

    ConsumerDataflow<TState> WithCreateSendMessage(Func<TState, Task<TState>> createMessage, int? maxDoP = null, bool? ensureOrdered = null, int? boundedCapacity = null, TaskScheduler taskScheduler = null);
    ConsumerDataflow<TState> WithCreateSendMessage(Func<TState, TState> createMessage, int? maxDoP = null, bool? ensureOrdered = null, int? boundedCapacity = null, TaskScheduler taskScheduler = null);
    ConsumerDataflow<TState> WithSendCompressedStep(int? maxDoP = null, bool? ensureOrdered = null, int? boundedCapacity = null, TaskScheduler taskScheduler = null);
    ConsumerDataflow<TState> WithSendEncryptedStep(int? maxDoP = null, bool? ensureOrdered = null, int? boundedCapacity = null, TaskScheduler taskScheduler = null);
    ConsumerDataflow<TState> WithSendMessageStep(int? maxDoP = null, bool? ensureOrdered = null, int? boundedCapacity = null, TaskScheduler taskScheduler = null);

    Task StartAsync();
    Task StopAsync(bool immediate = false, bool shutdownService = false);
}

public class ConsumerDataflow<TState> : BaseDataflow<TState>, IConsumerDataflow<TState> where TState : class, IRabbitWorkState, new()
{
    private readonly IRabbitService _rabbitService;
    private readonly ConsumerOptions _consumerOptions;
    private readonly TaskScheduler _taskScheduler;

    // Main Flow - Ingestion
    private readonly List<ConsumerBlock<IReceivedMessage>> _consumerBlocks;
    protected ITargetBlock<IReceivedMessage> _inputBuffer;
    private TransformBlock<IReceivedMessage, TState> _buildStateBlock;
    private TransformBlock<TState, TState> _createSendMessage;
    protected TransformBlock<TState, TState> _decryptBlock;
    protected TransformBlock<TState, TState> _decompressBlock;

    // Main Flow - User Defined/Supplied Steps
    protected ITargetBlock<TState> _readyBuffer;
    protected readonly List<TransformBlock<TState, TState>> _suppliedTransforms = new List<TransformBlock<TState, TState>>();

    // Main Flow - PostProcessing
    protected ITargetBlock<TState> _postProcessingBuffer;
    protected TransformBlock<TState, TState> _compressBlock;
    protected TransformBlock<TState, TState> _encryptBlock;
    protected TransformBlock<TState, TState> _sendMessageBlock;
    protected ActionBlock<TState> _finalization;

    // Error/Fault Flow
    protected ITargetBlock<TState> _errorBuffer;
    protected ActionBlock<TState> _errorAction;

    public string WorkflowName
    {
        get
        {
            return _consumerOptions?.WorkflowName;
        }
    }

    public string TimeFormat { get; set; } = TimeHelpers.Formats.RFC3339Long;

    public ConsumerDataflow(
        IRabbitService rabbitService,
        ConsumerOptions consumerOptions,
        TaskScheduler taskScheduler = null)
    {
        Guard.AgainstNull(rabbitService, nameof(rabbitService));
        Guard.AgainstNull(consumerOptions, nameof(consumerOptions));

        _rabbitService = rabbitService;
        _consumerOptions = consumerOptions;
        _serializationProvider = rabbitService.SerializationProvider;

        _linkStepOptions = new DataflowLinkOptions { PropagateCompletion = true };
        _taskScheduler = taskScheduler ?? TaskScheduler.Current;

        _executeStepOptions = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = _consumerOptions.WorkflowMaxDegreesOfParallelism < 1
                ? 1
                : _consumerOptions.WorkflowMaxDegreesOfParallelism,
            SingleProducerConstrained = true,
            EnsureOrdered = _consumerOptions.WorkflowEnsureOrdered,
            TaskScheduler = _taskScheduler,
        };

        _consumerBlocks = new List<ConsumerBlock<IReceivedMessage>>();
    }

    public virtual Task StartAsync() => StartAsync<ConsumerBlock<IReceivedMessage>>();

    protected async Task StartAsync<TConsumerBlock>() where TConsumerBlock : ConsumerBlock<IReceivedMessage>, new()
    {
        BuildLinkages<TConsumerBlock>();

        foreach (var consumerBlock in _consumerBlocks)
        {
            await consumerBlock.StartConsumingAsync().ConfigureAwait(false);
        }
    }

    public async Task StopAsync(
        bool immediate = false,
        bool shutdownService = false)
    {
        foreach (var consumerBlock in _consumerBlocks)
        {
            await consumerBlock.StopConsumingAsync(immediate).ConfigureAwait(false);
            consumerBlock.Complete();
        }

        if (shutdownService)
        {
            await _rabbitService.ShutdownAsync(immediate);
        }
    }

    /// <summary>
    /// Allows you to set the consumers serialization provider. This will be used to convert your bytes into an object.
    /// <para>By default, the serialization provider will auto-assign the same serialization provider as the one RabbitService uses. This is the most common use case.</para>
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> SetSerializationProvider(ISerializationProvider provider)
    {
        Guard.AgainstNull(provider, nameof(provider));
        _serializationProvider = provider;
        return this;
    }

    /// <summary>
    /// Allows you to unset the consumers serialization provider. This will be used when you are not using any serialization on your inner byte payloads.
    /// <para>By default, the serialization provider will auto-assign the same serialization provider (in the Constructor) as the one RabbitService uses.</para>
    /// <para>This is a more exotic scenario where you may be moving plain bytes around.</para>
    /// <para>ex.) You are transferring data from queue to database (or other queue) and don't need to deserialize the bytes.</para>
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> UnsetSerializationProvider()
    {
        _serializationProvider = null;
        return this;
    }

    public ConsumerDataflow<TState> SetCompressionProvider(ICompressionProvider provider)
    {
        Guard.AgainstNull(provider, nameof(provider));
        _compressionProvider = provider;
        return this;
    }

    public ConsumerDataflow<TState> SetEncryptionProvider(IEncryptionProvider provider)
    {
        Guard.AgainstNull(provider, nameof(provider));
        _encryptionProvider = provider;
        return this;
    }

    #region Step Adders

    protected static readonly string _defaultSpanNameFormat = "{0}.{1}";
    protected static readonly string _defaultStepSpanNameFormat = "{0}.step_{1}.{2}";

    protected string GetSpanName(string stepName)
    {
        return string.Format(_defaultSpanNameFormat, WorkflowName, stepName);
    }

    protected string GetStepSpanName(string stepName)
    {
        return string.Format(_defaultStepSpanNameFormat, WorkflowName, _suppliedTransforms.Count, stepName);
    }

    protected virtual ITargetBlock<TState> CreateTargetBlock(
        int boundedCapacity,
        TaskScheduler taskScheduler = null) =>
        new BufferBlock<TState>(
            new DataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity > 0 ? boundedCapacity : 1000,
                TaskScheduler = taskScheduler ?? _taskScheduler
            });

    /// <summary>
    /// This method allows you to set the action as the async error handler for the ConsumerDataflow.
    /// </summary>
    /// <param name="action"></param>
    /// <param name="boundedCapacity"></param>
    /// <param name="maxDoP"></param>
    /// <param name="ensureOrdered"></param>
    /// <param name="taskScheduler"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> WithErrorHandling(
        Action<TState> action,
        int boundedCapacity,
        int? maxDoP = null,
        bool? ensureOrdered = null,
        TaskScheduler taskScheduler = null)
    {
        Guard.AgainstNull(action, nameof(action));
        if (_errorBuffer is not null) return this;

        _errorBuffer = CreateTargetBlock(boundedCapacity, taskScheduler);
        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);
        _errorAction = GetLastWrappedActionBlock(action, executionOptions, GetSpanName("error_handler"));

        return this;
    }

    /// <summary>
    /// This method allows you to set the asynchronous function as the async error handler for the ConsumerDataflow.
    /// </summary>
    /// <param name="action"></param>
    /// <param name="boundedCapacity"></param>
    /// <param name="maxDoP"></param>
    /// <param name="ensureOrdered"></param>
    /// <param name="taskScheduler"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> WithErrorHandling(
        Func<TState, Task> action,
        int boundedCapacity,
        int? maxDoP = null,
        bool? ensureOrdered = null,
        TaskScheduler taskScheduler = null)
    {
        Guard.AgainstNull(action, nameof(action));
        if (_errorBuffer is not null)

            _errorBuffer = CreateTargetBlock(boundedCapacity, taskScheduler);
        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);
        _errorAction = GetLastWrappedActionBlock(action, executionOptions, GetSpanName("error_handler"));

        return this;
    }

    /// <summary>
    /// This method sets up the default error handler for the ConsumerDataflow.
    /// <para>1.) It will check to use rejection without requeue if the ConsumerOptions QueueArgs has the DLQ in it.</para>
    /// <para>2.) It will check to use ErrorQueueName in ConsumerOptions to send change the routingkey and send to AutoPublisher.</para>
    /// <para>3.) It will Nack with retry: true.</para>
    /// </summary>
    /// <param name="boundedCapacity"></param>
    /// <param name="maxDoP"></param>
    /// <param name="ensureOrdered"></param>
    /// <param name="taskScheduler"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> WithDefaultErrorHandling(
        int boundedCapacity = 100,
        int? maxDoP = null,
        bool? ensureOrdered = null,
        TaskScheduler taskScheduler = null)
    {
        if (_errorBuffer is not null) return this;

        _errorBuffer = CreateTargetBlock(boundedCapacity, taskScheduler);
        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);
        _errorAction = GetLastWrappedActionBlock(DefaultErrorHandlerAsync, executionOptions, GetSpanName("error_handler"));

        return this;
    }

    private async Task DefaultErrorHandlerAsync(TState state)
    {
        var logger = LogHelpers.GetLogger<ConsumerDataflow<TState>>();
        logger.LogError(state?.EDI?.SourceException, "Error Handler: {0}", state?.EDI?.SourceException?.Message);
        // First, check if DLQ is configured in QueueArgs.
        // Second, check if ErrorQueue is set in Options.
        // Lastly, decide if you want to Nack with requeue, or anything else.

        if (_consumerOptions.RejectOnError())
        {
            state.ReceivedMessage?.RejectMessage(requeue: false);
        }
        else if (!string.IsNullOrEmpty(_consumerOptions.ErrorQueueName))
        {
            // If type is currently an IMessage, republish with new RoutingKey.
            if (state.ReceivedMessage.Message is not null)
            {
                state.ReceivedMessage.Message.RoutingKey = _consumerOptions.ErrorQueueName;
                await _rabbitService.Publisher.QueueMessageAsync(state.ReceivedMessage.Message);
            }
            else
            {
                await _rabbitService.Publisher.PublishAsync(
                    exchangeName: "",
                    routingKey: _consumerOptions.ErrorQueueName,
                    body: state.ReceivedMessage.Body,
                    headers: state.ReceivedMessage.Properties.Headers,
                    messageId: Guid.NewGuid().ToString(),
                    deliveryMode: 2,
                    mandatory: false);
            }

            // Don't forget to Ack the original message when sending it to a different Queue.
            state.ReceivedMessage?.AckMessage();
        }
        else
        {
            state.ReceivedMessage?.NackMessage(requeue: true);
        }
    }

    public ConsumerDataflow<TState> WithReadyToProcessBuffer(int boundedCapacity, TaskScheduler taskScheduler = null)
    {
        _readyBuffer ??= CreateTargetBlock(boundedCapacity, taskScheduler);
        return this;
    }

    /// <summary>
    /// This is the method that lets you add your supplied synchronous function.
    /// </summary>
    /// <param name="suppliedStep"></param>
    /// <param name="stepName"></param>
    /// <param name="maxDoP"></param>
    /// <param name="ensureOrdered"></param>
    /// <param name="boundedCapacity"></param>
    /// <param name="taskScheduler"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> AddStep(
        Func<TState, TState> suppliedStep,
        string stepName,
        int? maxDoP = null,
        bool? ensureOrdered = null,
        int? boundedCapacity = null,
        TaskScheduler taskScheduler = null)
    {
        Guard.AgainstNull(suppliedStep, nameof(suppliedStep));

        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);
        _suppliedTransforms.Add(GetWrappedTransformBlock(suppliedStep, executionOptions, GetStepSpanName(stepName)));
        return this;
    }

    /// <summary>
    /// This is the method that lets you add your supplied asynchronous function.
    /// </summary>
    /// <param name="suppliedStep"></param>
    /// <param name="stepName"></param>
    /// <param name="maxDoP"></param>
    /// <param name="ensureOrdered"></param>
    /// <param name="boundedCapacity"></param>
    /// <param name="taskScheduler"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> AddStep(
        Func<TState, Task<TState>> suppliedStep,
        string stepName,
        int? maxDoP = null,
        bool? ensureOrdered = null,
        int? boundedCapacity = null,
        TaskScheduler taskScheduler = null)
    {
        Guard.AgainstNull(suppliedStep, nameof(suppliedStep));

        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);
        _suppliedTransforms.Add(GetWrappedTransformBlock(suppliedStep, executionOptions, GetStepSpanName(stepName)));
        return this;
    }

    public ConsumerDataflow<TState> WithPostProcessingBuffer(
        int boundedCapacity, TaskScheduler taskScheduler = null)
    {
        _postProcessingBuffer ??= CreateTargetBlock(boundedCapacity, taskScheduler);
        return this;
    }

    /// <summary>
    /// This method sets up the last step of the ConsumerDataflow with your supplied action.
    /// </summary>
    /// <param name="action"></param>
    /// <param name="maxDoP"></param>
    /// <param name="ensureOrdered"></param>
    /// <param name="boundedCapacity"></param>
    /// <param name="taskScheduler"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> WithFinalization(
        Action<TState> action,
        int? maxDoP = null,
        bool? ensureOrdered = null,
        int? boundedCapacity = null,
        TaskScheduler taskScheduler = null)
    {
        Guard.AgainstNull(action, nameof(action));
        if (_finalization is not null) return this;

        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);
        _finalization = GetLastWrappedActionBlock(action, executionOptions, GetSpanName("finalization"));

        return this;
    }

    /// <summary>
    /// This method sets up the last step of the ConsumerDataflow with your supplied function.
    /// </summary>
    /// <param name="action"></param>
    /// <param name="maxDoP"></param>
    /// <param name="ensureOrdered"></param>
    /// <param name="boundedCapacity"></param>
    /// <param name="taskScheduler"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> WithFinalization(
        Func<TState, Task> action,
        int? maxDoP = null,
        bool? ensureOrdered = null,
        int? boundedCapacity = null,
        TaskScheduler taskScheduler = null)
    {
        Guard.AgainstNull(action, nameof(action));
        if (_finalization is not null) return this;

        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);
        _finalization = GetLastWrappedActionBlock(action, executionOptions, GetSpanName("finalization"));
        return this;
    }

    /// <summary>
    /// This method sets up the last step of the ConsumerDataflow with a default Finalization method that the Message has finished and acks the
    /// IReceivedMessage.
    /// </summary>
    /// <param name="maxDoP"></param>
    /// <param name="ensureOrdered"></param>
    /// <param name="boundedCapacity"></param>
    /// <param name="taskScheduler"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> WithDefaultFinalization(
        int? maxDoP = null,
        bool? ensureOrdered = null,
        int? boundedCapacity = null,
        TaskScheduler taskScheduler = null)
    {
        if (_finalization is not null) return this;

        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);
        _finalization = GetLastWrappedActionBlock(DefaultFinalization, executionOptions, GetSpanName("finalization"));

        return this;
    }

    protected static readonly string _defaultFinalizationMessage = "Message [Id: {0}] finished processing. Acking message.";

    protected void DefaultFinalization(TState state)
    {
        var logger = LogHelpers.GetLogger<ConsumerDataflow<TState>>();
        logger.LogInformation(_defaultFinalizationMessage, state?.ReceivedMessage?.Message?.MessageId);

        state.ReceivedMessage?.AckMessage();
    }

    /// <summary>
    /// This simple method ensures you have a IRabbitWorkState object to work with you IReceivedMessage assigned to it.
    /// </summary>
    /// <param name="maxDoP"></param>
    /// <param name="ensureOrdered"></param>
    /// <param name="boundedCapacity"></param>
    /// <param name="taskScheduler"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> WithBuildState(
        int? maxDoP = null,
        bool? ensureOrdered = null,
        int? boundedCapacity = null,
        TaskScheduler taskScheduler = null)
    {
        if (_buildStateBlock is not null) return this;

        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);
        _buildStateBlock = GetBuildStateBlock(executionOptions);

        return this;
    }

    /// <summary>
    /// This method will automatically decrypt the IReceivedMessage.Body or IReceivedMessage.Message.Body.
    /// <para>It will skip execution if the body is not encrypted.</para>
    /// </summary>
    /// <param name="maxDoP"></param>
    /// <param name="ensureOrdered"></param>
    /// <param name="boundedCapacity"></param>
    /// <param name="taskScheduler"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> WithDecryptionStep(
        int? maxDoP = null,
        bool? ensureOrdered = null,
        int? boundedCapacity = null,
        TaskScheduler taskScheduler = null)
    {
        Guard.AgainstNull(_encryptionProvider, nameof(_encryptionProvider));
        if (_decryptBlock is not null) return this;

        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);

        _decryptBlock = GetByteManipulationTransformBlock(
            _encryptionProvider.Decrypt,
            executionOptions,
            false,
            x => x.ReceivedMessage.Encrypted,
            GetSpanName("receive_decrypt"),
            (state) =>
            {
                if (state?.ReceivedMessage is null) return;
                state.ReceivedMessage.Encrypted = false;
                state.ReceivedMessage.EncryptionType = null;
                state.ReceivedMessage.EncryptedDateTime = default;

                if (state?.ReceivedMessage?.Message?.Metadata?.Fields is null) return;
                state.ReceivedMessage.Message.Metadata.Fields[Constants.HeaderForEncrypted] = false;
                state.ReceivedMessage.Message.Metadata.Fields.Remove(Constants.HeaderForEncryption);
                state.ReceivedMessage.Message.Metadata.Fields.Remove(Constants.HeaderForEncryptDate);
            });

        return this;
    }

    /// <summary>
    /// This method will automatically decompress the IReceivedMessage.Body or IReceivedMessage.Message.Body.
    /// <para>It will skip execution if the body is not compressed.</para>
    /// </summary>
    /// <param name="maxDoP"></param>
    /// <param name="ensureOrdered"></param>
    /// <param name="boundedCapacity"></param>
    /// <param name="taskScheduler"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> WithDecompressionStep(
        int? maxDoP = null,
        bool? ensureOrdered = null,
        int? boundedCapacity = null,
        TaskScheduler taskScheduler = null)
    {
        Guard.AgainstNull(_compressionProvider, nameof(_compressionProvider));
        if (_decompressBlock is not null) return this;

        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);

        _decompressBlock = GetByteManipulationTransformBlock(
            _compressionProvider.Decompress,
            executionOptions,
            false,
            x => x.ReceivedMessage.Compressed,
            GetSpanName("receive_decompress"),
            (state) =>
            {
                if (state?.ReceivedMessage is null) return;
                state.ReceivedMessage.Compressed = false;
                state.ReceivedMessage.CompressionType = null;

                if (state?.ReceivedMessage?.Message?.Metadata?.Fields is null) return;
                state.ReceivedMessage.Message.Metadata.Fields[Constants.HeaderForCompressed] = false;
                state.ReceivedMessage.Message.Metadata.Fields.Remove(Constants.HeaderForCompression);
            });

        return this;
    }

    public ConsumerDataflow<TState> WithCreateSendMessage(
        Func<TState, Task<TState>> createMessage,
        int? maxDoP = null,
        bool? ensureOrdered = null,
        int? boundedCapacity = null,
        TaskScheduler taskScheduler = null)
    {
        if (_createSendMessage is not null) return this;

        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);
        _createSendMessage = GetWrappedTransformBlock(createMessage, executionOptions, GetSpanName("send_create"));
        return this;
    }

    public ConsumerDataflow<TState> WithCreateSendMessage(
        Func<TState, TState> createMessage,
        int? maxDoP = null,
        bool? ensureOrdered = null,
        int? boundedCapacity = null,
        TaskScheduler taskScheduler = null)
    {
        if (_createSendMessage is not null) return this;

        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);
        _createSendMessage = GetWrappedTransformBlock(createMessage, executionOptions, GetSpanName("send_create"));
        return this;
    }

    /// <summary>
    /// This method will Compress any SendMessage.Body before sending it out.
    /// <para>It will skip if the message body is already compressed.</para>
    /// </summary>
    /// <param name="maxDoP"></param>
    /// <param name="ensureOrdered"></param>
    /// <param name="boundedCapacity"></param>
    /// <param name="taskScheduler"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> WithSendCompressedStep(
        int? maxDoP = null,
        bool? ensureOrdered = null,
        int? boundedCapacity = null,
        TaskScheduler taskScheduler = null)
    {
        Guard.AgainstNull(_compressionProvider, nameof(_compressionProvider));

        if (_compressBlock is not null) return this;
        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);

        _compressBlock = GetByteManipulationTransformBlock(
            _compressionProvider.Compress,
            executionOptions,
            true,
            x => !x.SendMessage.Metadata.Compressed(),
            GetSpanName("send_compress"),
            (state) =>
            {
                if (state?.SendMessage?.Metadata?.Fields is null) return;
                state.SendMessage.Metadata.Fields[Constants.HeaderForCompressed] = true;
                state.SendMessage.Metadata.Fields[Constants.HeaderForCompression] = _compressionProvider.Type;
            });

        return this;
    }

    /// <summary>
    /// This method will Encrypted any SendMessage.Body before sending it out.
    /// <para>It will skip if the message body is already encrypted.</para>
    /// </summary>
    /// <param name="maxDoP"></param>
    /// <param name="ensureOrdered"></param>
    /// <param name="boundedCapacity"></param>
    /// <param name="taskScheduler"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> WithSendEncryptedStep(
        int? maxDoP = null,
        bool? ensureOrdered = null,
        int? boundedCapacity = null,
        TaskScheduler taskScheduler = null)
    {
        Guard.AgainstNull(_encryptionProvider, nameof(_encryptionProvider));
        if (_encryptBlock is not null) return this;

        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);

        _encryptBlock = GetByteManipulationTransformBlock(
            _encryptionProvider.Encrypt,
            executionOptions,
            true,
            x => !x.SendMessage.Metadata.Encrypted(),
            GetSpanName("send_encrypt"),
            (state) =>
            {
                if (state?.SendMessage?.Metadata?.Fields is null) return;
                state.SendMessage.Metadata.Fields[Constants.HeaderForEncrypted] = true;
                state.SendMessage.Metadata.Fields[Constants.HeaderForEncryption] = _encryptionProvider.Type;
                state.SendMessage.Metadata.Fields[Constants.HeaderForEncryptDate] = TimeHelpers.GetDateTimeNow(TimeFormat);
            });

        return this;
    }

    /// <summary>
    /// This method will put any SendMessage into the AutoPublisher for delivery.
    /// </summary>
    /// <param name="maxDoP"></param>
    /// <param name="ensureOrdered"></param>
    /// <param name="boundedCapacity"></param>
    /// <param name="taskScheduler"></param>
    /// <returns></returns>
    public ConsumerDataflow<TState> WithSendMessageStep(
        int? maxDoP = null,
        bool? ensureOrdered = null,
        int? boundedCapacity = null,
        TaskScheduler taskScheduler = null)
    {
        if (_sendMessageBlock is not null) return this;

        var executionOptions = GetExecuteStepOptions(maxDoP, ensureOrdered, boundedCapacity, taskScheduler ?? _taskScheduler);
        _sendMessageBlock = GetWrappedSendTransformBlock(_rabbitService, executionOptions);

        return this;
    }

    #endregion

    #region Step Linking

    protected virtual void BuildLinkages<TConsumerBlock>(DataflowLinkOptions overrideOptions = null)
        where TConsumerBlock : ConsumerBlock<IReceivedMessage>, new()
    {
        Guard.AgainstNull(_buildStateBlock, nameof(_buildStateBlock)); // Create State Is Mandatory
        Guard.AgainstNull(_finalization, nameof(_finalization)); // Leaving The Workflow Is Mandatory
        Guard.AgainstNull(_errorAction, nameof(_errorAction)); // Processing Errors Is Mandatory

        _inputBuffer ??= new BufferBlock<IReceivedMessage>();
        _readyBuffer ??= new BufferBlock<TState>();
        _postProcessingBuffer ??= new BufferBlock<TState>();

        for (var i = 0; i < _consumerOptions.WorkflowConsumerCount; i++)
        {
            var consumerBlock = new TConsumerBlock
            {
                Consumer = new Consumer(_rabbitService.ChannelPool, _consumerOptions.ConsumerName)
            };
            _consumerBlocks.Add(consumerBlock);
            _consumerBlocks[i].LinkTo(_inputBuffer, overrideOptions ?? _linkStepOptions);
        }

        ((ISourceBlock<IReceivedMessage>)_inputBuffer).LinkTo(_buildStateBlock, overrideOptions ?? _linkStepOptions);
        _buildStateBlock.LinkTo(_errorBuffer, overrideOptions ?? _linkStepOptions, x => x is null);
        SetCurrentSourceBlock(_buildStateBlock);

        LinkPreProcessing(overrideOptions);
        LinkSuppliedSteps(overrideOptions);
        LinkPostProcessing(overrideOptions);

        ((ISourceBlock<TState>)_errorBuffer).LinkTo(_errorAction, overrideOptions ?? _linkStepOptions);
    }

    private void LinkPreProcessing(DataflowLinkOptions overrideOptions = null)
    {
        // Link Deserialize to DecryptBlock with predicate if its not null.
        if (_decryptBlock is not null)
        { LinkWithFaultRoute(_currentBlock, _decryptBlock, x => x.IsFaulted, overrideOptions ?? _linkStepOptions); }

        if (_decompressBlock is not null)
        { LinkWithFaultRoute(_currentBlock, _decompressBlock, x => x.IsFaulted, overrideOptions ?? _linkStepOptions); }

        _currentBlock.LinkTo(_readyBuffer, overrideOptions ?? _linkStepOptions, x => !x.IsFaulted);
        SetCurrentSourceBlock(_readyBuffer);
    }

    private void LinkSuppliedSteps(DataflowLinkOptions overrideOptions = null)
    {
        // Link all user steps.
        if (_suppliedTransforms?.Count > 0)
        {
            for (var i = 0; i < _suppliedTransforms.Count; i++)
            {
                if (i == 0)
                { LinkWithFaultRoute(_currentBlock, _suppliedTransforms[i], x => x.IsFaulted, overrideOptions ?? _linkStepOptions); }
                else // Link Previous Step, To Next Step
                { LinkWithFaultRoute(_suppliedTransforms[i - 1], _suppliedTransforms[i], x => x.IsFaulted, overrideOptions ?? _linkStepOptions); }
            }

            // Link the last user step to PostProcessingBuffer/CreateSendMessage.
            if (_createSendMessage is not null)
            {
                LinkWithFaultRoute(_suppliedTransforms[^1], _createSendMessage, x => x.IsFaulted, overrideOptions ?? _linkStepOptions);
                _createSendMessage.LinkTo(_postProcessingBuffer, overrideOptions ?? _linkStepOptions);
                SetCurrentSourceBlock(_postProcessingBuffer);
            }
            else
            {
                _suppliedTransforms[^1].LinkTo(_postProcessingBuffer, overrideOptions ?? _linkStepOptions);
                SetCurrentSourceBlock(_postProcessingBuffer);
            }
        }
    }

    private void LinkPostProcessing(DataflowLinkOptions overrideOptions = null)
    {
        if (_compressBlock is not null)
        { LinkWithFaultRoute(_currentBlock, _compressBlock, x => x.IsFaulted, overrideOptions); }

        if (_encryptBlock is not null)
        { LinkWithFaultRoute(_currentBlock, _encryptBlock, x => x.IsFaulted, overrideOptions); }

        if (_sendMessageBlock is not null)
        { LinkWithFaultRoute(_currentBlock, _sendMessageBlock, x => x.IsFaulted, overrideOptions); }

        _currentBlock.LinkTo(_finalization, overrideOptions ?? _linkStepOptions); // Last Action
    }

    private void LinkWithFaultRoute(
        ISourceBlock<TState> source,
        IPropagatorBlock<TState, TState> target,
        Predicate<TState> faultPredicate,
        DataflowLinkOptions overrideOptions = null)
    {
        source.LinkTo(target, overrideOptions ?? _linkStepOptions);
        target.LinkTo(_errorBuffer, overrideOptions ?? _linkStepOptions, faultPredicate); // Fault Linkage
        SetCurrentSourceBlock(target);
    }

    #endregion

    #region Step Wrappers

    public virtual TState BuildState(IReceivedMessage receivedMessage)
    {
        var state = new TState
        {
            ReceivedMessage = receivedMessage,
            Data = new Dictionary<string, object>()
        };

        state.StartWorkflowSpan(
            WorkflowName,
            spanKind: SpanKind.Internal,
            suppliedAttributes: GetSpanAttributes(state, receivedMessage),
            parentSpanContext: receivedMessage.ParentSpanContext);

        return state;
    }

    protected virtual List<KeyValuePair<string, string>> GetSpanAttributes(TState state, IReceivedMessage receivedMessage)
    {
        var attributes = new List<KeyValuePair<string, string>>
        {
            KeyValuePair.Create(Constants.MessagingConsumerNameKey, _consumerOptions.ConsumerName),
            KeyValuePair.Create(Constants.MessagingOperationKey, Constants.MessagingOperationProcessValue)
        };

        if (state.ReceivedMessage?.Message?.MessageId is not null)
        {
            attributes.Add(KeyValuePair.Create(Constants.MessagingMessageMessageIdKey, state.ReceivedMessage.Message.MessageId));
        }
        if (state.ReceivedMessage?.Message?.Metadata?.PayloadId is not null)
        {
            attributes.Add(KeyValuePair.Create(Constants.MessagingMessagePayloadIdKey, state.ReceivedMessage.Message.Metadata.PayloadId));
        }
        if (state.ReceivedMessage?.DeliveryTag is not null)
        {
            attributes.Add(KeyValuePair.Create(Constants.MessagingMessageDeliveryTagIdKey, state.ReceivedMessage.DeliveryTag.ToString()));
        }

        return attributes;
    }

    protected TransformBlock<IReceivedMessage, TState> GetBuildStateBlock(
        ExecutionDataflowBlockOptions options)
    {
        TState BuildStateWrap(IReceivedMessage data)
        {
            try
            { return BuildState(data); }
            catch
            { return null; }
        }

        return new TransformBlock<IReceivedMessage, TState>(BuildStateWrap, options);
    }

    protected TransformBlock<TState, TState> GetByteManipulationTransformBlock(
        Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> action,
        ExecutionDataflowBlockOptions options,
        bool outbound,
        Predicate<TState> predicate,
        string spanName,
        Action<TState> callback = null)
    {
        TState WrapAction(TState state)
        {
            using var childSpan = state.CreateActiveChildSpan(spanName, SpanKind.Internal);
            try
            {
                if (outbound)
                {
                    if (state.SendData.Length > 0)
                    { state.SendData = action(state.SendData); }
                    else if (state.SendMessage.Body.Length > 0)
                    { state.SendMessage.Body = action(state.SendMessage.Body); }
                }
                else if (predicate.Invoke(state))
                {
                    if (state.ReceivedMessage?.Message?.Body.Length > 0)
                    { state.ReceivedMessage.Message.Body = action(state.ReceivedMessage.Message.Body); }
                    else
                    { state.ReceivedMessage.Body = action(state.ReceivedMessage.Body); }
                }

                if (callback is not null)
                { callback(state); }
            }
            catch (Exception ex)
            {
                childSpan?.SetStatus(Status.Error);
                childSpan?.RecordException(ex);
                state.IsFaulted = true;
                state.EDI = ExceptionDispatchInfo.Capture(ex);
            }

            childSpan?.End();
            return state;
        }

        return new TransformBlock<TState, TState>(WrapAction, options);
    }

    protected TransformBlock<TState, TState> GetByteManipulationTransformBlock(
        Func<ReadOnlyMemory<byte>, Task<byte[]>> action,
        ExecutionDataflowBlockOptions options,
        bool outbound,
        Predicate<TState> predicate,
        string spanName,
        Action<TState> callback = null)
    {
        async Task<TState> WrapActionAsync(TState state)
        {
            using var childSpan = state.CreateActiveChildSpan(spanName, SpanKind.Internal);
            try
            {

                if (outbound)
                {
                    if (state.SendData.Length > 0)
                    { state.SendData = await action(state.SendData).ConfigureAwait(false); }
                    else if (state.SendMessage.Body.Length > 0)
                    { state.SendMessage.Body = await action(state.SendMessage.Body).ConfigureAwait(false); }
                }
                else if (predicate.Invoke(state))
                {
                    if (state.ReceivedMessage?.Message?.Body.Length > 0)
                    { state.ReceivedMessage.Message.Body = await action(state.ReceivedMessage.Message.Body).ConfigureAwait(false); }
                    else
                    { state.ReceivedMessage.Body = await action(state.ReceivedMessage.Body).ConfigureAwait(false); }
                }

                if (callback is not null)
                { callback(state); }
            }
            catch (Exception ex)
            {
                childSpan?.SetStatus(Status.Error);
                childSpan?.RecordException(ex);
                state.IsFaulted = true;
                state.EDI = ExceptionDispatchInfo.Capture(ex);
            }

            childSpan?.End();
            return state;
        }

        return new TransformBlock<TState, TState>(WrapActionAsync, options);
    }

    private string SendStepIdentifier => $"{WorkflowName} send";
    private static readonly string _shutdownInProgress = "Shutdown in progress.";

    protected TransformBlock<TState, TState> GetWrappedSendTransformBlock(
        IRabbitService rabbitService,
        ExecutionDataflowBlockOptions options)
    {
        async Task<TState> WrapPublishAsync(TState state)
        {
            if (state?.SendMessage is null) return state;

            using var childSpan = state.CreateActiveChildSpan(SendStepIdentifier, SpanKind.Producer);
            try
            {
                await rabbitService.Publisher.QueueMessageAsync(state.SendMessage).ConfigureAwait(false);
            }
            // Shutdown is likely in progress, so we can't publish this message.
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("AutoPublisher"))
            {
                state.IsFaulted = true; // Dev's should nack the message with requeue true.
                childSpan?.SetStatus(Status.Error.WithDescription(_shutdownInProgress));
            }
            catch (Exception ex)
            {
                childSpan?.SetStatus(Status.Error);
                childSpan?.RecordException(ex);
                state.IsFaulted = true;
                state.EDI = ExceptionDispatchInfo.Capture(ex);
            }

            childSpan?.End();
            return state;
        }

        return new TransformBlock<TState, TState>(WrapPublishAsync, options);
    }

    #endregion
}

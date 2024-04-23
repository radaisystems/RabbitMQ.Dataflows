using System;

namespace RadAI.RabbitMQ;

public class GlobalConsumerPipelineOptions
{
    public bool? WaitForCompletion { get; set; }
    public int? MaxDegreesOfParallelism { get; set; } = Environment.ProcessorCount;
    public bool? EnsureOrdered { get; set; }
}

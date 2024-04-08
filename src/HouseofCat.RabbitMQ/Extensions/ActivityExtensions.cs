using OpenTelemetry.Trace;
using System;
using System.Diagnostics;

namespace HouseofCat.RabbitMQ.Extensions;
public static class ActivityExtensions
{
    // See https://opentelemetry.io/docs/specs/otel/trace/semantic_conventions/exceptions/
    public static void SetExceptionTags(this Activity activity, Exception ex)
    {
        if (activity is null)
        {
            return;
        }

        activity.RecordException(ex);
        _ = activity.SetStatus(ActivityStatusCode.Error);
    }

    public static void AddMessagingTags(this Activity activity, IMessage message)
    {
        if (activity is null)
        {
            return;
        }
        // See:
        //   * https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/messaging-spans.md#messaging-attributes
        //   * https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/rabbitmq.md
        _ = activity.SetTag("messaging.system", "rabbitmq");
        _ = activity.SetTag("messaging.destination_kind", "queue");
        _ = activity.SetTag("messaging.destination", message.Envelope.Exchange);
        _ = activity.SetTag("messaging.rabbitmq.routing_key", message.Envelope.RoutingKey);
        _ = activity.SetTag("messaging.message.id", message.MessageId);
        _ = activity.SetTag("messaging.operation", "publish");
    }
}
using System.Collections.Generic;

namespace HouseofCat.RabbitMQ;

public sealed record ExchangeBinding
{
    public string ChildExchange { get; set; }
    public string ParentExchange { get; set; }
    public string RoutingKey { get; set; }
    public IDictionary<string, object> Args { get; set; }
}

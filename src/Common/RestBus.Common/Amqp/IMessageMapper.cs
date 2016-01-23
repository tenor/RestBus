using System.Collections.Generic;
using System.Net.Http;
namespace RestBus.Common.Amqp
{
    public interface IMessageMapper
    {
        MessagingConfiguration GetMessagingConfig();
        string GetServiceName(HttpRequestMessage request);
        string GetRoutingKey(HttpRequestMessage request, ExchangeKind exchangeKind);
        IDictionary<string, object> GetHeaders(HttpRequestMessage request);
    }
}

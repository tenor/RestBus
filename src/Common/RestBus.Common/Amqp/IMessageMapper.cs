using System.Collections.Generic;
using System.Net.Http;
namespace RestBus.Common.Amqp
{
    public interface IMessageMapper
    {
        IList<AmqpConnectionInfo> ServerUris { get; }
        ExchangeKind SupportedExchangeKinds { get; }
        MessagingConfiguration MessagingConfig { get; }
        string GetServiceName(HttpRequestMessage request);
        string GetRoutingKey(HttpRequestMessage request, ExchangeKind exchangeKind);
        IDictionary<string, object> GetHeaders(HttpRequestMessage request);
    }
}

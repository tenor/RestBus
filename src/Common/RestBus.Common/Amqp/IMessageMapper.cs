using System.Net.Http;
namespace RestBus.Common.Amqp
{
    public interface IMessageMapper
    {
        ExchangeInfo GetExchangeInfo();
        string GetRoutingKey(HttpRequestMessage request);
        bool GetExpires(HttpRequestMessage request);
    }
}

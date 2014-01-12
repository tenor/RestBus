
namespace RestBus.Common
{
    public class MessageContext
    {
        public HttpRequestPacket Request { get; set;}
        public string ReplyToQueue { get; set; }
        public string CorrelationId { get; set; }

    }
}

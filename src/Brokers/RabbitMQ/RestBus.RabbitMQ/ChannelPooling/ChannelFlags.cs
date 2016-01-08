
namespace RestBus.RabbitMQ.ChannelPooling
{
    internal enum ChannelFlags
    {
        None = 0, //Regular channel
        Consumer = 1, //Channel linked to a long running consumer
        PublisherConfirms = 2, //Channel streams publisher confirms.
        RPC = 3, //RPC Channel
        RPCWithPublisherConfirms = 4 //RPC channel with publisher confirms.
        
        //NOTE: Add Tx channel type if AMQP transactions are ever supported.
    }
}

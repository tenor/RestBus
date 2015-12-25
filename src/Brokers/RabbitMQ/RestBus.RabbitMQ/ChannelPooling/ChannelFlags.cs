
namespace RestBus.RabbitMQ.ChannelPooling
{
    internal enum ChannelFlags
    {
        None = 0, //Regular channel
        Consumer = 1, //Channel linked to a consumer
        PublisherConfirms = 2 //Channel streams publisher confirms.
        
        //NOTE: Add Tx channel type if AMQP transactions are ever supported.
    }
}

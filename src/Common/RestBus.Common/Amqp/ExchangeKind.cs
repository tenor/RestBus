using System;

namespace RestBus.Common.Amqp
{
    [Flags]
    public enum ExchangeKind
    {
        Direct = 1,
        Fanout = 2,
        Headers = 4,
        Topic = 8
    }
}

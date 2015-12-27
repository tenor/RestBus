using System;

namespace RestBus.Common.Amqp
{
    [Flags]
    public enum ExchangeKind
    {
        Direct = 1,
        Fanout = 2,
        Topic = 4,
        Headers = 8
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestBus.RabbitMQ.Client
{
    public class ClientSettings
    {
        public ClientAckBehavior AckBehavior { get; set; }
        public bool DisableDirectReplies { get; set; }
    }
}

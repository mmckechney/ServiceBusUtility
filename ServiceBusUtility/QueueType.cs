using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceBusUtility
{
    internal enum QueueType
    {
        Queue,
        TopicSubscription,
        TopicScheduled,
        DeadLetterQueue,
        DeadLetterSubscription

    }
}

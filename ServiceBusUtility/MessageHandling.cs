namespace ServiceBusUtility
{
    enum MessageHandling
    {
        Complete,
        Abandon,
        LockExpire,
        DeadLetter, 
        Defer,
        Reschedule, 
        None
    }
}


namespace ServiceBusUtility
{
   public enum MessageHandling
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


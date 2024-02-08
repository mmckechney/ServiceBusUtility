using Azure.Messaging.ServiceBus;
using System;
using System.Text;

namespace ServiceBusUtility
{
   public static class Extensions
   {
      public static ServiceBusMessage Clone(this ServiceBusReceivedMessage msg)
      {
         ServiceBusMessage msg2 = new ServiceBusMessage()
         {
            Body = msg.Body,
            ContentType = msg.ContentType,
            SessionId = msg.SessionId,
            Subject = msg.Subject,
            To = msg.To,
            MessageId = msg.MessageId
         };
         return msg2;
      }

      public static string DecodeBase64(this string value)
      {
         if (string.IsNullOrWhiteSpace(value))
            return "";

         try
         {
            var valueBytes = System.Convert.FromBase64String(value);
            return Encoding.UTF8.GetString(valueBytes);
         }
         catch (Exception)
         {
            return value;
         }
      }

      public static string EncodeBase64(this string value)
      {
         if (string.IsNullOrWhiteSpace(value))
            return "";

         try
         {
            var valueBytes = Encoding.UTF8.GetBytes(value);
            return Convert.ToBase64String(valueBytes);
         }
         catch (Exception)
         {
            return value;
         }
      }
   }

}

using Microsoft.Extensions.Configuration;
using System.Configuration;
namespace ServiceBusUtility
{
   public class ServiceBusSettings
   {
      IConfiguration config;
      public ServiceBusSettings(IConfiguration config)
      {
         this.config = config;
      }

      private string sbNamespace = string.Empty;
      public string SbNamespace
      {
         get
         {
            if (string.IsNullOrWhiteSpace(sbNamespace) && config?["NamespaceName"] != null)
            {
               sbNamespace = config["NamespaceName"];
            }
            return sbNamespace;
         }
         set
         {
            sbNamespace = value;
         }
      }
      private string _queueName = string.Empty;
      public string QueueName
      {
         get
         {
            if (string.IsNullOrWhiteSpace(_queueName) && config?["QueueName"] != null)
            {
               _queueName = config["QueueName"];
            }
            return _queueName;
         }
         set
         {
            _queueName = value;
         }
      }

      private string _topicName = string.Empty;
      public string TopicName
      {
         get
         {
            if (string.IsNullOrWhiteSpace(_topicName) && config?["TopicName"] != null)
            {
               _topicName = config["TopicName"];
            }
            return _topicName;
         }
         set
         {
            _topicName = value;
         }
      }

      private string _subscriptionName = string.Empty;
     

      public string SubscriptionName
      {
         get
         {
            if (string.IsNullOrWhiteSpace(_subscriptionName) && config?["SubscriptionName"] != null)
            {
               _subscriptionName = config["SubscriptionName"];
            }
            return _subscriptionName;
         }
         set
         {
            _subscriptionName = value;
         }
      }
      public MessageHandling MessageHandling { get; set; } = MessageHandling.Complete;
   }
}

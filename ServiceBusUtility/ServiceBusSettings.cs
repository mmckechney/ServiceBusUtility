using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
namespace ServiceBusUtility
{
    public class ServiceBusSettings
    {
        public string SbNamespace { get; set; } = string.Empty;

        private string _queueName = string.Empty;
        public string QueueName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_queueName))
                {
                    _queueName = ConfigurationManager.AppSettings["QueueName"];
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
                if (string.IsNullOrWhiteSpace(_topicName))
                {
                    _topicName = ConfigurationManager.AppSettings["TopicName"];
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
                if (string.IsNullOrWhiteSpace(_subscriptionName))
                {
                    _subscriptionName = ConfigurationManager.AppSettings["SubscriptionName"];
                }
                return _subscriptionName;
            }
            set
            {
                _subscriptionName = value;
            }
        }
    }
}

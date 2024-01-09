param serviceBusNamespaceName string
param location string = resourceGroup().location

param serviceBusQueueName string = 'demoqueue'
param serviceBusTopicName string = 'demotopic'
param serviceBusSubName string = 'demosub'

resource servicebus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusNamespaceName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
 properties: {
    
  } 
}

resource serviceBusQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: serviceBusQueueName
  parent: servicebus
  properties: {
    enablePartitioning: false
    defaultMessageTimeToLive: 'P14D'
    lockDuration: 'PT1M'
    maxDeliveryCount: 10
    enableBatchedOperations: true
  }
}


resource serviceBusTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  name: serviceBusTopicName
  parent: servicebus
  properties: {
    enablePartitioning: true
  }
}
resource serviceBusSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  name: serviceBusSubName
  parent: serviceBusTopic
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 10
    defaultMessageTimeToLive: 'P14D'
    enableBatchedOperations: true
  }
}

output serviceBusNamespaceName string = servicebus.name
output serviceBusQueueName string = serviceBusQueue.name
output serviceBusTopicName string = serviceBusTopic.name
output serviceBusSubName string = serviceBusSubscription.name

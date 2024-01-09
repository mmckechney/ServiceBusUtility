targetScope = 'subscription'
param resourceGroupName string
param location string
param serviceBusNamespaceName string


resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: resourceGroupName
  location: location
}


module serviceBus 'servicebus.bicep' = {
  name: 'serviceBus'
  scope: rg
  params: {
    location: location
    serviceBusNamespaceName: serviceBusNamespaceName
  }
}

output serviceBusNamespaceName string = serviceBus.outputs.serviceBusNamespaceName
output queueName string = serviceBus.outputs.serviceBusQueueName
output topicName string = serviceBus.outputs.serviceBusTopicName
output subscriptionName string = serviceBus.outputs.serviceBusSubName

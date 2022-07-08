param(
 [Parameter(Mandatory=$True)]
 [string]
 $resourceGroupName,

 [string]
 $location,

 [Parameter(Mandatory=$True)]
 [string]
 $namespaceName
)


#Create or check for existing resource group
$resourceGroup = Get-AzResourceGroup -Name $resourceGroupName -ErrorAction SilentlyContinue
if(!$resourceGroup)
{
    Write-Host "Creating resource group '$resourceGroupName' in location '$location'" -ForegroundColor Cyan
    New-AzResourceGroup -Name $resourceGroupName -Location $location
}
else
{
    Write-Host "Using existing resource group '$resourceGroupName'" -ForegroundColor Cyan
}

$serviceBusNameSpace = $namespaceName
$serviceBusQueue = "demoqueue"
$serviceBusTopic = "demotopic"
$serviceBusSub = "demosub"

# Start the deployment
Write-Host "Starting deployment..." -ForegroundColor Cyan
#####################################################
# Create the Azure Resources 
#####################################################
if($null -eq (Get-AzServiceBusNamespace -ResourceGroupName $resourceGroupName -Name $serviceBusNameSpace -ErrorAction SilentlyContinue))
{
    Write-Host "Creating Service Bus Namespace: $($serviceBusNameSpace)"
    New-AzServiceBusNamespace -ResourceGroupName $resourceGroupName -Name $serviceBusNameSpace -Location $location -SkuName  "Standard" 
}

if($null -eq (Get-AzServiceBusQueue -ResourceGroupName $resourceGroupName -Namespace $serviceBusNameSpace -Name $serviceBusQueue -ErrorAction SilentlyContinue))
{
    Write-Host "Creating Service Bus Queue: $($serviceBusQueue)"
    New-AzServiceBusQueue -ResourceGroupName $resourceGroupName -Name $serviceBusQueue -Namespace $serviceBusNameSpace
}

if($null -eq (Get-AzServiceBusTopic -ResourceGroupName $resourceGroupName -Namespace $serviceBusNameSpace -Name $serviceBusTopic -ErrorAction SilentlyContinue))
{
    Write-Host "Creating Service Bus Topic : $($serviceBusTopic)"
    New-AzServiceBusTopic -ResourceGroupName $resourceGroupName -Name $serviceBusTopic -Namespace $serviceBusNameSpace -EnablePartitioning $true
}

if($null -eq (Get-AzServiceBusSubscription -ResourceGroupName $resourceGroupName -Namespace $serviceBusNameSpace -Topic $serviceBusTopic -Name $serviceBusSub -ErrorAction SilentlyContinue))
{
    Write-Host "Creating Service Bus Topic Subscription : $($serviceBusSub)"
    New-AzServiceBusSubscription -ResourceGroupName $resourceGroupName -Topic $serviceBusTopic -Name $serviceBusSub -Namespace $serviceBusNameSpace
}`

Write-Host "Deployment Complete" -ForegroundColor Cyan
$sbString = (Get-AzServiceBusKey -ResourceGroupName $resourceGroupName -Namespace $serviceBusNameSpace -Name "RootManageSharedAccessKey").PrimaryConnectionString



#####################################################
# Output the values for reference
#####################################################
Write-Output "You can use the following information when exercising the utility against your sample Service Bus:"


Write-Output "For App Service settings:"
Write-Host "Connection String: `t $sbString" -ForegroundColor Green
Write-Host "Namespace Name: `t $serviceBusNameSpace" -ForegroundColor Green
Write-Host "Queue Name: `t`t $serviceBusQueue" -ForegroundColor Green
Write-Host "Topic Name: `t`t $serviceBusTopic" -ForegroundColor Green
Write-Host "Subscription Name: `t $serviceBusSub" -ForegroundColor Green


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

$error.Clear()
$ErrorActionPreference = 'Stop'

Write-Host "Creating Azure resources" -ForegroundColor Cyan
$results = (az deployment sub create --location $location  --template-file ./infra/main.bicep --parameters location=$location resourceGroupName=$resourceGroupName serviceBusNamespaceName=$namespaceName) | ConvertFrom-Json -Depth 10
		
if(!$?){ exit }

$serviceBusNameSpace = $results.properties.outputs.serviceBusNamespaceName.value
$serviceBusQueue = $results.properties.outputs.queueName.value
$serviceBusTopic = $results.properties.outputs.topicName.value
$serviceBusSub = $results.properties.outputs.subscriptionName.value

$sbConnString = az servicebus namespace authorization-rule keys list -n "RootManageSharedAccessKey" --namespace-name $serviceBusNameSpace -g $resourceGroupName --query primaryConnectionString -o tsv

$settings = @{
    "Namespace" = $serviceBusNameSpace
    "QueueName" = $serviceBusQueue
    "TopicName" = $serviceBusTopic
    "SubscriptionName" = $serviceBusSub
    "NamespaceName" = $serviceBusNameSpace
}
Write-Host -ForegroundColor Green "Creating console app local.settings.json"
$settings = ConvertTo-Json $settings -Depth 100
$settings | Out-File -FilePath ".\ServiceBusUtility\local.settings.json"

Write-Host "Building Service Bus Utility project" -ForegroundColor Cyan
dotnet publish .

#####################################################
# Output the values for reference
#####################################################
Write-Host "You can use the following information when exercising the utility against your sample Service Bus (they have also been saved in the local.settings.json file):" -ForegroundColor Cyan

Write-Host "For App Service settings:" -ForegroundColor Cyan
Write-Host "Connection String: `t $sbConnString" -ForegroundColor Green
Write-Host "Namespace Name: `t $serviceBusNameSpace" -ForegroundColor Green
Write-Host "Queue Name: `t`t $serviceBusQueue" -ForegroundColor Green
Write-Host "Topic Name: `t`t $serviceBusTopic" -ForegroundColor Green
Write-Host "Subscription Name: `t $serviceBusSub" -ForegroundColor Green


Write-Host "Asking for Admin escallation to create link to executable..." -ForegroundColor Cyan
$exelinkPath = Join-Path -Path (Resolve-Path .).Path -ChildPath "sbu.exe"
$exetargetPath = (Resolve-Path .\ServiceBusUtility\bin\Release\net8.0\win-x64\publish\sbu.exe).Path

# Self-elevate the script if required
if (-Not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] 'Administrator')) {
    if ([int](Get-CimInstance -Class Win32_OperatingSystem | Select-Object -ExpandProperty BuildNumber) -ge 6000) {
     $CommandLine =  "New-Item -ItemType HardLink -Path $($exelinkPath)  -Target $($exetargetPath);"
     Start-Process -FilePath PowerShell.exe -Verb Runas -WindowStyle Minimized -ArgumentList $CommandLine

    }
   }



Write-Host "Setting connection string (run: `sbu connection clear` if you want to use RBAC authentication)" -ForegroundColor Blue
.\ServiceBusUtility\bin\Release\net8.0\win-x64\publish\sbu.exe connection set -c $sbConnString --exit

Write-Host "Running Service Bus Utility" -ForegroundColor Cyan
.\ServiceBusUtility\bin\Release\net8.0\win-x64\publish\sbu.exe
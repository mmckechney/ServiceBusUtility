# Service Bus Utility

This command line tool and source code can be used both as a utility to manage messages on an Azure Service Bus Queue or Topic as well as a learning tool to understand the Azure.Messaging.ServiceBus SDKs

## What's included

Utility and sample code for accessing using Azure Service Bus

- **ServiceBusUtility** - A command line tool to send messages to and read messages from an Azure Service Bus Queue or Topic/Subscription. Example commands:
  - `sbu topic send --topic-name <name> --sub <name> --count <int> --delay 300` : Sends the specified number of messages to the service bus topic, scheduling the message for a specified number of seconds in the future
  - `sbu queue read --queue-name <name> --messagehandling  <Abandon|Complete|DeadLetter|None|Reschedule> ` : Reads messages from the Service Bus, with the specified handling of the message.

  To see the full list of command line options, use `sbu -h`

---

## Setup

1. Build the solution 
2. From a PowerShell command, run the `setup.ps1` script. This will create the Service Bus Namespace, and a queue, topic and subscription for you to test against. 

    ``` PowerShell
    Connect-AzAccount
    ./SetUp.ps1 -resourceGroupName <resource group name> -location <Azure region> -namespaceName <unique name>
    ```

3. Either assign your Azure AAD account the RBAC roles of `Azure Service Bus Data Receiver` and `Azure Service Bus Data Sender` and/or use the `sbu connection set -c` command with the connection string displayed from the `setup.ps1` command

----

## Running

After running the setup (above), you can send messages to the Service Bus by navigating to the output directory of the utility project `\ServiceBusUtility\ServiceBusUtility\bin\Debug\net6.0`

From here, try running the send command

``` bash
sbu queue send -q demoqueue --count 10

Starting with args: send -c 10
Loop 001: Sent message '1812e4a2-bd6e-4f6e-97f2-64523ff2ded5' to queue 'demoqueue'
Loop 002: Sent message '4596609c-8b47-4685-84ed-5e1dbf4522a9' to queue 'demoqueue'
Loop 003: Sent message '9fd59a8c-a7a4-46cc-a9d9-03c44ebfc61b' to queue 'demoqueue'
Loop 004: Sent message '5cfd2824-56bd-412b-b056-de524883ddfb' to queue 'demoqueue'
Loop 005: Sent message 'd14fee7c-3c88-4602-baad-9ff9b3f1adbe' to queue 'demoqueue'
Loop 006: Sent message '325c948f-ecba-4637-8af5-1351fb4d2fa4' to queue 'demoqueue'
Loop 007: Sent message '9daf0c44-bb3c-4ea2-be91-79180f90b22d' to queue 'demoqueue'
Loop 008: Sent message 'e8d4a14c-5898-494a-87f2-7361d9783996' to queue 'demoqueue'
Loop 009: Sent message '5b17e9d0-cc66-439b-8267-b09d0a531e3b' to queue 'demoqueue'
Loop 010: Sent message '71b8d368-5ab7-4612-a317-37d8e0d5c414' to queue 'demoqueue'
```

Next, try receiving messages by running a `read` command. This will open a received that will retrieve message until you hit the \<Enter> key

```  
sbu queue read -q demoqueue 

Using saved connection string to connect to service bus...
Starting message read for queue 'demoqueue'
Press <Enter> so stop reading and exit...

----------
Sequence Number: 11
Message State:   Active
Message body:    This is a new test message for queue created at 7/8/2022 7:40:49 PM
Message id:      7f06f54c-a62b-484f-8c95-44fce2dfddb3
ScheduledTime:   1/1/0001 12:00:00 AM +00:00
EnqueuedTime:    7/8/2022 7:40:52 PM +00:00
ExpiresAt:
TimeToLive:      15372286728.091293
DeliveryCount:   1
Messages rec'd:  1
Finishing message processing. Message handling set to Complete and removed from queue

----------
Sequence Number: 12
Message State:   Active
Message body:    This is a new test message for queue created at 7/8/2022 7:40:51 PM
Message id:      b1e5e330-b3cd-4b43-851a-ae197e797f45
ScheduledTime:   1/1/0001 12:00:00 AM +00:00
EnqueuedTime:    7/8/2022 7:40:52 PM +00:00
ExpiresAt:
TimeToLive:      15372286728.091293
DeliveryCount:   1
Messages rec'd:  2
Finishing message processing. Message handling set to Complete and removed from queue
----
```

## Experimenting

To experiment with different Service Bus message handling, you can leverage the `read` command 

Now, try send a message to the Service Bus via:

``` bash
sbu queue send --queue-name demoqueue --count 1
```

### **Demonstrate "Abandon"**

``` bash
sbu queue read --queue-name demoqueue --messagehandling Abandon
```
Abandoned messages are put back into the queue with there delivery count incremented. Once the delivery count hits the limit set on the queue, it will be sent to the Dead Letter queue

```
Starting message read for queue 'demoqueue'
Press <Enter> so stop reading and exit...

----------
Sequence Number: 31
Message State:   Active
Message body:    This is a new test message for queue created at 7/8/2022 7:54:40 PM
Message id:      f786138f-1228-4fe9-ac92-2309939b53b8
ScheduledTime:   1/1/0001 12:00:00 AM +00:00
EnqueuedTime:    7/8/2022 7:54:42 PM +00:00
ExpiresAt:
TimeToLive:      15372286728.091293
DeliveryCount:   1
Messages rec'd:  1
Message handling set to Abandon. Stopping message processing, putting back into queue with Deliver Count +1

----------
Sequence Number: 31
Message State:   Active
Message body:    This is a new test message for queue created at 7/8/2022 7:54:40 PM
Message id:      f786138f-1228-4fe9-ac92-2309939b53b8
ScheduledTime:   1/1/0001 12:00:00 AM +00:00
EnqueuedTime:    7/8/2022 7:54:42 PM +00:00
ExpiresAt:
TimeToLive:      15372286728.091293
DeliveryCount:   2
Messages rec'd:  2
Message handling set to Abandon. Stopping message processing, putting back into queue with Deliver Count +1
```
### **Demonstrate "Lock Expiration"**

```
sbu queue read --queue-name demoqueue --messagehandling LockExpire
```

The default message lock time is an attribute that can be set on the queue. You can extend the default lock with your Queue client. You should notice that the actual lock time will likely be longer than the requested time (but it won't be shorter). Once the lock expires, the message will get requeued with its delivery count incremented by 1

```
----------
Sequence Number: 32
Message State:   Active
Message body:    This is a new test message for queue created at 7/8/2022 7:55:51 PM
Message id:      48db8404-dd0f-481b-b414-eefb4bc4c37f
ScheduledTime:   1/1/0001 12:00:00 AM +00:00
EnqueuedTime:    7/8/2022 7:55:53 PM +00:00
ExpiresAt:
TimeToLive:      15372286728.091293
DeliveryCount:   1
Messages rec'd:  1

----------
Sequence Number: 32
Message State:   Active
Message body:    This is a new test message for queue created at 7/8/2022 7:55:51 PM
Message id:      48db8404-dd0f-481b-b414-eefb4bc4c37f
ScheduledTime:   1/1/0001 12:00:00 AM +00:00
EnqueuedTime:    7/8/2022 7:55:53 PM +00:00
ExpiresAt:
TimeToLive:      15372286728.091293
DeliveryCount:   2
Messages rec'd:  2
```

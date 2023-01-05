using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Threading;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Net.NetworkInformation;
using Microsoft.Azure.Amqp;
using System.Diagnostics;
using Microsoft.VisualBasic;
using Azure.Messaging.ServiceBus;
using System.ComponentModel.DataAnnotations;
using System.Collections;
using System.Runtime.CompilerServices;
using Microsoft.Azure.Amqp.Framing;
using System.Data;
using Azure.Identity;
using System.Net.Sockets;
using System.IO;
using System.Reflection.Metadata.Ecma335;

namespace ServiceBusUtility
{
    class Program
    {
        static void Main(string[] args)
        {
            connectionString = GetConnectionString();
            var parser = CommandBuilder.BuildCommandLine();
            Task<int> val = parser.InvokeAsync(args);
            val.Wait();

        }

        private static int messagesReceived = 0;
        private static ServiceBusSender _sender = null;
        private static ServiceBusClient _client = null;
        private static string connectionString ="";
        private static string activeQueueOrTopic = "";
        private static ServiceBusSettings sbSettings = null;
        static ServiceBusClient Client
        {
            get
            {
                if (_client == null)
                {
                    if (!string.IsNullOrWhiteSpace(sbSettings.SbNamespace))
                    {
                        if (!sbSettings.SbNamespace.ToLower().EndsWith("servicebus.windows.net")) sbSettings.SbNamespace = sbSettings.SbNamespace + ".servicebus.windows.net";
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Using AAD RBAC authentication to connect to service bus...");
                        Console.ForegroundColor = ConsoleColor.White;
                        _client = new ServiceBusClient(sbSettings.SbNamespace, new AzureCliCredential());
                    }  
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Using saved connection string to connect to service bus...");
                        Console.ForegroundColor = ConsoleColor.White;
                        _client = new ServiceBusClient(connectionString);

                    }
                }
                    return _client;
                
            }
        }
        static ServiceBusSender Sender
        {
            get
            {
                if(_sender == null)
                {
                    if (string.IsNullOrWhiteSpace(activeQueueOrTopic)) activeQueueOrTopic = sbSettings.QueueName;
                    _sender = Client.CreateSender(activeQueueOrTopic);
                }
                return _sender;
            }
        }
        static ServiceBusReceiver GetReceiver(QueueType queueType)
        {

            switch (queueType)
            {

                case QueueType.TopicSubscription:
                    return Client.CreateReceiver(sbSettings.TopicName, sbSettings.SubscriptionName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock });
                case QueueType.TopicScheduled:
                    return Client.CreateReceiver(sbSettings.TopicName,  new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock });
                case QueueType.DeadLetterQueue:
                    return Client.CreateReceiver(sbSettings.QueueName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock , SubQueue = SubQueue.DeadLetter});
                case QueueType.DeadLetterSubscription:
                    return Client.CreateReceiver(sbSettings.TopicName, sbSettings.SubscriptionName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock, SubQueue = SubQueue.DeadLetter });
                case QueueType.Queue:
                default:
                    return Client.CreateReceiver(sbSettings.QueueName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock });
            } 
        }
        private static void IncrementMessagesReceived()
        {
            Interlocked.Increment(ref messagesReceived);
        }
        static (ServiceBusProcessor, string) GetProcessor(QueueType queueType)
        {
            var options = new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 2
            };

            string message;
            ServiceBusProcessor processor = null;
            switch (queueType)
            {

                case QueueType.TopicSubscription:
                    message = $"Starting message read for topic/subscription '{sbSettings.TopicName}/{sbSettings.SubscriptionName}'";
                    processor = Client.CreateProcessor(sbSettings.TopicName, sbSettings.SubscriptionName, options);
                    break;
                case QueueType.DeadLetterSubscription:
                    options.SubQueue = SubQueue.DeadLetter;
                    message = $"Starting message read for topic/subscription '{sbSettings.TopicName}/{sbSettings.SubscriptionName}' deadletter sub-queue";
                    processor = Client.CreateProcessor(sbSettings.TopicName, sbSettings.SubscriptionName, options);
                    break;
                case QueueType.DeadLetterQueue:
                    options.SubQueue = SubQueue.DeadLetter;
                    message = $"Starting message read for queue '{sbSettings.QueueName}' deadletter sub-queue";
                    processor = Client.CreateProcessor(sbSettings.QueueName, options);
                    break;
                case QueueType.Queue:
                default:
                    message = $"Starting message read for queue '{sbSettings.QueueName}'";
                    processor =  Client.CreateProcessor(sbSettings.QueueName, options);
                    break;
            }
            return (processor, message);
        }


        internal static async Task PeekMessages(int count, QueueType queueType, bool scheduled, ServiceBusSettings sbSettings)
        {
            Program.sbSettings = sbSettings;
            if (queueType == QueueType.TopicSubscription && scheduled)
            {
                queueType = QueueType.TopicScheduled;
            }
            ServiceBusReceiver receiver = GetReceiver(queueType);

            var msgs = await receiver.PeekMessagesAsync(count);
            var num = 1;
            foreach(var m in msgs)
            {
                WriteMessageOutput(m, num);
                num++;
            }
            Console.WriteLine("----------");
            Console.WriteLine($"{msgs.Count} messages peeked");
        }

        internal static async Task SendMessages(int count, int wait, int delay, QueueType queueType, ServiceBusSettings sbSettings, bool quiet)
        {
            Stopwatch s = new Stopwatch();
            
            try
            {
                Console.WriteLine($"Starting send of {count} messages...");
                Program.sbSettings = sbSettings;
                Program.activeQueueOrTopic = queueType == QueueType.Queue ? sbSettings.QueueName : sbSettings.TopicName;
                s.Start();

                if (wait == 0)
                {
                    List<Task<string>> msgs = new List<Task<string>>();
                    for (int i = 0; i < count; i++)
                    {
                        msgs.Add(SendMessage(i, delay, quiet));
                    }
                    Task.WaitAll(msgs.ToArray());

                    if (!quiet)
                    {
                        foreach (var t in msgs)
                        {
                            if (t.Result.Length > 0)
                            {
                                Console.WriteLine(t.Result);
                            }
                        }
                    }
                }
                else
                {

                    for (int i = 0; i < count; i++)
                    {
                        var output = await SendMessage(i, delay, quiet);
                        Console.WriteLine(output);
                        Thread.Sleep(wait);
                    }
                }
                s.Stop();
                Console.WriteLine($"Sent {count} messages in {s.Elapsed.TotalSeconds.ToString("#.##")} seconds");
            }
            catch (Exception exe)
            {
                HandleServiceBusException(exe);
            }
        }

        internal static async Task<string> SendMessage(int counter, int delay, bool quiet)
        {
            var sbMessages = new ServiceBusMessage($"This is a new test message for queue created at {DateTime.UtcNow}");
            string id = Guid.NewGuid().ToString();
            sbMessages.MessageId = id;

            if (delay != 0)
            {
                sbMessages.ScheduledEnqueueTime = DateTime.UtcNow.AddSeconds(delay);
                var sequenceNumber = await Sender.ScheduleMessageAsync(sbMessages, sbMessages.ScheduledEnqueueTime);
                if (!quiet)
                {
                    return $"Loop {(counter + 1).ToString().PadLeft(4, '0')}: Sent message '{id}' to '{activeQueueOrTopic}'. Sequence #{sequenceNumber} scheduled for {sbMessages.ScheduledEnqueueTime.ToString("yyyy-MM-dd HH:mm:ss fff")}";
                }
            }
            else
            {
                await Sender.SendMessageAsync(sbMessages);
                if (!quiet)
                {
                    return $"Loop {(counter+1).ToString().PadLeft(4, '0')}: Sent message '{id}' to '{activeQueueOrTopic}'.";
                }
            }
            return string.Empty;
        }
        static MessageHandling handling;
        internal static async Task ReadMessage(MessageHandling messagehandling, long? sequenceNumber, QueueType queueType, int delay, bool scheduled, ServiceBusSettings sbSettings)
        {
            try
            {
                Program.sbSettings = sbSettings;
                Program.activeQueueOrTopic = queueType == QueueType.Queue ? sbSettings.QueueName : sbSettings.TopicName;
                Program.handling = messagehandling;
                if (!sequenceNumber.HasValue)
                {
                    InitializeReceiver(queueType);
                    //This will keep the app alive as the messages are retrieved and "processed"
                    Console.Read();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"Total Messages Received: {messagesReceived}");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                else
                {
                    await ReadSpecificMessage(sequenceNumber.Value, queueType, handling, delay, scheduled);
                }
            }
            catch(Exception exe)
            {
                HandleServiceBusException(exe);
            }
            return;
        }
        private static async Task ReadSpecificMessage(long sequenceNumber, QueueType queueType, MessageHandling handling, int delay, bool scheduled)
        {

            if (queueType == QueueType.TopicSubscription && scheduled)
            {
                queueType = QueueType.TopicScheduled;
            }
            ServiceBusReceivedMessage message = null;
            ServiceBusReceiver receiver = GetReceiver(queueType);
           
            try
            {
                message = await receiver.ReceiveDeferredMessageAsync(sequenceNumber);
            }
            catch (ServiceBusException exe)
            {
                
                Console.WriteLine(exe.Message);
                Console.WriteLine("Trying to peek for the message");
                message = await receiver.PeekMessageAsync(sequenceNumber);
            }
            if (message != null)
            {
                WriteMessageOutput(message, 1);

                if(message.State == ServiceBusMessageState.Active)
                {
                    Console.WriteLine($"Because message #{message.SequenceNumber} is Active and Peeked, no action can be taken.");
                    return;
                }
                switch (handling)
                {
                    case MessageHandling.Complete:
                        
                        switch (message.State)
                        {
                            case ServiceBusMessageState.Scheduled:
                                Console.WriteLine("Message handling set to Complete. Finishing message processing. Scheduled message has been cancelled");
                                await Sender.CancelScheduledMessageAsync(message.SequenceNumber);
                                break;
                            case ServiceBusMessageState.Deferred:
                                Console.WriteLine("Message handling set to Complete. Finishing message processing. Deferred message getting removed from queue");
                                await receiver.CompleteMessageAsync(message);
                                break;
                        }
                        break;
                    case MessageHandling.DeadLetter:
                        Console.WriteLine("Message handling set to DeadLetter. Stopping message processing, message going to DeadLetter Queue");
                        await receiver.DeadLetterMessageAsync(message, "Deadletter by request");
                        break;
                    case MessageHandling.Abandon:
                        Console.WriteLine("Message handling set to Abandon. Stopping message processing, putting back into queue with Deliver Count +1");
                        await receiver.AbandonMessageAsync(message);
                        break;
                    case MessageHandling.Defer:
                        Console.WriteLine($"Message with sequence #{message.SequenceNumber} is getting deferred");
                        await receiver.DeferMessageAsync(message);
                        break;
                    case MessageHandling.Reschedule:
                        if (delay == 0) delay = 60;
                        var schedule = DateTime.UtcNow.AddSeconds(delay);
                        var newMsg = message.Clone();
                        newMsg.ScheduledEnqueueTime = schedule;
                        var newSequenceNumber = await Sender.ScheduleMessageAsync(newMsg, schedule);
                        Console.WriteLine($"Rescheduled message with sequence #{message.SequenceNumber} as new messages with sequence #{newSequenceNumber} scheduled for {schedule}");
                        switch(message.State)
                        {
                            case ServiceBusMessageState.Active:
                                break;
                            case ServiceBusMessageState.Scheduled:
                                await Sender.CancelScheduledMessageAsync(message.SequenceNumber);
                                break;
                            case ServiceBusMessageState.Deferred:
                                await receiver.CompleteMessageAsync(message);
                                break;
                        }
                        Console.WriteLine($"Marking original message with sequence #{message.SequenceNumber} as Complete");
                        break;
                    case MessageHandling.None:
                    default:
                        Console.WriteLine($"Message Handling set to None. No action taken on message");
                        break;
                }
            }
            else
            {
                Console.Write($"Unable to find message with Sequence Number {sequenceNumber}");
            }
        }
        
        private static void WriteMessageOutput(ServiceBusReceivedMessage message, int messageNum)
        {
            StringBuilder sb = new StringBuilder();
            var amqp = message.GetRawAmqpMessage();
            sb.AppendLine("");
            sb.AppendLine("----------");
            sb.AppendLine($"Sequence Number: {message.SequenceNumber}");
            sb.AppendLine($"Message State:\t {message.State.ToString()}");
            sb.AppendLine($"Message body:\t {Encoding.UTF8.GetString(message.Body)}");
            sb.AppendLine($"Message id:\t {message.MessageId}");
            sb.AppendLine($"ScheduledTime:\t {message.ScheduledEnqueueTime}");
            sb.AppendLine($"EnqueuedTime:\t {message.EnqueuedTime}");
            sb.AppendLine($"ExpiresAt:\t {amqp.Properties.AbsoluteExpiryTime}");
            sb.AppendLine($"TimeToLive:\t {message.TimeToLive.TotalMinutes}");
            sb.AppendLine($"DeliveryCount:\t {amqp.Header.DeliveryCount}");
            sb.Append($"Messages rec'd:\t {messageNum}");
            Console.WriteLine(sb.ToString());
        }

        private static void HandleServiceBusException(Exception exe)
        {

            Console.ForegroundColor = ConsoleColor.Red;
            if(exe is ServiceBusException)
            {
                var sbe = (ServiceBusException)exe;
                if (sbe.InnerException != null && sbe.InnerException is SocketException)
                {
                    var se = (SocketException)sbe.InnerException;
                    if (se.SocketErrorCode == SocketError.HostNotFound)
                    {
                        Console.WriteLine("Unable to find Service Bus namespace. Please validate that you have entered the correct value or have set the proper connection string");
                    }
                }
                else if (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
                {
                    Console.WriteLine("The specified queue or topic does not exist in the namespace. Please change the value and try again.");
                }

            }
            else if (exe is UnauthorizedAccessException)
            {
                Console.WriteLine($"You do not have send/receive RBAC access to this Service Bus.{Environment.NewLine}Please make sure your identity has 'Azure Service Bus Data Receiver' and 'Azure Service Bus Data Sender' role assignments{Environment.NewLine}To use a connection string, remove the `--ns` argument and set a connection string with 'sbu connection set' command");
            }
            else if(exe is ArgumentException)
            {
                var aex = (ArgumentException)exe;
                if(aex.ParamName == "connectionString")
                {
                    Console.WriteLine("Please either specify a Service Bus Namespace argument value (--ns) or set a connection string with 'sbu connection set' command");
                }
                else if(aex.ParamName == "entityPath")
                {
                    Console.WriteLine("Please specify an appropriate Queue (-q) or Topic (-t) argument value");
                }
                else
                {
                    Console.WriteLine(aex.Message);
                }
            }
            else
            {
                Console.WriteLine(exe.Message);
            }
            Console.ForegroundColor = ConsoleColor.White;
            Environment.Exit(1);
        }

        static void InitializeReceiver(QueueType queueType)
        {
            (var processor, var message) = GetProcessor(queueType);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(message);
            Console.WriteLine("Press <Enter> so stop reading and exit...");
            Console.ForegroundColor = ConsoleColor.White;

            processor.ProcessMessageAsync += ProcessMessageAsync;
            processor.ProcessErrorAsync += ExceptionReceivedHandler;

            processor.StartProcessingAsync();
        }
        private static async Task ProcessMessageAsync(ProcessMessageEventArgs arg)
        {
            var message = arg.Message;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            IncrementMessagesReceived();
            int msgNum = messagesReceived;
            WriteMessageOutput(message, msgNum);
            switch (Program.handling)
            {
                case MessageHandling.Complete:
                    Console.WriteLine("Finishing message processing. Message handling set to Complete and removed from queue");
                    await arg.CompleteMessageAsync(message, arg.CancellationToken);
                    break;
                case MessageHandling.DeadLetter:
                    Console.WriteLine("Message handling set to DeadLetter. Stopping message processing, message going to DeadLetter Queue");
                    await arg.DeadLetterMessageAsync(message, "Deadletter by request");
                    break;
                case MessageHandling.Abandon:
                    Console.WriteLine("Message handling set to Abandon. Stopping message processing, putting back into queue with Deliver Count +1");
                    await arg.AbandonMessageAsync(message);
                    break;
                case MessageHandling.Defer:
                    var schedule = DateAndTime.Now.AddSeconds(30);
                    Console.WriteLine($"Message with sequence #{message.SequenceNumber} is getting deferred.");
                    await arg.DeferMessageAsync(message);
                    break;
            }

        }
        static Task ExceptionReceivedHandler(ProcessErrorEventArgs args)
        {
            HandleServiceBusException(args.Exception);
            Console.WriteLine($"Message handler encountered an exception {args.Exception}.");
            return Task.CompletedTask;
        }


        internal static string GetConnectionString()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var csFile = Path.Combine(appDataPath, "cs.txt");
            if(File.Exists(csFile))
            {
                return File.ReadAllText(csFile).DecodeBase64();
            }
            else
            {
                return "";
            }
            
        }
        internal static void SetConnectionString(string connectionString)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var csFile = Path.Combine(appDataPath, "cs.txt");
            File.WriteAllText(csFile, connectionString.EncodeBase64());
        }
        internal static void ClearConnectionString(object obj)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var csFile = Path.Combine(appDataPath, "cs.txt");
            if(File.Exists(csFile))
            {
                File.Delete(csFile);
            }    
        }
    }
}


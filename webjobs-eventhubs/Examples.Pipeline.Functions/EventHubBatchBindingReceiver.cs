using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Examples.Pipeline.Functions
{
    public class EventHubBatchBindingReceiver
    {
        readonly TelemetryClient _telemetry;

        public EventHubBatchBindingReceiver(TelemetryConfiguration telemetryConfiguration)
        {
            _telemetry = new TelemetryClient(telemetryConfiguration);
        }

        [FunctionName("EventHubBatchBindingReceiver")]
        public async Task Run(
            [EventHubTrigger("numbers-batched-binding", Connection = "EventHubConnectionString")] EventData[] events,
            ILogger log,
            PartitionContext partitionContext,
            [EventHub("numbers-batched-binding-2", Connection = "EventHubConnectionString")]IAsyncCollector<EventData> outputEvents)
        {
            log.LogInformation($"EventHubBatchBindingReceiver: Batch count = {events.Length}, Partition = {partitionContext.PartitionId}, Owner = {partitionContext.Owner}");

            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

                    // Replace these two lines with your processing logic.
                    log.LogInformation($"EventHubBatchBindingReceiver: message = {messageBody}");

                    _telemetry.TrackEvent(
                        "EventHubBatchBindingReceiver/EventProcessed",
                        properties: new Dictionary<string, string>
                        {
                            { "partitionId", partitionContext.PartitionId },
                            { "owner", partitionContext.Owner },
                            { "sequenceNumber", eventData.SystemProperties.SequenceNumber.ToString() }
                        });

                    // send processed message to next hub
                    var newEventData = new EventData(Encoding.UTF8.GetBytes(messageBody));
                    await outputEvents.AddAsync(newEventData);
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                    _telemetry.TrackException(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}

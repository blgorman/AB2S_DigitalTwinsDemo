using Azure.Messaging.EventHubs.Consumer;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Common.Exceptions;
using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceSimulator
{
    public static class AzureIoTHub
    {
        private static int taskDelay = 10 * 1000;
        //#####################################################################
        //Replace Hub Name, hub Shared Access Key, and then the device shared access keys
        //  if you have different names for your devices, update those as well
        //#####################################################################
        private static string hubName = "your-hub-name-here";
        private static string hubSharedAccessKey = "your-hub-owner-shared-access-key";

        private static string device1Name = "trailer_sensor_11111101";
        private static string device1SharedAccessKey = "your-device-shared-access-key";
        private static string device2Name = "trailer_sensor_22222201";
        private static string device2SharedAccessKey = "your-device-shared-access-key";
        private static string device3Name = "trailer_sensor_33333301";
        private static string device3SharedAccessKey = "your-device-shared-access-key";

        //these are composed from the above values
        private static string iotHubConnectionString = @$"HostName={hubName}.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey={hubSharedAccessKey}";

        private static string deviceConnectionString1 = $"HostName={hubName}.azure-devices.net;DeviceId={device1Name};SharedAccessKey={device1SharedAccessKey}";
        private static string deviceConnectionString2 = $"HostName={hubName}.azure-devices.net;DeviceId={device2Name};SharedAccessKey={device2SharedAccessKey}";
        private static string deviceConnectionString3 = $"HostName={hubName}.azure-devices.net;DeviceId={device3Name};SharedAccessKey={device3SharedAccessKey}";
        
        //#####################################################################
        //Replace these for the correct device simulation
        //#####################################################################
        private static string deviceConnectionString = deviceConnectionString1;
        private static string deviceId = device1Name;

        public static async Task<string> CreateDeviceIdentityAsync(string deviceName)
        {
            var registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            var device = new Device(deviceName);
            try
            {
                device = await registryManager.AddDeviceAsync(device);
            }
            catch (DeviceAlreadyExistsException)
            {
                device = await registryManager.GetDeviceAsync(deviceName);
            }

            return device.Authentication.SymmetricKey.PrimaryKey;
        }

        public static async Task SendDeviceToCloudMessageAsync(CancellationToken cancelToken)
        {
            var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString);

            double avgTemperature = 2.0D;
            var rand = new Random();

            while (!cancelToken.IsCancellationRequested)
            {
                double currentTemperatureLocal = avgTemperature + rand.NextDouble() * 4;

                if (DateTime.Now.Millisecond % 2 == 0)
                {
                    currentTemperatureLocal = 37.0D;
                }

                var telemetryDataPoint = new TrailerTelemetry
                {
                    id = deviceId,
                    temperature = currentTemperatureLocal,
                    temperatureAlert = currentTemperatureLocal > 30.0D
                };
                var messageString = JsonSerializer.Serialize(telemetryDataPoint);

                var message = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(messageString))
                {
                    ContentType = "application/json",
                    ContentEncoding = "utf-8"
                };
                await deviceClient.SendEventAsync(message);
                Console.WriteLine($"{DateTime.Now} > Sending message: {messageString}");
                
                //Keep this value above 1000 to keep a safe buffer above the ADT service limits
                //See https://aka.ms/adt-limits for more info
                await Task.Delay(taskDelay);
            }
        }

        public static async Task<string> ReceiveCloudToDeviceMessageAsync()
        {
            var oneSecond = TimeSpan.FromSeconds(1);
            var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString);

            while (true)
            {
                var receivedMessage = await deviceClient.ReceiveAsync();
                if (receivedMessage == null)
                {
                    await Task.Delay(oneSecond);
                    continue;
                }

                var messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                await deviceClient.CompleteAsync(receivedMessage);
                return messageData;
            }
        }

        public static async Task ReceiveMessagesFromDeviceAsync(CancellationToken cancelToken)
        {
            try
            {
                string eventHubConnectionString = await IotHubConnection.GetEventHubsConnectionStringAsync(iotHubConnectionString);
                await using var consumerClient = new EventHubConsumerClient(
                    EventHubConsumerClient.DefaultConsumerGroupName,
                    eventHubConnectionString);

                await foreach (PartitionEvent partitionEvent in consumerClient.ReadEventsAsync(cancelToken))
                {
                    if (partitionEvent.Data == null) continue;

                    string data = Encoding.UTF8.GetString(partitionEvent.Data.Body.ToArray());
                    Console.WriteLine($"Message received. Partition: {partitionEvent.Partition.PartitionId} Data: '{data}'");
                }
            }
            catch (TaskCanceledException) { } // do nothing
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading event: {ex}");
            }
        }
    }
}

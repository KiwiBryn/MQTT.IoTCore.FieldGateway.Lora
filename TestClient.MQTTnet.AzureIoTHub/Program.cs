﻿/*
    Copyright ® 2019 November devMobile Software, All Rights Reserved
 
    MIT License

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE

	 A quick and dirty test client to explore how Azure IoT Hub MQTT connectivity works
 */
namespace devMobile.Mqtt.TestClient.AzureIoTHub
{
   using System;
   using System.Diagnostics;
   using System.Globalization;
   using System.Net;
   using System.Security.Cryptography;
   using System.Text;
   using System.Threading;
   using System.Threading.Tasks;
   using System.Web;
   using MQTTnet;
   using MQTTnet.Client;
   using MQTTnet.Client.Disconnecting;
   using MQTTnet.Client.Options;
   using MQTTnet.Client.Receiving;
   using Newtonsoft.Json;
   using Newtonsoft.Json.Linq;

   class Program
   {
      private static IMqttClient mqttClient = null;
      private static IMqttClientOptions mqttOptions = null;
      private static string server;
      private static string username;
      private static string password;
      private static string clientId;
      private static string topicD2C;
      private static string topicC2D;

      static void Main(string[] args)
      {
         MqttFactory factory = new MqttFactory();
         mqttClient = factory.CreateMqttClient();

         if (args.Length != 3)
         {
            Console.WriteLine("[AzureIoTHubHostName] [deviceID] [SASKey]");
            Console.WriteLine("Press <enter> to exit");
            Console.ReadLine();
            return;
         }

         server = args[0];
         clientId = args[1];
         password = args[2];

         username = $"{server}/{clientId}/api-version=2018-06-30";
         topicD2C = $"devices/{clientId}/messages/events/";
         topicC2D = $"devices/{clientId}/messages/devicebound/#";

         Console.WriteLine($"MQTT Server:{server} Username:{username} ClientID:{clientId}");

         string token = generateSasToken($"{server}/devices/{clientId}", password, "", new TimeSpan(1,0,0,0));

         mqttOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(server, 8883)
				.WithCredentials(username, token)
            .WithClientId(clientId)
            .WithTls()
            .Build();

         mqttClient.UseDisconnectedHandler(new MqttClientDisconnectedHandlerDelegate(e => MqttClient_Disconnected(e)));
         mqttClient.UseApplicationMessageReceivedHandler(new MqttApplicationMessageReceivedHandlerDelegate(e => MqttClient_ApplicationMessageReceived(e)));
         mqttClient.ConnectAsync(mqttOptions).Wait();

			mqttClient.SubscribeAsync(topicC2D, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce).GetAwaiter().GetResult();

			while (true)
         {
            JObject payloadJObject = new JObject();

            payloadJObject.Add("OfficeTemperature", "22." + DateTime.UtcNow.Millisecond.ToString());
            payloadJObject.Add("OfficeHumidity", (DateTime.UtcNow.Second + 40).ToString());

            string payload = JsonConvert.SerializeObject(payloadJObject);
            Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Topic:{topicD2C} Length: {payload.Length} Payload:{payload}");

            var message = new MqttApplicationMessageBuilder()
               .WithTopic(topicD2C)
               .WithPayload(payload)
               .WithAtLeastOnceQoS()
            .Build();

				try
				{
					Console.WriteLine("PublishAsync start");
					mqttClient.PublishAsync(message).Wait();
					Console.WriteLine("PublishAsync finish");
				}
				catch( Exception ex)
				{
					Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} PublishAsync failed {ex.Message}");
				}

				Thread.Sleep(new TimeSpan(0, 2, 0));
			}
      }

      private static void MqttClient_ApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs e)
      {
         Console.WriteLine($"ClientId:{e.ClientId} Topic:{e.ApplicationMessage.Topic} Length:{e.ApplicationMessage.ConvertPayloadToString().Length} Payload:{e.ApplicationMessage.ConvertPayloadToString()}");
      }

      private static async void MqttClient_Disconnected(MqttClientDisconnectedEventArgs e)
      {
         Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Disconnected");
         await Task.Delay(TimeSpan.FromSeconds(5));

         try
         {
            await mqttClient.ConnectAsync(mqttOptions);
         }
         catch (Exception ex)
         {
            Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Reconnect failed {0}", ex.Message);
         }
      }

      public static string generateSasToken(string resourceUri, string key, string policyName, TimeSpan timeToLive)
      {
         DateTimeOffset expiryDateTimeOffset = new DateTimeOffset(DateTime.UtcNow.Add(timeToLive));

         string expiryEpoch = expiryDateTimeOffset.ToUnixTimeSeconds().ToString();
         string stringToSign = WebUtility.UrlEncode(resourceUri) + "\n" + expiryEpoch;

         HMACSHA256 hmac = new HMACSHA256(Convert.FromBase64String(key));
         string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

         string token = $"SharedAccessSignature sr={WebUtility.UrlEncode(resourceUri)}&sig={WebUtility.UrlEncode(signature)}&se={expiryEpoch}";

         if (!String.IsNullOrEmpty(policyName))
         {
            token += "&skn=" + policyName;
         }

         return token;
      }
   }
}

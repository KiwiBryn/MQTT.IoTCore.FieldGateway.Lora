/*
    Copyright ® 2019 April devMobile Software, All Rights Reserved
 
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

	 A quick and dirty test client to explore how AskSensors MQTT connectivity with 
	 the MQTTnet library works
*/
namespace devmobile.Mqtt.TestClient.AskSensors
{
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	using MQTTnet;
	using MQTTnet.Client;
   using MQTTnet.Client.Disconnecting;
   using MQTTnet.Client.Options;
   using MQTTnet.Client.Receiving;

   class Program
	{
		private static IMqttClient mqttClient = null;
		private static IMqttClientOptions mqttOptions = null;
		private static string server;
		private static string username;
		private static string apiKey;
		private static string clientID;

		static void Main(string[] args)
		{
			MqttFactory factory = new MqttFactory();
			mqttClient = factory.CreateMqttClient();
			bool heatPumpOn = false;

			if (args.Length != 4)
			{
				Console.WriteLine("[MQTT Server] [UserName] [APIKey] [ClientID]");
				Console.WriteLine("Press <enter> to exit");
				Console.ReadLine();
				return;
			}

			server = args[0];
			username = args[1];
			apiKey = args[2];
			clientID = args[3];

			Console.WriteLine($"MQTT Server:{server} Username:{username} ClientID:{clientID}");

			mqttOptions = new MqttClientOptionsBuilder()
				.WithTcpServer(server)
				.WithCredentials(username, "")
				.WithClientId(clientID)
				//.WithTls()
				.Build();

         mqttClient.UseDisconnectedHandler(new MqttClientDisconnectedHandlerDelegate(e => MqttClient_Disconnected(e)));
         mqttClient.UseApplicationMessageReceivedHandler(new MqttApplicationMessageReceivedHandlerDelegate(e => MqttClient_ApplicationMessageReceived(e)));
         mqttClient.ConnectAsync(mqttOptions).Wait();

			// AskSensors formatted client state update topic
			string stateTopic = $"{username}/{apiKey}";

			while (true)
			{
				string payloadText;
				double temperature = 22.0 + (DateTime.UtcNow.Millisecond / 1000.0);
				double humidity = 50 + (DateTime.UtcNow.Millisecond / 100.0);
				double speed = 10 + (DateTime.UtcNow.Millisecond / 100.0);
				Console.WriteLine($"Topic:{stateTopic} Temperature:{temperature:0.00} Humidity:{humidity:0} HeatPumpOn:{heatPumpOn}");

				// First JSON attempt didn't work
				payloadText = @"{""Humidity"":55}";

            // Second attempt worked
            payloadText = $"module1=22";

            // Third attempt with "real" values injected
            payloadText = $"module1={temperature}&m2={humidity}";

            var message = new MqttApplicationMessageBuilder()
					.WithTopic(stateTopic)
					.WithPayload(payloadText)
					.WithQualityOfServiceLevel(global::MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
				   .WithExactlyOnceQoS()
				   //.WithAtLeastOnceQoS()
				   //.WithRetainFlag()
				   .Build();

				Console.WriteLine("PublishAsync start");
				mqttClient.PublishAsync(message).Wait();
				Console.WriteLine("PublishAsync finish");

				Thread.Sleep(30100);
			}
		}

		private static void MqttClient_ApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs e)
		{
			Console.WriteLine($"ClientId:{e.ClientId} Topic:{e.ApplicationMessage.Topic} Payload:{e.ApplicationMessage.ConvertPayloadToString()}");
		}

		private static async void MqttClient_Disconnected(MqttClientDisconnectedEventArgs e)
		{
			Debug.WriteLine("Disconnected");
			await Task.Delay(TimeSpan.FromSeconds(5));

			try
			{
				await mqttClient.ConnectAsync(mqttOptions);
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Reconnect failed {0}", ex.Message);
			}
		}
	}
}

/*
    Copyright ® 2020 February devMobile Software, All Rights Reserved
 
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

	 A quick and dirty test client to explore how AllThingsTalk MQTT connectivity with 
	 the MQTTnet library works
*/
namespace devmobile.Mqtt.TestClient.AllThingsTalk
{
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	using MQTTnet;
	using MQTTnet.Client;
	using MQTTnet.Client.Disconnecting;
	using MQTTnet.Client.Options;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	class Program
	{
		private static IMqttClient mqttClient = null;
		private static IMqttClientOptions mqttOptions = null;
		private static string server;
		private static string username;
		private static string deviceID;

		static void Main(string[] args)
		{
			MqttFactory factory = new MqttFactory();
			mqttClient = factory.CreateMqttClient();

			if ((args.Length != 3))
			{
				Console.WriteLine("[MQTT Server] [UserName] [ClientID]");
				Console.WriteLine("Press <enter> to exit");
				Console.ReadLine();
				return;
			}

			server = args[0];
			username = args[1];
			deviceID = args[2];

			Console.WriteLine($"MQTT Server:{server} ClientID:{deviceID}");

			// AllThingsTalk formatted device state update topic
			string topicD2C = $"device/{deviceID}/state";

			mqttOptions = new MqttClientOptionsBuilder()
				.WithTcpServer(server)
				.WithCredentials(username, "HighlySecurePassword")
				.WithClientId(deviceID)
				.WithTls()
				.Build();

			mqttClient.UseDisconnectedHandler(new MqttClientDisconnectedHandlerDelegate(e => MqttClient_Disconnected(e)));
			mqttClient.ConnectAsync(mqttOptions).Wait();

			while (true)
			{
				JObject payloadJObject = new JObject();

				double temperature = 22.0 + (DateTime.UtcNow.Millisecond / 1000.0);
				double humidity = 50 + (DateTime.UtcNow.Millisecond / 100.0);

				JObject temperatureJObject = new JObject
				{
					{ "value", temperature }
				};
				payloadJObject.Add("Temperature", temperatureJObject);

				JObject humidityJObject = new JObject
				{
					{ "value", humidity }
				};
				payloadJObject.Add("Humidity", humidityJObject);

				string payload = JsonConvert.SerializeObject(payloadJObject);
				Console.WriteLine($"Topic:{topicD2C} Payload:{payload}");

				var message = new MqttApplicationMessageBuilder()
					.WithTopic(topicD2C)
					.WithPayload(payload)
					.WithAtMostOnceQoS()
//					.WithAtLeastOnceQoS()
					.Build();

				Console.WriteLine("PublishAsync start");
				mqttClient.PublishAsync(message).Wait();
				Console.WriteLine("PublishAsync finish");

				Thread.Sleep(15100);
			}
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

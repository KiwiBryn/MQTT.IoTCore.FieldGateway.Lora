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

	 A quick and dirty test client to explore how MyDevice Cayenne connectivity works
	 with the MQTTnet library
 */
namespace devmobile.TestClient.MQTTnet.MyDevicesCayenne
{
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;
	using global::MQTTnet;
	using global::MQTTnet.Client;

	class Program
	{
		private static IMqttClient mqttClient = null;
		private static IMqttClientOptions mqttOptions = null;
		private static string server;
		private static string username;
		private static string password;
		private static string clientId;
		private static string Channel;

		static void Main(string[] args)
		{
			MqttFactory factory = new MqttFactory();
			mqttClient = factory.CreateMqttClient();

			if (args.Length != 5)
			{
				Console.WriteLine("[MQTT Server] [UserName] [Password] [ClientID] [Channel]");
				Console.WriteLine("Press <enter> to exit");
				Console.ReadLine();
				return;
			}

			server = args[0];
			username = args[1];
			password = args[2];
			clientId = args[3];
			Channel = args[4];

			Console.WriteLine($"MQTT Server:{server} Username:{username} ClientID:{clientId} Channel:{Channel}");

			mqttOptions = new MqttClientOptionsBuilder()
				.WithTcpServer(args[0])
				.WithCredentials(args[1], args[2])
				.WithClientId(args[3])
				.WithTls()
				.Build();

			mqttClient.ConnectAsync(mqttOptions).Wait();
			mqttClient.Disconnected += MqttClient_Disconnected;

			string topicTemperatureData = $"v1/{username}/things/{clientId}/data/{Channel}";

			while (true)
			{
				string value = "22." + DateTime.UtcNow.Millisecond.ToString();
				Console.WriteLine($"Feed {topicTemperatureData}  Value {value}");

				var message = new MqttApplicationMessageBuilder()
					.WithTopic(topicTemperatureData)
					.WithPayload(value)
					.WithQualityOfServiceLevel(global::MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
					//.WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce) // Causes publish to hang
					.WithRetainFlag()
				.Build();

				Console.WriteLine("PublishAsync start");
				mqttClient.PublishAsync(message).Wait();
				Console.WriteLine("PublishAsync finish");

				Thread.Sleep(30100);
			}
		}

		private static async void MqttClient_Disconnected(object sender, MqttClientDisconnectedEventArgs e)
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

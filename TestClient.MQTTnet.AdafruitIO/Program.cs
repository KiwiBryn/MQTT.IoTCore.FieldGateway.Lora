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

	 A quick and dirty test client to explore how Adafruit.IO MQTT connectivity works
 */
namespace devMobile.Mqtt.TestClient.AdaFruit
{
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	using MQTTnet;
	using MQTTnet.Client;

	class Program
	{
		private static IMqttClient mqttClient = null;
		private static IMqttClientOptions mqttOptions = null;
		private static string server;
		private static string username;
		private static string password;
		private static string clientId;
		private static string groupname;
		private static string feedname;

		static void Main(string[] args)
		{
			MqttFactory factory = new MqttFactory();
			mqttClient = factory.CreateMqttClient();

			if ((args.Length != 5) && (args.Length != 6))
			{
				Console.WriteLine("[MQTT Server] [UserName] [Password] [ClientID] [GroupName] [FeedName]");
				Console.WriteLine("[MQTT Server] [UserName] [Password] [ClientID] [FeedName]");
				Console.WriteLine("Press <enter> to exit");
				Console.ReadLine();
				return;
			}

			server = args[0];
			username = args[1];
			password = args[2];
			clientId = args[3];
			if (args.Length == 5)
			{
				feedname = args[4].ToLower();
				Console.WriteLine($"MQTT Server:{server} Username:{username} ClientID:{clientId} Feedname:{feedname}");
			}

			if (args.Length == 6)
			{
				groupname = args[4].ToLower();
				feedname = args[5].ToLower();
				Console.WriteLine($"MQTT Server:{server} Username:{username} ClientID:{clientId} Groupname:{groupname} Feedname:{feedname}");
			}

			mqttOptions = new MqttClientOptionsBuilder()
				.WithTcpServer(server)
				.WithCredentials(username, password)
				.WithClientId(clientId)
				.WithTls()
				.Build();

			mqttClient.Disconnected += MqttClient_Disconnected;
			mqttClient.ConnectAsync(mqttOptions).Wait();

			// Adafruit.IO format for topics which are called feeds
			string topic = string.Empty;

			if (args.Length == 5)
			{
				topic = $"{username}/feeds/{feedname}";				
			}

			if (args.Length == 6)
			{
				topic = $"{username}/feeds/{groupname}.{feedname}";
			}

			while (true)
			{
				string value = "22." + DateTime.UtcNow.Millisecond.ToString();
				Console.WriteLine($"Topic:{topic} Value:{value}");

				var message = new MqttApplicationMessageBuilder()
					.WithTopic(topic)
					.WithPayload(value)
					.WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
				.WithExactlyOnceQoS()
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

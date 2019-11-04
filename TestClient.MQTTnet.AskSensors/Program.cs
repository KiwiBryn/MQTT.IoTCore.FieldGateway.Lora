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

	 A quick and dirty test client to explore how Ubidots MQTT connectivity with 
	 the MQTTnet library works
*/
namespace devmobile.Mqtt.TestClient.MQTTnet.AskSensors
{
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	using global::MQTTnet;
	using global::MQTTnet.Client;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

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

			mqttClient.ApplicationMessageReceived += MqttClient_ApplicationMessageReceived;
			mqttClient.Disconnected += MqttClient_Disconnected;
			mqttClient.ConnectAsync(mqttOptions).Wait();

			// Setup a subscription for commands sent to client
			//commandTopic = $"/v1.6/devices/{deviceLabel}/officetemperaturedesired";
			//mqttClient.SubscribeAsync(commandTopic).GetAwaiter().GetResult();

			//string commandTopic = $"/v1.6/devices/{deviceLabel}/officetemperaturedesired/lv";
			//string commandTopic = $"/v1.6/devices/{deviceLabel}/officetemperaturedesired"; // JSON
			//mqttClient.SubscribeAsync(commandTopic).GetAwaiter().GetResult();

			//string commandTopic = $"/v1.6/devices/{deviceLabel}/53-65-65-65-64-41-4d-32-33-30-32-31l/lv";
			//string commandTopic = $"/v1.6/devices/{deviceLabel}/53-65-65-65-64-41-4d-32-33-30-32-31l"; // JSON
			//mqttClient.SubscribeAsync(commandTopic).GetAwaiter().GetResult();

			//string commandTopic3 = $"/v1.6/devices/{deviceLabel}/+";  // Works
			//string commandTopic3 = $"/v1.6/devices/{deviceLabel}/+";
			//mqttClient.SubscribeAsync(commandTopic3).GetAwaiter().GetResult();

			// AskSensors formatted client state update topic
			string stateTopic = $"{username}/{apiKey}";

			while (true)
			{
				string payloadText;
				double temperature = 22.0 + (DateTime.UtcNow.Millisecond / 1000.0);
				double humidity = 50 + (DateTime.UtcNow.Millisecond / 100.0);
				double speed = 10 + (DateTime.UtcNow.Millisecond / 100.0);
				Console.WriteLine($"Topic:{stateTopic} Temperature:{temperature:0.00} Humidity:{humidity:0} HeatPumpOn:{heatPumpOn}");

				// First attempt which worked
				//payloadText = @"{""OfficeTemperature"":22.5}";

				// Second attempt to work out data format with "real" values injected
				//payloadText = @"{ ""officetemperature"":"+ temperature.ToString("F2") + @",""officehumidity"":" + humidity.ToString("F0") + @"}";

				// Third attempt with Jobject which sort of worked but number serialisation was sub optimal
				//JObject payloadJObject = new JObject();
				//payloadJObject.Add("Module 1", temperature.ToString("F2"));
				//payloadJObject.Add("Module 2", humidity.ToString("F0"));
				/*
				if (heatPumpOn)
				{
					payloadJObject.Add("HeatPumpOn", 1);
				}
				else
				{
					payloadJObject.Add("HeatPumpOn", 0);
				}
				heatPumpOn = !heatPumpOn;
				*/
				//payloadText = JsonConvert.SerializeObject(payloadJObject);

				/*
				// Forth attempt with JOBject, timestamps and gps 
				JObject payloadJObject = new JObject();
				JObject context = new JObject();
				context.Add("lat", "-43.5309325");
				context.Add("lng", "172.637119");// Christchurch Cathederal
			   //context.Add("timestamp", ((DateTimeOffset)(DateTime.UtcNow)).ToUnixTimeSeconds()); // This field is optional and can be commented out
				JObject position = new JObject();
				position.Add("context", context);
				position.Add("value", "0");
				payloadJObject.Add("postion", position);
				payloadText = JsonConvert.SerializeObject(payloadJObject);
				*/

				//payloadText = $"module1={temperature}&m2={humidity}";
				payloadText = $"module1=22";

				var message = new MqttApplicationMessageBuilder()
					.WithTopic(stateTopic)
					.WithPayload(payloadText)
				//	.WithQualityOfServiceLevel(global::MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
				//.WithExactlyOnceQoS()// With AskSensors this caused the publish to hang
				.WithAtLeastOnceQoS()
				//.WithRetainFlag()
				.Build();

				Console.WriteLine("PublishAsync start");
				mqttClient.PublishAsync(message).Wait();
				Console.WriteLine("PublishAsync finish");

				Thread.Sleep(30100);
			}
		}

		private static void MqttClient_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
		{
			Console.WriteLine($"ClientId:{e.ClientId} Topic:{e.ApplicationMessage.Topic} Payload:{e.ApplicationMessage.ConvertPayloadToString()}");
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

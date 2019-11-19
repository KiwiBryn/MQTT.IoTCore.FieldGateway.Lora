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

	 A quick and dirty test client to explore how Losant MQTT connectivity with 
	 the MQTTnet library works
*/
namespace devmobile.Mqtt.TestClient.Losant
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

		static void Main(string[] args)
		{
			MqttFactory factory = new MqttFactory();
			mqttClient = factory.CreateMqttClient();
			bool heatPumpOn = false;

			if (args.Length != 4)
			{
				Console.WriteLine("[MQTT Server] [UserName] [Password] [ClientID]");
				Console.WriteLine("Press <enter> to exit");
				Console.ReadLine();
				return;
			}

			server = args[0];
			username = args[1];
			password = args[2];
			clientId = args[3];

			Console.WriteLine($"MQTT Server:{server} Username:{username} ClientID:{clientId}");

			mqttOptions = new MqttClientOptionsBuilder()
				.WithTcpServer(server)
				.WithCredentials(username, password)
				.WithClientId(clientId)
				.WithTls()
				.Build();

         mqttClient.UseDisconnectedHandler(new MqttClientDisconnectedHandlerDelegate(e => MqttClient_Disconnected(e)));
         mqttClient.UseApplicationMessageReceivedHandler(new MqttApplicationMessageReceivedHandlerDelegate(e => MqttClient_ApplicationMessageReceived(e)));
         mqttClient.ConnectAsync(mqttOptions).Wait();

			// Setup a subscription for commands sent to client
			string commandTopic = $"losant/{clientId}/command";
         mqttClient.SubscribeAsync(commandTopic);

         // Losant formatted client state update topic
         string stateTopic = $"losant/{clientId}/state";

			while (true)
			{
				string payloadText;
				double temperature = 22.0 + +(DateTime.UtcNow.Millisecond / 1000.0);
				double humidity = 50 + +(DateTime.UtcNow.Millisecond / 1000.0);
				Console.WriteLine($"Topic:{stateTopic} Temperature:{temperature} Humidity:{humidity} HeatPumpOn:{heatPumpOn}");

            // First attempt which worked
            //payloadText = @"{""data"":{ ""OfficeTemperature"":22.5}}";

            // Second attempt to work out data format with "real" values injected
            //payloadText = @"{""data"":{ ""OfficeTemperature"":"+ temperature.ToString("f1") + @",""OfficeHumidity"":" + humidity.ToString("F0") + @"}}";

            // Third attempt with Jobject which sort of worked but number serialisation is sub optimal
            //JObject payloadJObject = new JObject(); 
            //payloadJObject.Add("time", DateTime.UtcNow.ToString("u")); // This field is optional and can be commented out

            //JObject data = new JObject();
            //data.Add("OfficeTemperature", temperature.ToString("F1"));
            //data.Add("OfficeHumidity", humidity.ToString("F0"));

            //if (heatPumpOn)
            //{
            //	data.Add("HeatPumpOn", "true");
            //}
            //else
            //{
            //	data.Add("HeatPumpOn", "false");
            //}
            //heatPumpOn = !heatPumpOn;
            //payloadJObject.Add( "data", data);

            //payloadText = JsonConvert.SerializeObject(payloadJObject);

            // Forth attempt with JOBject and gps info https://docs.losant.com/devices/state/
            //JObject payloadJObject = new JObject(); 
            //payloadJObject.Add("time", DateTime.UtcNow.ToString("u")); // This field is optional and can be commented out
            //JObject data = new JObject();
            //data.Add("GPS", "-43.5309325, 172.637119"); // Christchurch Cathederal
            //payloadJObject.Add("data", data);
            //payloadText = JsonConvert.SerializeObject(payloadJObject);

            // Fith attempt for peripheral device
            //stateTopic = $"losant/5cd3f2fed8920a0009bd68c8/state";
            stateTopic = $"losant/{clientId}/state";

				JObject payloadJObject = new JObject(); 
				JObject data = new JObject();
				data.Add("t", temperature.ToString("F1"));
				data.Add("h", humidity.ToString("F0"));
				payloadJObject.Add( "data", data);

				payloadText = JsonConvert.SerializeObject(payloadJObject);

				var message = new MqttApplicationMessageBuilder()
					.WithTopic(stateTopic)
					.WithPayload(payloadText)
					.WithQualityOfServiceLevel(global::MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
				//.WithExactlyOnceQoS() With Losant this caused the publish to hang
				.WithAtLeastOnceQoS()
				//.WithRetainFlag() Losant doesn't allow this flag
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

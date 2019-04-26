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
 */
namespace devMobile.Mqtt.IoTCore.FieldGateway
{
	using System;
	using MQTTnet;
	using MQTTnet.Client;
	using devMobile.IoT.Rfm9x;
	using Windows.Foundation.Diagnostics;
	using Newtonsoft.Json.Linq;
	using System.Text;
	using System.Diagnostics;
	using System.Threading.Tasks;
	using Newtonsoft.Json;

	public class MessageHandler : IMessageHandler
	{
		private LoggingChannel logging { get; set; }
		private IMqttClient mqttClient { get; set; }
		private Rfm9XDevice rfm9XDevice { get; set; }

		void IMessageHandler.Initialise(LoggingChannel logging, IMqttClient mqttClient, Rfm9XDevice rfm9XDevice)
		{
			LoggingFields processInitialiseLoggingFields = new LoggingFields();

			this.logging = logging;
			this.mqttClient = mqttClient;
			this.rfm9XDevice = rfm9XDevice;

			string commandTopic = $"losant/{mqttClient.Options.ClientId}/command";
			try
			{
				mqttClient.SubscribeAsync(commandTopic);
			}
			catch (Exception ex)
			{
				processInitialiseLoggingFields.AddString("Exception", ex.ToString());
				this.logging.LogEvent("PayloadProcess failure converting payload to text", processInitialiseLoggingFields, LoggingLevel.Warning);
				return;
			}
		}

		async void IMessageHandler.ProcessReceive(object sender, Rfm9XDevice.OnDataReceivedEventArgs e)
		{
			LoggingFields processReceiveLoggingFields = new LoggingFields();
			JObject telemetryDataPoint = new JObject();
			char[] sensorReadingSeparators = { ',' };
			char[] sensorIdAndValueSeparators = { ' ' };
			
			processReceiveLoggingFields.AddString("PacketSNR", e.PacketSnr.ToString("F1"));
			processReceiveLoggingFields.AddInt32("PacketRSSI", e.PacketRssi);
			processReceiveLoggingFields.AddInt32("RSSI", e.Rssi);

			string addressBcdText = BitConverter.ToString(e.Address);
			processReceiveLoggingFields.AddInt32("DeviceAddressLength", e.Address.Length);
			processReceiveLoggingFields.AddString("DeviceAddressBCD", addressBcdText);

			string messageText;
			try
			{
				messageText = UTF8Encoding.UTF8.GetString(e.Data);
				processReceiveLoggingFields.AddString("MessageText", messageText);
			}
			catch (Exception ex)
			{
				processReceiveLoggingFields.AddString("Exception", ex.ToString());
				this.logging.LogEvent("PayloadProcess failure converting payload to text", processReceiveLoggingFields, LoggingLevel.Warning);
				return;
			}

			// Chop up the CSV text
			string[] sensorReadings = messageText.Split(sensorReadingSeparators, StringSplitOptions.RemoveEmptyEntries);
			if (sensorReadings.Length < 1)
			{
				this.logging.LogEvent("PayloadProcess payload contains no sensor readings", processReceiveLoggingFields, LoggingLevel.Warning);
				return;
			}

			// Chop up each sensor read into an ID & value
			JObject data = new JObject();
			foreach (string sensorReading in sensorReadings)
			{
				string[] sensorIdAndValue = sensorReading.Split(sensorIdAndValueSeparators, StringSplitOptions.RemoveEmptyEntries);

				// Check that there is an id & value
				if (sensorIdAndValue.Length != 2)
				{
					this.logging.LogEvent("PayloadProcess payload invalid format", processReceiveLoggingFields, LoggingLevel.Warning);
					return;
				}

				string sensorId = sensorIdAndValue[0];
				string value = sensorIdAndValue[1];

				data.Add(addressBcdText + sensorId, Convert.ToDouble(value));
			}
			telemetryDataPoint.Add("data", data);

			processReceiveLoggingFields.AddString("MQTTClientId", mqttClient.Options.ClientId);

			string topic = $"losant/{mqttClient.Options.ClientId}/state";

			try
			{
				var message = new MqttApplicationMessageBuilder()
					.WithTopic(topic)
					.WithPayload(JsonConvert.SerializeObject(telemetryDataPoint))
					.WithAtLeastOnceQoS()
					.Build();
				Debug.WriteLine(" {0:HH:mm:ss} MQTT Client PublishAsync start", DateTime.UtcNow);
				await mqttClient.PublishAsync(message);
				Debug.WriteLine(" {0:HH:mm:ss} MQTT Client PublishAsync finish", DateTime.UtcNow);

				this.logging.LogEvent("PublishAsync Losant payload", processReceiveLoggingFields, LoggingLevel.Information);
			}
			catch (Exception ex)
			{
				processReceiveLoggingFields.AddString("Exception", ex.ToString());
				this.logging.LogEvent("PublishAsync Losant payload", processReceiveLoggingFields, LoggingLevel.Error);
			}
		}

		ProcessTransmitResponse IMessageHandler.ProcessTransmit(object sender, MqttApplicationMessageReceivedEventArgs e)
		{
			return null;
		}

		void IMessageHandler.OnTransmit(object sender, Rfm9XDevice.OnDataTransmitedEventArgs e)
		{ 
		}
	}
}

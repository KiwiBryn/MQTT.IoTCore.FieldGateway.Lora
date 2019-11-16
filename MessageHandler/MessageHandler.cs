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

	 Template for MessageHandler assemblies with sample logging code
 */
namespace devMobile.Mqtt.IoTCore.FieldGateway
{
	using System;
	using Windows.Foundation.Diagnostics;

	using devMobile.IoT.Rfm9x;
	using MQTTnet;
	using MQTTnet.Client;

	public class MessageHandler : IMessageHandler
	{
		private LoggingChannel Logging { get; set; }
		private IMqttClient MqttClient { get; set; }
		private Rfm9XDevice Rfm9XDevice { get; set; }
      private string PlatformSpecificConfiguration { get; set; }


      void IMessageHandler.Initialise(LoggingChannel logging, IMqttClient mqttClient, Rfm9XDevice rfm9XDevice, string platformSpecificConfiguration)
		{
			LoggingFields processInitialiseLoggingFields = new LoggingFields();

			this.Logging = logging;
			this.MqttClient = mqttClient;
			this.Rfm9XDevice = rfm9XDevice;
		}

		async void IMessageHandler.Rfm9XOnReceive(object sender, Rfm9XDevice.OnDataReceivedEventArgs e)
		{
			LoggingFields processReceiveLoggingFields = new LoggingFields();

			processReceiveLoggingFields.AddString("PacketSNR", e.PacketSnr.ToString("F1"));
			processReceiveLoggingFields.AddInt32("PacketRSSI", e.PacketRssi);
			processReceiveLoggingFields.AddInt32("RSSI", e.Rssi);

			string addressBcdText = BitConverter.ToString(e.Address);
			processReceiveLoggingFields.AddInt32("DeviceAddressLength", e.Address.Length);
			processReceiveLoggingFields.AddString("DeviceAddressBCD", addressBcdText);

			string payloadBcdText = BitConverter.ToString(e.Data);
			processReceiveLoggingFields.AddInt32("PayloadLength", e.Data.Length);
			processReceiveLoggingFields.AddString("DeviceAddressBCD", payloadBcdText);

			this.Logging.LogEvent("Rfm9XOnReceive", processReceiveLoggingFields, LoggingLevel.Information);
		}

		void IMessageHandler.MqttApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
		{
			LoggingFields processReceiveLoggingFields = new LoggingFields();

			processReceiveLoggingFields.AddString("ClientId", e.ClientId);
#if DEBUG
			processReceiveLoggingFields.AddString("Payload", e.ApplicationMessage.ConvertPayloadToString());
#endif
			processReceiveLoggingFields.AddString("QualityOfServiceLevel", e.ApplicationMessage.QualityOfServiceLevel.ToString());
			processReceiveLoggingFields.AddBoolean("Retain", e.ApplicationMessage.Retain);
			processReceiveLoggingFields.AddString("Topic", e.ApplicationMessage.Topic);

			this.Logging.LogEvent("MqttApplicationMessageReceived topic not processed", processReceiveLoggingFields, LoggingLevel.Error);
		}

		void IMessageHandler.Rfm9xOnTransmit(object sender, Rfm9XDevice.OnDataTransmitedEventArgs e)
		{
			this.Logging.LogMessage("Rfm9xOnTransmit", LoggingLevel.Information);
		}
	}
}

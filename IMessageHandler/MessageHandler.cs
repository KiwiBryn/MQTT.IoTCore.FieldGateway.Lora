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
	using System.Threading.Tasks;

	public class ProcessTransmitResponse
	{
		public byte[] Address { get; set; }
		public byte[] Payload { get; set; }
	}

	public interface IMessageHandler
	{
		void Initialise(LoggingChannel logging, IMqttClient mqttClient, Rfm9XDevice rfm9XDevice);

		void ProcessReceive(object sender, Rfm9XDevice.OnDataReceivedEventArgs e);
		
		ProcessTransmitResponse ProcessTransmit(object sender, MqttApplicationMessageReceivedEventArgs e);

		void OnTransmit(object sender, Rfm9XDevice.OnDataTransmitedEventArgs e);
	}
}

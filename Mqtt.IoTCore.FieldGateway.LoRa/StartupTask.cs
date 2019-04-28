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
namespace devMobile.Mqtt.IoTCore.FieldGateway.LoRa
{
	using System;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Reflection;
	using System.Text;
	using System.Threading.Tasks;

	using devMobile.IoT.Rfm9x;

	using MQTTnet;
	using MQTTnet.Client;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Converters;
	using Windows.ApplicationModel;
	using Windows.ApplicationModel.Background;
	using Windows.Foundation.Diagnostics;
	using Windows.Storage;
	using Windows.System;

	public sealed class StartupTask : IBackgroundTask
	{
		private const string ConfigurationFilename = "config.json";

		// LoRa Hardware interface configuration
#if DRAGINO
		private const byte ChipSelectLine = 25;
		private const byte ResetLine = 17;
		private const byte InterruptLine = 4;
		private Rfm9XDevice rfm9XDevice = new Rfm9XDevice(ChipSelectPin.CS0, ChipSelectLine, ResetLine, InterruptLine);
#endif
#if M2M
		private const byte ChipSelectLine = 25;
		private const byte ResetLine = 17;
		private const byte InterruptLine = 4;
		private Rfm9XDevice rfm9XDevice = new Rfm9XDevice(ChipSelectPin.CS0, ChipSelectLine, ResetLine, InterruptLine);
#endif
#if ELECROW
		private const byte ResetLine = 22;
		private const byte InterruptLine = 25;
		private Rfm9XDevice rfm9XDevice = new Rfm9XDevice(ChipSelectPin.CS1, ResetLine, InterruptLine);
#endif
#if ELECTRONIC_TRICKS
		private const byte ResetLine = 22;
		private const byte InterruptLine = 25;
		private Rfm9XDevice rfm9XDevice = new Rfm9XDevice(ChipSelectPin.CS0, ResetLine, InterruptLine);
#endif
#if UPUTRONICS_RPIZERO_CS0
		private const byte InterruptLine = 25;
		private Rfm9XDevice rfm9XDevice = new Rfm9XDevice(ChipSelectPin.CS0, InterruptLine);
#endif
#if UPUTRONICS_RPIZERO_CS1
		private const byte InterruptLine = 16;
		private Rfm9XDevice rfm9XDevice = new Rfm9XDevice(ChipSelectPin.CS1, InterruptLine);
#endif
#if UPUTRONICS_RPIPLUS_CS0
		private const byte InterruptLine = 25;
		private Rfm9XDevice rfm9XDevice = new Rfm9XDevice(ChipSelectPin.CS0, InterruptLine);
#endif
#if UPUTRONICS_RPIPLUS_CS1
		private const byte InterruptLine = 16;
		private Rfm9XDevice rfm9XDevice = new Rfm9XDevice(ChipSelectPin.CS1, InterruptLine);
#endif
#if ADAFRUIT_RADIO_BONNET
		private const byte ResetLine = 25;
		private const byte InterruptLine = 22;
		private Rfm9XDevice rfm9XDevice = new Rfm9XDevice(ChipSelectPin.CS1, ResetLine, InterruptLine);
#endif
		private readonly TimeSpan mqttReconnectDelay = new TimeSpan(0, 0, 5);
		private readonly LoggingChannel logging = new LoggingChannel("devMobile MQTT LoRa Field Gateway", null, new Guid("4bd2826e-54a1-4ba9-bf63-92b73ea1ac4a"));
		private ApplicationSettings applicationSettings = null;
		private IMessageHandler messageHandler = null;
		private IMqttClient mqttClient = null;
		private IMqttClientOptions mqttOptions = null ;
		private BackgroundTaskDeferral deferral = null;

		public void Run(IBackgroundTaskInstance taskInstance)
		{
			StorageFolder localFolder = ApplicationData.Current.LocalFolder;

			try
			{
				// see if the configuration file is present if not copy minimal sample one from application directory
				if (localFolder.TryGetItemAsync(ConfigurationFilename).AsTask().Result == null)
				{
					StorageFile templateConfigurationfile = Package.Current.InstalledLocation.GetFileAsync(ConfigurationFilename).AsTask().Result;
					templateConfigurationfile.CopyAsync(localFolder, ConfigurationFilename).AsTask();
				}

				// Load the settings from configuration file exit application if missing or invalid
				StorageFile file = localFolder.GetFileAsync(ConfigurationFilename).AsTask().Result;

				applicationSettings = (JsonConvert.DeserializeObject<ApplicationSettings>(FileIO.ReadTextAsync(file).AsTask().Result));
			}
			catch (Exception ex)
			{
				this.logging.LogMessage("JSON configuration file load failed " + ex.Message, LoggingLevel.Error);
				return;
			}

			// Log the Application build, shield information etc.
			LoggingFields applicationBuildInformation = new LoggingFields();
#if DRAGINO
			applicationBuildInformation.AddString("Shield", "DraginoLoRaGPSHat");
#endif
#if ELECROW
			applicationBuildInformation.AddString("Shield", "ElecrowRFM95IoTBoard");
#endif
#if M2M
			applicationBuildInformation.AddString("Shield", "M2M1ChannelLoRaWanGatewayShield");
#endif
#if ELECTRONIC_TRICKS
			applicationBuildInformation.AddString("Shield", "ElectronicTricksLoRaLoRaWANShield");
#endif
#if UPUTRONICS_RPIZERO_CS0
			applicationBuildInformation.AddString("Shield", "UputronicsPiZeroLoRaExpansionBoardCS0");
#endif
#if UPUTRONICS_RPIZERO_CS1
			applicationBuildInformation.AddString("Shield", "UputronicsPiZeroLoRaExpansionBoardCS1");
#endif
#if UPUTRONICS_RPIPLUS_CS0
			applicationBuildInformation.AddString("Shield", "UputronicsPiPlusLoRaExpansionBoardCS0");
#endif
#if UPUTRONICS_RPIPLUS_CS1
			applicationBuildInformation.AddString("Shield", "UputronicsPiPlusLoRaExpansionBoardCS1");
#endif
#if ADAFRUIT_RADIO_BONNET
			applicationBuildInformation.AddString("Shield", "AdafruitRadioBonnet");
#endif
#if PAYLOAD_TEXT
			applicationBuildInformation.AddString("PayloadProcessor", "Text");
#endif
#if PAYLOAD_TEXT_COMA_SEPARATED_VALUES
			applicationBuildInformation.AddString("PayloadProcessor", "ComaSeperatedValues");
#endif
#if PAYLOAD_BINARY_BINARY_CODED_DECIMAL
			applicationBuildInformation.AddString("PayloadProcessor", "BinaryCodedDecimal");
#endif
#if PAYLOAD_BINARY_CAYENNE_LOW_POWER_PAYLOAD
			applicationBuildInformation.AddString("PayloadProcessor", "CayenneLowPowerPayload");
#endif
			applicationBuildInformation.AddString("Timezone", TimeZoneSettings.CurrentTimeZoneDisplayName);
			applicationBuildInformation.AddString("OSVersion", Environment.OSVersion.VersionString);
			applicationBuildInformation.AddString("MachineName", Environment.MachineName);

			// This is from the application manifest 
			Package package = Package.Current;
			PackageId packageId = package.Id;
			PackageVersion version = packageId.Version;

			applicationBuildInformation.AddString("ApplicationVersion", string.Format($"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"));
			this.logging.LogEvent("Application starting", applicationBuildInformation, LoggingLevel.Information);

			// Log the MQTT connection string and associated settings
			LoggingFields mqttClientInformation = new LoggingFields();
			mqttClientInformation.AddString("UserName", this.applicationSettings.MqttUserName);
			mqttClientInformation.AddString("Password", this.applicationSettings.MqttPassword);
			mqttClientInformation.AddString("Server", this.applicationSettings.MqttServer);
			mqttClientInformation.AddString("ClientID", this.applicationSettings.MqttClientID);
			this.logging.LogEvent("MQTT client configuration", mqttClientInformation, LoggingLevel.Information);

			// Connect the MQTT broker so we are ready for messages
			var factory = new MqttFactory();
			this.mqttClient = factory.CreateMqttClient();

			try
			{ 
				this.mqttOptions = new MqttClientOptionsBuilder()
							.WithClientId(applicationSettings.MqttClientID)
							.WithTcpServer(applicationSettings.MqttServer)
							.WithCredentials(applicationSettings.MqttUserName, applicationSettings.MqttPassword)
							.WithTls()
							.Build();

				this.mqttClient.ConnectAsync(this.mqttOptions).Wait();
			}
			catch (Exception ex)
			{
				mqttClientInformation.AddString("Exception", ex.ToString());
				this.logging.LogMessage("MQTT Connect Async failed" + ex.Message, LoggingLevel.Error);
				return;
			}

			// Wire up a handler for disconnect event for retry
			this.mqttClient.ApplicationMessageReceived += MqttClient_ApplicationMessageReceived;
			this.mqttClient.Disconnected += MqttClient_Disconnected;

			// Load up the message handler assembly
			try
			{
				Assembly assembly = Assembly.Load(applicationSettings.MessageHandlerAssembly);

				messageHandler = (IMessageHandler)assembly.CreateInstance("devMobile.Mqtt.IoTCore.FieldGateway.MessageHandler");
				if (messageHandler == null)
				{
					this.logging.LogMessage($"MessageHandler assembly {applicationSettings.MessageHandlerAssembly} load failed", LoggingLevel.Error);
					return;
				}

				messageHandler.Initialise(logging, mqttClient, rfm9XDevice);
			}
			catch (Exception ex)
			{
				mqttClientInformation.AddString("Exception", ex.ToString());
				this.logging.LogMessage("MessageHandler configuration failed" + ex.Message, LoggingLevel.Error);
				return;
			}

			// Configure the LoRa module
			rfm9XDevice.OnReceive += Rfm9XDevice_OnReceive;
			rfm9XDevice.OnTransmit += Rfm9XDevice_OnTransmit;

			rfm9XDevice.Initialise(this.applicationSettings.Frequency,
				rxDoneignoreIfCrcMissing: true,
				rxDoneignoreIfCrcInvalid: true,
				paBoost: this.applicationSettings.PABoost, maxPower: this.applicationSettings.MaxPower, outputPower: this.applicationSettings.OutputPower,
				ocpOn: this.applicationSettings.OCPOn, ocpTrim: this.applicationSettings.OCPTrim,
				lnaGain: this.applicationSettings.LnaGain, lnaBoost: this.applicationSettings.LNABoost,
				bandwidth: this.applicationSettings.Bandwidth, codingRate: this.applicationSettings.CodingRate, implicitHeaderModeOn: this.applicationSettings.ImplicitHeaderModeOn,
				spreadingFactor: this.applicationSettings.SpreadingFactor,
				rxPayloadCrcOn: true,
				symbolTimeout: this.applicationSettings.SymbolTimeout,
				preambleLength: this.applicationSettings.PreambleLength,
				payloadLength: this.applicationSettings.PayloadLength,
				payloadMaxLength: this.applicationSettings.PayloadMaxLength,
				freqHoppingPeriod: this.applicationSettings.FreqHoppingPeriod,
				lowDataRateOptimize: this.applicationSettings.LowDataRateOptimize,
				ppmCorrection: this.applicationSettings.PpmCorrection,
				detectionOptimize: this.applicationSettings.DetectionOptimize,
				invertIQ: this.applicationSettings.InvertIQ,
				detectionThreshold: this.applicationSettings.DetectionThreshold,
				syncWord: this.applicationSettings.SyncWord
				);

#if DEBUG
			rfm9XDevice.RegisterDump();
#endif

			rfm9XDevice.Receive(Encoding.UTF8.GetBytes(this.applicationSettings.Address));

			LoggingFields loRaSettings = new LoggingFields();
			loRaSettings.AddString("Address", this.applicationSettings.Address);
			loRaSettings.AddDouble("Frequency", this.applicationSettings.Frequency);
			loRaSettings.AddBoolean("PABoost", this.applicationSettings.PABoost);

			loRaSettings.AddUInt8("MaxPower", this.applicationSettings.MaxPower);
			loRaSettings.AddUInt8("OutputPower", this.applicationSettings.OutputPower);
			loRaSettings.AddBoolean("OCPOn", this.applicationSettings.OCPOn);
			loRaSettings.AddUInt8("OCPTrim", this.applicationSettings.OCPTrim);

			loRaSettings.AddString("LnaGain", this.applicationSettings.LnaGain.ToString());
			loRaSettings.AddBoolean("lnaBoost", this.applicationSettings.LNABoost);

			loRaSettings.AddString("codingRate", this.applicationSettings.CodingRate.ToString());
			loRaSettings.AddString("implicitHeaderModeOn", applicationSettings.ImplicitHeaderModeOn.ToString());
			loRaSettings.AddString("spreadingFactor", this.applicationSettings.SpreadingFactor.ToString());
			loRaSettings.AddBoolean("rxPayloadCrcOn", true);

			loRaSettings.AddUInt8("symbolTimeout", this.applicationSettings.SymbolTimeout);
			loRaSettings.AddUInt8("preambleLength", this.applicationSettings.PreambleLength);
			loRaSettings.AddUInt8("payloadLength", this.applicationSettings.PayloadLength);

			loRaSettings.AddUInt8("payloadMaxLength", this.applicationSettings.PayloadMaxLength);
			loRaSettings.AddUInt8("freqHoppingPeriod", this.applicationSettings.FreqHoppingPeriod);
			loRaSettings.AddBoolean("lowDataRateOptimize", this.applicationSettings.LowDataRateOptimize);
			loRaSettings.AddUInt8("ppmCorrection", this.applicationSettings.PpmCorrection);

			loRaSettings.AddString("detectionOptimize", this.applicationSettings.DetectionOptimize.ToString());
			loRaSettings.AddBoolean("invertIQ", this.applicationSettings.InvertIQ);
			loRaSettings.AddString("detectionThreshold", this.applicationSettings.DetectionThreshold.ToString());
			loRaSettings.AddUInt8("SyncWord", this.applicationSettings.SyncWord);

			this.logging.LogEvent("LoRa configuration", loRaSettings, LoggingLevel.Information);

			this.deferral = taskInstance.GetDeferral();
		}

		private async void MqttClient_Disconnected(object sender, MqttClientDisconnectedEventArgs e)
		{
			LoggingFields mqttConnectRetry = new LoggingFields();

			mqttConnectRetry.AddString("InitialException", e.Exception.ToString());
			await Task.Delay(mqttReconnectDelay);

			try
			{
				await mqttClient.ConnectAsync(this.mqttOptions);
				this.logging.LogEvent("MqttClient_Disconnected reconnect success", mqttConnectRetry, LoggingLevel.Information);
			}
			catch (Exception ex)
			{
				mqttConnectRetry.AddString("RetryException", ex.ToString());
				this.logging.LogEvent("MqttClient_Disconnected reconnect failure", mqttConnectRetry, LoggingLevel.Error);
			}
		}

		private void MqttClient_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
		{
			LoggingFields messageHandlerLoggingFields = new LoggingFields();
#if DEBUG
			Debug.WriteLine($"{DateTime.UtcNow:HH:mm:ss}-MqttClient_ApplicationMessageReceived ClientId:{e.ClientId} Topic:{e.ApplicationMessage.Topic} Payload:{e.ApplicationMessage.ConvertPayloadToString()}");
#endif
			messageHandlerLoggingFields.AddString("ClientId", e.ClientId);
			messageHandlerLoggingFields.AddString("Topic", e.ApplicationMessage.Topic);
			messageHandlerLoggingFields.AddString("Payload", e.ApplicationMessage.ConvertPayloadToString());

			if (messageHandler != null)
			{
				try
				{
					messageHandler.MqttApplicationMessageReceived(sender, e);
				}
				catch (Exception ex)
				{
					messageHandlerLoggingFields.AddString("Exception", ex.ToString());
					this.logging.LogEvent("MqttClient_ApplicationMessageReceived MessageHandler failed", messageHandlerLoggingFields, LoggingLevel.Error);
					return;
				}
			}
			this.logging.LogEvent("MqttClient_ApplicationMessageReceived", messageHandlerLoggingFields, LoggingLevel.Information);
		}

		private void Rfm9XDevice_OnTransmit(object sender, Rfm9XDevice.OnDataTransmitedEventArgs e)
		{
			LoggingFields messageHandlerLoggingFields = new LoggingFields();
#if DEBUG
			Debug.WriteLine($"{DateTime.UtcNow:HH:mm:ss}-Rfm9XDevice_OnTransmit");
#endif
			if (messageHandler != null)
			{
				try
				{
					messageHandler.Rfm9xOnTransmit(sender, e);
				}
				catch (Exception ex)
				{
					messageHandlerLoggingFields.AddString("Exception", ex.ToString());
					this.logging.LogEvent("Rfm9XDevice_OnTransmit MessageHandler", messageHandlerLoggingFields, LoggingLevel.Error);
					return;
				}
			}
			this.logging.LogEvent("Rfm9XDevice_OnTransmit", messageHandlerLoggingFields, LoggingLevel.Information);
		}

		private void Rfm9XDevice_OnReceive(object sender, Rfm9XDevice.OnDataReceivedEventArgs e)
		{
			LoggingFields messageHandlerLoggingFields = new LoggingFields();
#if DEBUG
			Debug.WriteLine($"{DateTime.UtcNow:HH:mm:ss}-OnReceive From:{BitConverter.ToString(e.Address)} PacketSnr:{e.PacketSnr:0.0} Packet RSSI:{e.PacketRssi}dBm RSSI:{e.Rssi}dBm Length:{e.Data.Length}");
#endif
			messageHandlerLoggingFields.AddString("PacketSNR", e.PacketSnr.ToString("F1"));
			messageHandlerLoggingFields.AddInt32("PacketRSSI", e.PacketRssi);
			messageHandlerLoggingFields.AddInt32("RSSI", e.Rssi);

			string addressBcdText = BitConverter.ToString(e.Address);
			messageHandlerLoggingFields.AddInt32("DeviceAddressLength", e.Address.Length);
			messageHandlerLoggingFields.AddString("DeviceAddressBCD", addressBcdText);

			if (messageHandler != null)
			{
				try
				{
					messageHandler.Rfm9XOnReceive(sender, e);
				}
				catch (Exception ex)
				{
					messageHandlerLoggingFields.AddString("MessageHandler Exception", ex.ToString());
					this.logging.LogEvent("Rfm9XDevice_OnReceive", messageHandlerLoggingFields, LoggingLevel.Error);
					return;
				}
			}
			this.logging.LogEvent("Rfm9XDevice_OnReceive", messageHandlerLoggingFields, LoggingLevel.Information);
		}

		private class ApplicationSettings
		{
			[JsonProperty("MQTTServer", Required = Required.DisallowNull)]
			public string MqttServer { get; set; }

			[JsonProperty("MQTTUserName", Required = Required.Always)]
			public string MqttUserName { get; set; }

			[JsonProperty("MQTTPassword", Required = Required.Always)]
			public string MqttPassword { get; set; }

			[JsonProperty("MQTTClientID", Required = Required.Always)]
			public string MqttClientID { get; set; }

			// LoRa configuration parameters
			[JsonProperty("MessageHandlerAssembly", Required = Required.Always)]
			public string MessageHandlerAssembly { get; set; }

			// LoRa configuration parameters
			[JsonProperty("Address", Required = Required.Always)]
			public string Address { get; set; }

			[DefaultValue(Rfm9XDevice.FrequencyDefault)]
			[JsonProperty("Frequency", DefaultValueHandling = DefaultValueHandling.Populate)]
			public double Frequency { get; set; }

			// RegPaConfig
			[DefaultValue(Rfm9XDevice.PABoostDefault)]
			[JsonProperty("PABoost", DefaultValueHandling = DefaultValueHandling.Populate)]
			public bool PABoost { get; set; }

			[DefaultValue(Rfm9XDevice.RegPAConfigMaxPowerDefault)]
			[JsonProperty("MaxPower", DefaultValueHandling = DefaultValueHandling.Populate)]
			public byte MaxPower { get; set; }

			[DefaultValue(Rfm9XDevice.RegPAConfigOutputPowerDefault)]
			[JsonProperty("OutputPower", DefaultValueHandling = DefaultValueHandling.Populate)]
			public byte OutputPower { get; set; }

			// RegOcp
			[DefaultValue(Rfm9XDevice.RegOcpDefault)]
			[JsonProperty("OCPOn", DefaultValueHandling = DefaultValueHandling.Populate)]
			public bool OCPOn { get; set; }

			[DefaultValue(Rfm9XDevice.RegOcpOcpTrimDefault)]
			[JsonProperty("OCPTrim", DefaultValueHandling = DefaultValueHandling.Populate)]
			public byte OCPTrim { get; set; }

			// RegLna
			[DefaultValue(Rfm9XDevice.LnaGainDefault)]
			[JsonProperty("LNAGain", DefaultValueHandling = DefaultValueHandling.Populate)]
			[JsonConverter(typeof(StringEnumConverter))]
			public Rfm9XDevice.RegLnaLnaGain LnaGain { get; set; }

			[DefaultValue(Rfm9XDevice.LnaBoostDefault)]
			[JsonProperty("LNABoost", DefaultValueHandling = DefaultValueHandling.Populate)]
			public bool LNABoost { get; set; }

			// RegModemConfig1
			[DefaultValue(Rfm9XDevice.RegModemConfigBandwidthDefault)]
			[JsonProperty("Bandwidth", DefaultValueHandling = DefaultValueHandling.Populate)]
			[JsonConverter(typeof(StringEnumConverter))]
			public Rfm9XDevice.RegModemConfigBandwidth Bandwidth { get; set; }

			[DefaultValue(Rfm9XDevice.RegModemConfigCodingRateDefault)]
			[JsonProperty("codingRate", DefaultValueHandling = DefaultValueHandling.Populate)]
			[JsonConverter(typeof(StringEnumConverter))]
			public Rfm9XDevice.RegModemConfigCodingRate CodingRate { get; set; }

			[DefaultValue(Rfm9XDevice.RegModemConfigImplicitHeaderModeOnDefault)]
			[JsonProperty("ImplicitHeaderModeOn", DefaultValueHandling = DefaultValueHandling.Populate)]
			[JsonConverter(typeof(StringEnumConverter))]
			public Rfm9XDevice.RegModemConfigImplicitHeaderModeOn ImplicitHeaderModeOn { get; set; }

			// RegModemConfig2SpreadingFactor
			[DefaultValue(Rfm9XDevice.RegModemConfig2SpreadingFactorDefault)]
			[JsonProperty("SpreadingFactor", DefaultValueHandling = DefaultValueHandling.Populate)]
			public Rfm9XDevice.RegModemConfig2SpreadingFactor SpreadingFactor { get; set; }

			[DefaultValue(Rfm9XDevice.SymbolTimeoutDefault)]
			[JsonProperty("SymbolTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
			public byte SymbolTimeout { get; set; }

			[DefaultValue(Rfm9XDevice.PreambleLengthDefault)]
			[JsonProperty("PreambleLength", DefaultValueHandling = DefaultValueHandling.Populate)]
			public byte PreambleLength { get; set; }

			[DefaultValue(Rfm9XDevice.PayloadLengthDefault)]
			[JsonProperty("PayloadLength", DefaultValueHandling = DefaultValueHandling.Populate)]
			public byte PayloadLength { get; set; }

			[DefaultValue(Rfm9XDevice.PayloadMaxLengthDefault)]
			[JsonProperty("PayloadMaxLength", DefaultValueHandling = DefaultValueHandling.Populate)]
			public byte PayloadMaxLength { get; set; }

			[DefaultValue(Rfm9XDevice.FreqHoppingPeriodDefault)]
			[JsonProperty("freqHoppingPeriod", DefaultValueHandling = DefaultValueHandling.Populate)]
			public byte FreqHoppingPeriod { get; set; }

			[DefaultValue(Rfm9XDevice.LowDataRateOptimizeDefault)]
			[JsonProperty("LowDataRateOptimize", DefaultValueHandling = DefaultValueHandling.Populate)]
			public bool LowDataRateOptimize { get; set; }

			[DefaultValue(Rfm9XDevice.AgcAutoOnDefault)]
			[JsonProperty("AgcAutoOn", DefaultValueHandling = DefaultValueHandling.Populate)]
			public byte AgcAutoOn { get; set; }

			[DefaultValue(Rfm9XDevice.ppmCorrectionDefault)]
			[JsonProperty("PPMCorrection", DefaultValueHandling = DefaultValueHandling.Populate)]
			public byte PpmCorrection { get; set; }

			[DefaultValue(Rfm9XDevice.RegDetectOptimizeDectionOptimizeDefault)]
			[JsonProperty("DetectionOptimize", DefaultValueHandling = DefaultValueHandling.Populate)]
			public Rfm9XDevice.RegDetectOptimizeDectionOptimize DetectionOptimize { get; set; }

			[DefaultValue(Rfm9XDevice.InvertIqDefault)]
			[JsonProperty("InvertIQ", DefaultValueHandling = DefaultValueHandling.Populate)]
			public bool InvertIQ { get; set; }

			[DefaultValue(Rfm9XDevice.RegisterDetectionThresholdDefault)]
			[JsonProperty("DetectionThreshold", DefaultValueHandling = DefaultValueHandling.Populate)]
			public Rfm9XDevice.RegisterDetectionThreshold DetectionThreshold { get; set; }

			[DefaultValue(Rfm9XDevice.RegSyncWordDefault)]
			[JsonProperty("SyncWord", DefaultValueHandling = DefaultValueHandling.Populate)]
			public byte SyncWord { get; set; }
		}
	}
}
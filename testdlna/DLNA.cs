using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace testdlna {
	class DLNA {
		internal static string LogFile    = Application.ExecutablePath + ".log"; // Файл лога, куда будут записаны ответы и сообщения, если указан ключ -d
		internal static int    MaxLogSize = 1024 * 1024 * 2; // Максимальный размер файла лога (2 МБ)

		public UPnPDeviceList  Devices        = new UPnPDeviceList();
		public UPnPDevice      SelectedDevice = new UPnPDevice("");

		private HttpWebRequest Request        = null;

		private string RequestPayload = "";
		private string SoapCommand    = "";
		private string SoapService    = "";
		private string NameSpace      = "";

        public string ResponseText = "";
		public string ResponseBody = "";
		public string UserAgent    = "";
		public string MIMEtype     = "";

		public bool Debug = false;

		public DLNA() {
		}

		private void CreateRequest(string url, string method="GET") {
			Request = (HttpWebRequest)WebRequest.Create(url);
			Request.Method      = method;
			Request.ContentType = "text/xml; charset=\"utf-8\"";
			Request.Credentials = CredentialCache.DefaultCredentials;
			Request.Expect      = "";
			Request.UserAgent   = UserAgent;
			Request.Headers.Set(HttpRequestHeader.AcceptEncoding, "gzip");
        }

		private void SetSOAPAction(string serviceName, string cmd) {
			SoapService = serviceName;
			SoapCommand = cmd;
			try {
				UPnPService service = SelectedDevice.GetService(serviceName);
				NameSpace = service.ServiceType;
				CreateRequest(service.ControlUrl, "POST");
				Request.Headers.Set("SOAPACTION", "\""+ service.ServiceType + "#" + cmd + "\"");
			} catch {
				SelectedDevice.Ready = false;
			}
		}

		public void SetFile(string fileURI, string name, string time, string date) {
			SetSOAPAction("AVTransport", "SetAVTransportURI");
			string didlMeta = GetDIDLVideoMetadata(fileURI, name, time, date);
			string metaData = "<CurrentURIMetaData>"+ WebUtility.HtmlEncode(didlMeta) + "</CurrentURIMetaData>";
			SetPayload("CurrentURI", fileURI, metaData);
			Send();
		}

		public void Play(int speed = 1) {
			SetSOAPAction("AVTransport", "Play");
			SetPayload("Speed", speed.ToString());
			Send();
		}

		public void Stop() {
			SetSOAPAction("AVTransport", "Stop");
			SetPayload();
			Send();
		}

		private void Send() {
			if (!SelectedDevice.Ready) return;
			//Write the payload to the request body.
			if (RequestPayload.Length > 0) {
				using (Stream requestStream = Request.GetRequestStream()) {
					requestStream.Write(Encoding.UTF8.GetBytes(RequestPayload), 0, Encoding.UTF8.GetByteCount(RequestPayload));
				}
			}
			GetResponse();
		}

		private string GetResponse(string url = "") {
			ResponseText = "";
			ResponseBody = "";
			WebResponse  response      = null;
			StreamReader readStream    = null;
			Stream       receiveStream = null;
			try {
				if (url.Length > 0) CreateRequest(url);
				response      = Request.GetResponse();
				receiveStream = response.GetResponseStream();
				readStream    = new StreamReader(receiveStream, Encoding.UTF8);
				ResponseBody  = readStream.ReadToEnd();

			} catch (WebException e) {
				Console.WriteLine("Ошибка: " + e.Message + " Статус: " + e.Status.ToString());
				if (e.Response != null) {
					ResponseText = new StreamReader(e.Response.GetResponseStream()).ReadToEnd();
				}

			} catch (Exception e) {
				Console.WriteLine(e.ToString());

			} finally {
				if (readStream    != null) readStream   .Close();
				if (receiveStream != null) receiveStream.Close();
				if (response      != null) response     .Close();
			}
			if (Debug) {
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("Current SOAP action: " + SoapService + "#" + SoapCommand);
				sb.AppendLine("Request url: " + Request.RequestUri.AbsoluteUri);
				sb.AppendLine("Request payload: " + RequestPayload);
				sb.AppendLine("ResponseText: " + ResponseText);
				sb.AppendLine("ResponseBody: " + ResponseBody);
				LogMe(sb.ToString());
			}
			return ResponseBody;
		}

		public void SearchRenderers(bool output2console = false) {
			IPEndPoint localEndPoint     = new IPEndPoint(IPAddress.Any, 0);
			IPEndPoint multicastEndPoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);

			Socket udpSocket = null;
			StringBuilder sb = new StringBuilder();

			try {
				udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

				udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multicastEndPoint.Address, IPAddress.Any));
				udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
				udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);

				udpSocket.Bind(localEndPoint);

				string SearchString = "M-SEARCH * HTTP/1.1\r\nHOST:239.255.255.250:1900\r\nMAN:\"ssdp:discover\"\r\nST:upnp:rootdevice\r\nMX:3\r\nUSER-AGENT: unix/5.1 UPnP/1.0 testdlna/1.0\r\n\r\n";

				udpSocket.SendTo(Encoding.UTF8.GetBytes(SearchString), SocketFlags.None, multicastEndPoint);

				if (output2console) Console.WriteLine("M-Search sent...\r\n");
				System.Threading.Thread.Sleep(3000);

				byte[] ReceiveBuffer = new byte[64000];
				int ReceivedBytes = 0;

				while (udpSocket.Available > 0) {
					ReceivedBytes = udpSocket.Receive(ReceiveBuffer, SocketFlags.None);
					if (ReceivedBytes > 0)
						sb.Append(Encoding.UTF8.GetString(ReceiveBuffer, 0, ReceivedBytes));
				}
			} finally {
				if (udpSocket != null)
					udpSocket.Close();
            }
			string answer = sb.ToString();

			if (output2console) Console.WriteLine(answer);

			MatchCollection mc = Regex.Matches(answer, @"HTTP/.*?\r\n\r\n", RegexOptions.Singleline);
			foreach(Match m in mc) {
				string usn = GetAnswerValue(m.Value, "USN");
				if (!Devices.Exists(usn)) {
					UPnPDevice device = new UPnPDevice(usn);
					device.Type     = GetAnswerValue(m.Value, "ST");
					device.Location = GetAnswerValue(m.Value, "LOCATION");
					device.Server   = GetAnswerValue(m.Value, "SERVER");
					device.HeadersInfo = m.Value;
					device.DeviceAddr  = Regex.Match(device.Location, @"^.*?//[^/]+").Value;

                    if (device.Location.Length > 0) {
						string info = GetResponse(device.Location);
						device.Name         = GetXmlValue(info, "friendlyName");
						device.Manufacturer = GetXmlValue(info, "manufacturer");
						device.ModelName    = GetXmlValue(info, "modelName"   );
						device.UDN          = GetXmlValue(info, "UDN"         );
                    	device.Type         = GetXmlValue(info, "deviceType"  );
						MatchCollection mcServices = Regex.Matches(info, @"<service>.*?</service>", RegexOptions.Singleline);
						foreach (Match s in mcServices) {
							UPnPService service = new UPnPService();
							service.ServiceType = GetXmlValue(s.Value, "serviceType");
							service.ServiceID   = GetXmlValue(s.Value, "serviceId"  );
							service.SCPDURL     = device.DeviceAddr + GetXmlValue(s.Value, "SCPDURL"    );
							service.ControlUrl  = device.DeviceAddr + GetXmlValue(s.Value, "controlURL" );
							service.EventSubURL = device.DeviceAddr + GetXmlValue(s.Value, "eventSubURL");
							device.Services.Add(service);
							if (service.ServiceType.IndexOf("AVTransport")>0) device.Ready = true;
                        }
					}

					Devices.Add(device);
				}
			}
		}

		public bool SelectDevice(string key) {
			SelectedDevice = Devices.Select(key);
			return (SelectedDevice.Name.Length > 0);
		}

		private string GetAnswerValue(string answer, string name) {
			return Regex.Match(answer, name+":(.*?)[\r\n]").Groups[1].Value.Trim();
        }

		private string GetXmlValue(string xml, string name) {
			return Regex.Match(xml, "<"+name+">(.*?)</").Groups[1].Value;
		}

		private void SetPayload(string parameterName = "", string parameterValue = "", string additionalData = "") {
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
			sb.AppendLine("<s:Envelope s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\" xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\">");
			sb.AppendLine("   <s:Body>");
			sb.AppendLine("      <u:" + SoapCommand + " xmlns:u=\"" + NameSpace + "\">");
			if (SoapService=="AVTransport") sb.AppendLine  ("         <InstanceID>0</InstanceID>");
			if (parameterName .Length > 0 ) sb.AppendFormat("         <" + parameterName + ">{0}</" + parameterName + ">\r\n", parameterValue);
			if (additionalData.Length > 0 ) sb.AppendLine  ("         "  + additionalData);
			sb.AppendLine("      </u:" + SoapCommand + ">");
			sb.AppendLine("   </s:Body>");
			sb.AppendLine("</s:Envelope>");
			RequestPayload = sb.ToString();
		}

		private string GetDIDLVideoMetadata(string fileUri, string title, string time, string date, int size = 0, string itemId = "advert", string parentId = "0", int restricted = 1) {
			string mime = (MIMEtype.Length > 0) ? MIMEtype : GetMIMEfromExt(Path.GetExtension(fileUri));
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("<DIDL-Lite xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\" xmlns:dlna=\"urn:schemas-dlna-org:metadata-1-0/\">");
			sb.AppendLine("<item id=\"" + itemId + "\" parentID=\"" + parentId + "\" restricted=\"" + restricted + "\">");
			sb.AppendLine("<upnp:storageMedium>UNKNOWN</upnp:storageMedium>");
			sb.AppendLine("<upnp:writeStatus>UNKNOWN</upnp:writeStatus>");
			sb.AppendLine("<dc:title>" + title + "</dc:title>");
			sb.AppendLine("<upnp:class>object.item.videoItem</upnp:class>");
			sb.AppendLine("<dc:date>" + date + "</dc:date>");
			sb.AppendLine("<res protocolInfo=\"http-get:*:" + mime + ":*\" duration=\"" + time + "\" size=\"" + size + "\">" + fileUri + "</res>");
			sb.AppendLine("</item></DIDL-Lite>");
			return sb.ToString();
		}

		private string GetMIMEfromExt(string extension) {
			string mime;
			return VideoMIMEmappings.TryGetValue(extension, out mime) ? mime : "video/mp4";
		}

		private static IDictionary<string, string> VideoMIMEmappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
			#region Big freaking list of mime types
			{".3g2" , "video/3gpp"},
			{".3gp" , "video/3gpp"},
			{".3gp2", "video/3gpp2"},
			{".3gpp", "video/3gpp"},
			{".asf" , "video/x-ms-asf"},
			{".asr" , "video/x-ms-asf"},
			{".asx" , "video/x-ms-asf"},
			{".avi" , "video/avi"},
			{".dif" , "video/x-dv"},
			{".divx", "video/avi"},
			{".dvr-ms","video/x-ms-dvr"},
            {".dv"  , "video/x-dv"},
			{".evo" , "video/mpeg"},
            {".flv" , "video/x-flv"},
			{".IVF" , "video/x-ivf"},
			{".lsf" , "video/x-la-asf"},
			{".lsx" , "video/x-la-asf"},
			{".m1v" , "video/mpeg"},
			{".m2t" , "video/vnd.dlna.mpeg-tts"},
			{".m2ts", "video/vnd.dlna.mpeg-tts"},
			{".m2v" , "video/mpeg"},
			{".m4v" , "video/mp4"},
			{".mkv" , "video/x-matroska"},
            {".mod" , "video/mpeg"},
			{".mov" , "video/quicktime"},
			{".movie","video/x-sgi-movie"},
			{".mp2v", "video/mpeg"},
			{".mp4" , "video/mp4"},
			{".mp4v", "video/mp4"},
			{".mpe" , "video/mpeg"},
			{".mpeg", "video/mpeg"},
			{".mpg" , "video/mpeg"},
			{".mpv2", "video/mpeg"},
			{".mqv" , "video/quicktime"},
			{".mts" , "video/vnd.dlna.mpeg-tts"},
			{".ogm" , "video/x-ogm"},
			{".nsc" , "video/x-ms-asf"},
			{".qt"  , "video/quicktime"},
			{".smpg"  , "video/x-mpegurl"},
			{".ssif"  , "video/vnd.dlna.mpeg-tts"},
			{".tp"  , "video/mpeg"},
			{".ts"  , "video/vnd.dlna.mpeg-tts"},
			{".tts" , "video/vnd.dlna.mpeg-tts"},
			{".vbk" , "video/mpeg"},
			{".vob" , "video/mpeg"},
			{".webm" , "video/webm"},
			{".wm"  , "video/x-ms-wm"},
			{".wmp" , "video/x-ms-wmp"},
			{".wmv" , "video/x-ms-wmv"},
			{".wmx" , "video/x-ms-wmx"},
			{".wvx" , "video/x-ms-wvx"},
			{".wtv" , "video/wtv"},
			{".xvid" , "video/avi"},

			{".aac" , "audio/vnd.dlna.adts"},
			{".ac3" , "audio/ac3"},
			{".ADT" , "audio/vnd.dlna.adts"},
			{".ADTS" , "audio/vnd.dlna.adts"},
			{".aif" , "audio/aiff"},
			{".aifc" , "audio/aiff"},
			{".aiff" , "audio/aiff"},
			{".amr" , "audio/amr"},
			{".ape" , "audio/x-ape"},
			{".au" , "audio/basic"},
			{".awb" , "audio/awb"},
			{".cda" , "audio/cda"},
			{".dff" , "audio/x-dff"},
			{".dsf" , "audio/x-dsf"},
			{".dts" , "audio/dts"},
			{".flac" , "audio/x-flac"},
			{".m3u" , "audio/x-mpegurl"},
			{".m4a" , "audio/mp4"},
			{".mid" , "audio/mid"},
			{".midi" , "audio/mid"},
			{".mka" , "audio/x-matroska"},
			{".mp1" , "audio/mpeg"},
			{".mp2" , "audio/mpeg"},
			{".mp3" , "audio/mpeg"},
			{".mpa" , "audio/mpeg"},
			{".rmi" , "audio/mid"},
			{".s16be" , "audio/L16"},
			{".smp3" , "audio/x-mpegurl"},
			{".snd" , "audio/basic"},
			{".wav" , "audio/x-wav"},
			{".wax" , "audio/x-ms-wax"},
			{".wma" , "audio/x-ms-wma"},

			{ ".bmp" , "image/bmp"},
			{ ".bw" , "image/bw"},
			{ ".cel" , "image/cel"},
			{ ".cut" , "image/cut"},
			{ ".dib" , "image/dib"},
			{ ".emf" , "image/emf"},
			{ ".eps" , "image/eps"},
			{ ".fax" , "image/fax"},
			{ ".gif" , "image/gif"},
			{ ".icb" , "image/icb"},
			{ ".ico" , "image/x-icon"},
			{ ".jfif" , "image/jpeg"},
			{ ".jpe" , "image/jpeg"},
			{ ".jpeg" , "image/jpeg"},
			{ ".jpg" , "image/jpeg"},
			{ ".jps" , "image/x-jps|image/jpeg"},
			{ ".pbm" , "image/pbm"},
			{ ".pcc" , "image/pcc"},
			{ ".pcd" , "image/pcd"},
			{ ".pcx" , "image/pcx"},
			{ ".pdd" , "image/pdd"},
			{ ".pgm" , "image/pgm"},
			{ ".pic" , "image/pic"},
			{ ".png" , "image/png"},
			{ ".pns" , "iimage/pns|image/png"},
			{ ".ppm" , "image/ppm"},
			{ ".psd" , "image/psd"},
			{ ".psp" , "image/psp"},
			{ ".rgb" , "image/rgb"},
			{ ".rgba" , "image/rgba"},
			{ ".rla" , "image/rla"},
			{ ".rle" , "image/rle"},
			{ ".rpf" , "image/rpf"},
			{ ".scr" , "image/scr"},
			{ ".sgi" , "image/sgi"},
			{ ".svg" , "image/svg+xml"},
			{ ".tga" , "image/tga"},
			{ ".tif" , "image/tiff"},
			{ ".tiff" , "image/tiff"},
			{ ".wdp" , "image/vnd.ms-photo"}
			#endregion
        };

		/// <summary>
		/// Запись в лог-файл сообщения
		/// </summary>
		/// <param name="msg">Сообщение, которое будет записано в лог-файл</param>
		public static void LogMe(string msg) {
			if (File.Exists(LogFile)) {
				FileInfo fileInfo = new FileInfo(LogFile);
				// Если он такой большой, значит забытый и не нужный - удаляем, чтобы начать всё заного
				if (fileInfo.Length > MaxLogSize)
					File.Delete(LogFile);
			}
			File.AppendAllText(LogFile, DateTime.Now.ToString() + " " + msg + "\n");
		}
	}

	class UPnPDevice {
		public string USN  = "";
		public string UDN  = "";
		public string Name = "";
		public string Type = "";
		public string Manufacturer = "";
		public string ModelName    = "";
		public string Location     = "";
		public string Server       = "";
		public string HeadersInfo  = "";
		public string DeviceAddr   = "";
		public bool   Ready        = false;

		public UPnPServiceList Services = new UPnPServiceList();

		public UPnPService GetService(string serviceName) {
			foreach (UPnPService s in Services)
				if (s.ServiceType.IndexOf(serviceName)>=0) return s;
			return new UPnPService();
		}

		public UPnPDevice(string usn) {
			USN = usn;
		}

		public override string ToString() { return Name + " (" + DeviceAddr + ")"; }
	}

	class UPnPService {
        public string ServiceType  = "";
		public string ServiceID    = "";
		public string SCPDURL      = "";
		public string ControlUrl   = "";
		public string EventSubURL  = "";
		public override string ToString() { return ServiceType + " (" + ControlUrl + ")"; }
	}

	class UPnPDeviceList : List<UPnPDevice> {
		public bool Exists(string usn) {
			foreach (var device in this)
				if (device.USN == usn) return true;
			return false;
		}

		public UPnPDevice Select(string keyword) {
			keyword = keyword.ToLower();
			foreach (UPnPDevice d in this) {
				string allInfo = d.HeadersInfo + d.Manufacturer + d.Name + d.ModelName + d.UDN;
                if (allInfo.ToLower().IndexOf(keyword) >= 0) return d;
			}
			return new UPnPDevice("");
        }
	}

	class UPnPServiceList: List<UPnPService> {
	}

}

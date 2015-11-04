using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace testdlna {
	internal sealed class DLNA {
		public HttpWebRequest Request;
		private string deviceAddr = "";
		private string requestPayload = "";

		private string soapService = "";
		private string soapCommand = "";

		public string responseText = "";
		public string responseBody = "";
		public string UserAgent    = "";

		public DLNA(string deviceIP, string devicePort) {
			deviceAddr = "http://" + deviceIP + ":" + devicePort;
		}

		private void SetSOAPAction(string service, string cmd) {
			soapService = service;
			soapCommand = cmd;
			Request = (HttpWebRequest)WebRequest.Create(deviceAddr + "/upnp/control/" + service);
			Request.KeepAlive = false;
			//Request.Connection  = "Close";
			Request.Method      = "POST";
			Request.ContentType = "text/xml; charset=\"utf-8\"";
			Request.Credentials = CredentialCache.DefaultCredentials;
			Request.Headers.Set(HttpRequestHeader.AcceptEncoding, "gzip");
			Request.Headers.Set("SOAPACTION", "\"urn:schemas-upnp-org:service:"+ soapService + ":1#" + soapCommand + "\"");
			Request.Expect      = "";
			if (UserAgent.Length > 0)
				Request.UserAgent = UserAgent;
		}

		public void SetFile(string fileURI, string name, string time, string date) {
			SetSOAPAction("AVTransport", "SetAVTransportURI");
			string didlMeta = GetDIDLVideoMetadata(fileURI, name, time, date);
			string xml = "<CurrentURI>" + fileURI + "</CurrentURI>\n        <CurrentURIMetaData>"+ WebUtility.HtmlEncode(didlMeta) + "</CurrentURIMetaData>";
			SetPayload(soapCommand, xml);
			Send();
        }

		public void Play(int speed = 1) {
			SetSOAPAction("AVTransport", "Play");
			SetPayload(soapCommand, "<Speed>"+speed+"</Speed>");
			Send();
		}

		private void SetPayload(string cmd, string xmlBody = "", int InstanceID = 0) {
			string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
						 "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\n" +
						 "   <s:Body>\n"    +
						 "      <u:{1} xmlns:u=\"urn:schemas-upnp-org:service:{0}:1\">\n" +
						 "        {2}\n"    +
						 "      </u:{1}>\n" +
						 "   </s:Body>\n"   +
						 "</s:Envelope>";
			if (xmlBody.Length > 0)
				xmlBody = "<InstanceID>" + InstanceID + "</InstanceID>\n        " + xmlBody;
			requestPayload = string.Format(xml, soapService, cmd, xmlBody);
		}


		private string GetMIMEfromExt(string extension) {
			string mime;
			return VideoMIMEmappings.TryGetValue(extension, out mime) ? mime : "application/octet-stream";
		}

		private string GetDIDLVideoMetadata(string fileUri, string title, string time, string date) {
			string mime = GetMIMEfromExt(Path.GetExtension(fileUri));
            return
				"<DIDL-Lite xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\" xmlns:dlna=\"urn:schemas-dlna-org:metadata-1-0/\">" +
				"<item id=\"advert\" parentID=\"18\" restricted=\"1\">" +
				"<upnp:storageMedium>UNKNOWN</upnp:storageMedium>" +
				"<upnp:writeStatus>UNKNOWN</upnp:writeStatus>" +
				"<dc:title>"+ title + "</dc:title>" +
				"<upnp:class>object.item.videoItem</upnp:class>" +
				"<dc:date>"+ date + "</dc:date>" +
				"<res protocolInfo=\"http-get:*:"+mime+":*\" duration=\""+time+"\" size=\"0\">" + fileUri + "</res></item></DIDL-Lite>";
		}

		private void Send() {
			UTF8Encoding encoding = new UTF8Encoding();
			responseText = "";
			responseBody = "";

			try {
				//Write the payload to the request body.
				using (Stream requestStream = Request.GetRequestStream()) {
					requestStream.Write(encoding.GetBytes(requestPayload), 0, encoding.GetByteCount(requestPayload));
				}

				HttpWebResponse response = Request.GetResponse() as HttpWebResponse;
                using (Stream rspStm = response.GetResponseStream()) {
					using (StreamReader reader = new StreamReader(rspStm)) {
						responseText += "Response Description: " + response.StatusDescription;
						responseText += "Response Status Code: " + response.StatusCode;
						responseText += "\r\n\r\n";
						responseBody = reader.ReadToEnd();
					}
				}
				responseText = "Success: " + response.StatusCode.ToString();

			} catch (Exception ex) {
				responseText += ex.ToString() + "\r\n";
				WebException wex = ex as WebException;
				if (wex != null) {
					using (var sr = new StreamReader(wex.Response.GetResponseStream())) {
						responseText = sr.ReadToEnd().Trim();
						sr.Close();
					}
				}
            }
        }

		private static IDictionary<string, string> VideoMIMEmappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {

			#region Big freaking list of mime types
			{".3g2" , "video/3gpp2"},
			{".3gp" , "video/3gpp"},
			{".3gp2", "video/3gpp2"},
			{".3gpp", "video/3gpp"},
			{".asf" , "video/x-ms-asf"},
			{".asr" , "video/x-ms-asf"},
			{".asx" , "video/x-ms-asf"},
			{".avi" , "video/x-msvideo"},
			{".dif" , "video/x-dv"},
			{".dv"  , "video/x-dv"},
			{".flv" , "video/x-flv"},
			{".IVF" , "video/x-ivf"},
			{".lsf" , "video/x-la-asf"},
			{".lsx" , "video/x-la-asf"},
			{".m1v" , "video/mpeg"},
			{".m2t" , "video/vnd.dlna.mpeg-tts"},
			{".m2ts", "video/vnd.dlna.mpeg-tts"},
			{".m2v" , "video/mpeg"},
			{".m4v" , "video/x-m4v"},
			{".mod" , "video/mpeg"},
			{".mov" , "video/quicktime"},
			{".movie","video/x-sgi-movie"},
			{".mp2" , "video/mpeg"},
			{".mp2v", "video/mpeg"},
			{".mp4" , "video/mp4"},
			{".mp4v", "video/mp4"},
			{".mpa" , "video/mpeg"},
			{".mpe" , "video/mpeg"},
			{".mpeg", "video/mpeg"},
			{".mpg" , "video/mpeg"},
			{".mpv2", "video/mpeg"},
			{".mqv" , "video/quicktime"},
			{".mts" , "video/vnd.dlna.mpeg-tts"},
			{".nsc" , "video/x-ms-asf"},
			{".qt"  , "video/quicktime"},
			{".ts"  , "video/vnd.dlna.mpeg-tts"},
			{".tts" , "video/vnd.dlna.mpeg-tts"},
			{".vbk" , "video/mpeg"},
			{".wm"  , "video/x-ms-wm"},
			{".wmp" , "video/x-ms-wmp"},
			{".wmv" , "video/x-ms-wmv"},
			{".wmx" , "video/x-ms-wmx"},
			{".wvx" , "video/x-ms-wvx"},
			#endregion

        };

	}
}

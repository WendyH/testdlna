using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;

namespace testdlna {
	class Program {
		static void Main(string[] args) {

			string userAgent  = "SEC_HHP_Flipps/1.0";
			string deviceIP   = "192.168.2.3";
			string devicePort = "52235";
			string fileURI    = "http://5.9.84.176:8080/app/2LES5e/sd0lGeslRbp8GCXTHsdpEE3jfNaogpnMB68c8bzqbIWRwR3xz8TPVxIx76W+7ijJPgNdKSL0fadYXoPyIdgXco9FWmwFnqVm703hEhet23JILKNo/A1xDVyAAgzwZnyBXqmqwRpbnG3gKK1+hU+ix5j9L5/yvOlSREqLIZ9baL3ozyliyKif0IL53g8wUKmr2RVJx+mimjtXgVttg/oa3myKT4nW4OvvL2n8vKJQxZvcQf3U57o722gGswA8XkcgEKOVjKS0zOC6eAG/+68YWeXegcgtwgeVkcNKMV6B9iO60x+A1lldRH+PwNLBCdahHBKyeJM0ZEIkLsUg3F9eGwYW9EN44GpgV6Wmb0+cn1wnljVtNhPpxDWQQMZ2+H2vpTBD/uChg/Vea8GpRkO+IA4EdLruyYgSAS6LS4q0R2ZrzKxVOszbtHucM.mp4";

			if (args.Length > 0) deviceIP   = args[0];
			if (args.Length > 1) devicePort = args[1];
			if (args.Length > 2) userAgent  = args[2];
			if (args.Length > 3) fileURI    = args[3];

			Console.WriteLine("Устройство: " + deviceIP + ":" + devicePort);
			Console.WriteLine("Файл: " + fileURI);
			Console.WriteLine("");

			DLNA dlnaDevice = new DLNA(deviceIP, devicePort);

			dlnaDevice.UserAgent = userAgent;

			Console.WriteLine("Поиск устройств воспроизведения...");

			dlnaDevice.SearchRenderers();

			Console.WriteLine("Установка адреса файла...");

			dlnaDevice.SetFile(fileURI, "Advertisement", "00:00:30.000", "2015-11-04T11:54:23");

			Console.WriteLine("Ответ от устройства: \n" + dlnaDevice.responseText);
			Console.WriteLine(dlnaDevice.responseBody);

			Console.WriteLine("Play...");

			dlnaDevice.Play();

			Console.WriteLine("Ответ от устройства: \n" + dlnaDevice.responseText);
			Console.WriteLine(dlnaDevice.responseBody);
		}
	}
}

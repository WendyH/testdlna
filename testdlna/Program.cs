using System;
using System.Text.RegularExpressions;
using System.Threading;
using Bend.Util;
using System.Net;

namespace testdlna {
	class Program {

		static void Main(string[] args) {
    
			bool   debug      = false;                 // флаг отладки (ведения лог-файла)
			string MIMEtype   = "";                    // Если установлено - MIME-тип, который будет принудительно (без автоопределения) передан
			string userAgent  = "SEC_HHP_Flipps/1.0";  // User-Agent для запросов устройству
			string deviceIP   = "192.168.2.3";         // IP адрес устройства, которому будет отправлен файл (вместо IP может быть любое ключевое слово, по которому можно будет идентифицировать устройство)
			string fileURI    = "http://5.9.84.176:8080/app/2LES5e/sd0lGeslRbp8GCXTHsdpEE3jfNaogpnMB68c8bzqbIWRwR3xz8TPVxIx76W+7ijJPgNdKSL0fadYXoPyIdgXco9FWmwFnqVm703hEhet23JILKNo/A1xDVyAAgzwZnyBXqmqwRpbnG3gKK1+hU+ix5j9L5/yvOlSREqLIZ9baL3ozyliyKif0IL53g8wUKmr2RVJx+mimjtXgVttg/oa3myKT4nW4OvvL2n8vKJQxZvcQf3U57o722gGswA8XkcgEKOVjKS0zOC6eAG/+68YWeXegcgtwgeVkcNKMV6B9iO60x+A1lldRH+PwNLBCdahHBKyeJM0ZEIkLsUg3F9eGwYW9EN44GpgV6Wmb0+cn1wnljVtNhPpxDWQQMZ2+H2vpTBD/uChg/Vea8GpRkO+IA4EdLruyYgSAS6LS4q0R2ZrzKxVOszbtHucM.mp4";
			//string fileURI = "http://www.youtube.com/watch?v=25pzrnf8xyk";
			if (args.Length > 0) deviceIP   = args[0]; // Первый параметр, елси указан - IP устройства, которому будет отправлен файл
			if (args.Length > 1) {                     // Если указаны другие параметры, проверяем их
				fileURI   = GetKey(args, "-(f|file)"     , fileURI  ); // Отправляемый файл на телек
				MIMEtype  = GetKey(args, "-(m|mime)"     , MIMEtype ); // MIME-тип передаваемого файла
				userAgent = GetKey(args, "-(a|useragent)", userAgent); // User-Agent, который будет использован для общения с устройством
				debug     = ChkKey(args, "-d");        // Режим отладки - запись ответов от устройства в лог-файл
			}

			fileURI = CheckKnownLinks(fileURI);

			/*
			HttpServer httpServer = new MyHttpServer(8368);
			Thread thread = new Thread(new ThreadStart(httpServer.listen));
			thread.Start();

			Console.WriteLine("Press any key...");
			Console.ReadKey();
			return;
			*/

			DLNA dlna = new DLNA();

			dlna.Debug     = debug;
			dlna.UserAgent = userAgent;
			dlna.MIMEtype  = MIMEtype;

			Console.WriteLine("Устройство: " + deviceIP);
			Console.WriteLine("Файл: " + fileURI);
			Console.WriteLine("User-Agent: " + userAgent);
			if (MIMEtype!="")
				Console.WriteLine("MIME-Type: " + MIMEtype);
			Console.WriteLine("");

			Console.WriteLine("Поиск устройств воспроизведения...");
			dlna.SearchRenderers(true);

			if (!dlna.SelectDevice(deviceIP)) {
				System.Threading.Thread.Sleep(2000);
				dlna.SearchRenderers();
			}

			if (dlna.SelectDevice(deviceIP))
				Console.WriteLine("Выбрано устройство: {0}", dlna.SelectedDevice.Name);
			else {
				Console.WriteLine("Устройство {0} не найдено.", deviceIP);
				return;
			}

			if (!dlna.SelectedDevice.Ready) {
				Console.WriteLine("Выбранное устройство не готово или не предназначено для воспроизведения видео.");
				return;
			}

			dlna.Stop();

			Console.WriteLine("Установка адреса файла...");
			dlna.SetFile(fileURI, "Advertisement", "02:00:00.000", "2015-11-04T11:54:23");

			Console.WriteLine("Play...");
			dlna.Play();

			//Console.WriteLine("Press any key...");
			//Console.ReadKey();
		}


		/// <summary>
		/// Получение значение указанного ключа в параметрах запуска программы.
		/// </summary>
		/// <param name="args">Аргументы запуска программы</param>
		/// <param name="key">Проверяемый ключ, значение которого проверяется</param>
		/// <param name="defaultValue">Значение по-умолчанию, которое будет возвращено, если ключ не указан в параметрах</param>
		/// <returns>Возвращает значение указанного ключа или значение defaultValue, если указанный параметр не присутствует в параметрах запуска программы</returns>
		static string GetKey(string[] args, string key, string defaultValue) {
			for (int i = 0; i < args.Length; i++) {
				if (Regex.IsMatch(args[i], key) && (args.Length >= i+1)) return args[i + 1];
			}
			return defaultValue;
		}

		/// <summary>
		/// Проверка наличия ключа в параметрах запуска программы
		/// </summary>
		/// <param name="args">Аргументы запуска программы</param>
		/// <param name="key">Проверяемый ключ</param>
		/// <param name="defaultValue">Значение по-умолчанию. Необязательный параметры. По-умолчанию False.</param>
		/// <returns>Возвращает True, если указанный параметр присутствует в параметрах запуска программы</returns>
		static bool ChkKey(string[] args, string key, bool defaultValue=false) {
			foreach (string arg in args) {
				if (Regex.IsMatch(arg, key)) return true;
			}
			return defaultValue;
		}

		static string CheckKnownLinks(string link) {
			Match m = Regex.Match(link, @"youtube.*?\?v=([\w_-]+)");
			if (m.Success) {
				string videoID = m.Groups[1].Value;
				WebClient client = new WebClient();
				link = client.DownloadString("https://hms.lostcut.net/youtube/g.php?v=" + videoID + "&link_only=1");
			}
			return link;
		}
	}
}

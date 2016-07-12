using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Timers;

namespace IzendaService
{
	public class CustomWebClient : WebClient
	{
		protected override WebRequest GetWebRequest(Uri address)
		{
			WebRequest request = (WebRequest) base.GetWebRequest(address);
			request.PreAuthenticate = true;
			return request;
		}
	}

	public partial class IzendaService : ServiceBase
	{
		string rsPath = "";
		string timePeriod = "1";
		string tenants = "";
		string user = "";
		string pass = "";

		public IzendaService()
		{
			InitializeComponent();
		}

		private Timer timer;

		private void RunScheduledReports(object sender, ElapsedEventArgs elapsedEventArgs)
		{
			timer.Stop();
			string executeResult;
			try
			{
				string schedulingLogs = "";
				using (CustomWebClient client = new CustomWebClient())
				{
					if (!String.IsNullOrEmpty(user) && !String.IsNullOrEmpty(pass))
					{
						NetworkCredential credentials = new NetworkCredential(user, pass);
						client.UseDefaultCredentials = false;
						client.Credentials = credentials;
					}
					else
						client.UseDefaultCredentials = true;
					string url = String.Format("{0}?run_scheduled_reports={1}{2}", rsPath, timePeriod, String.IsNullOrEmpty(tenants) ? "" : ("&tenants=" + tenants));
					Stream networkStream = client.OpenRead(url);
					using (StreamReader reader = new StreamReader(networkStream))
						schedulingLogs = reader.ReadToEnd().Replace("<br>", Environment.NewLine).Replace("<br/>", Environment.NewLine);
					networkStream.Close();
				}
				executeResult = "Scheduling operation succeeded. Log which can be parsed: " + schedulingLogs;
			}
			catch (Exception e)
			{
				executeResult = "Scheduling operation failed: " + e.Message;
			}
			Log(executeResult);
			timer.Start();
		}

		protected override void OnStart(string[] args)
		{
			rsPath = (System.Configuration.ConfigurationSettings.AppSettings["responseServerPath"] ?? "").ToString();
			user = (System.Configuration.ConfigurationSettings.AppSettings["user"] ?? "").ToString();
			pass = (System.Configuration.ConfigurationSettings.AppSettings["password"] ?? "").ToString();
			tenants = (System.Configuration.ConfigurationSettings.AppSettings["tenants"] ?? "").ToString();
			timePeriod = (System.Configuration.ConfigurationSettings.AppSettings["timePeriod"] ?? "1").ToString();
			int interval = Convert.ToInt32((System.Configuration.ConfigurationSettings.AppSettings["interval"] ?? "-1").ToString());
			if (String.IsNullOrEmpty(rsPath))
			{
				Log("Response server URL is not specified. Attribute name is 'responseServerPath'");
				return;
			}
			if (interval <= 0)
			{
				Log("Time interval between scheduler runs is not specified. Attribute name is 'interval'");
				return;
			}
			timer = new Timer(interval);
			timer.Elapsed += RunScheduledReports;
			RunScheduledReports(null, null);
			Log("IzendaService started");
		}

		protected override void OnStop()
		{
			timer.Stop();
			timer.Close();
			Log("IzendaService stopped");
		}

		public void Log(string log)
		{
			try
			{
				if (!EventLog.SourceExists("IzendaService"))
					EventLog.CreateEventSource("IzendaService", "IzendaService");
				eventLog1.Source = "IzendaService";
				eventLog1.WriteEntry(log);
			}
			catch
			{
			}
		}
	}
}

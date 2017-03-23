using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;

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
		string izUser = "";
		string izPassword = "";
        List<string> tentantsList =new List<string>();


        public IzendaService()
		{
            IDbConnection _db = new SqlConnection(ConfigurationManager.ConnectionStrings["WasteStrategies"].ConnectionString);

            tentantsList = _db.Query<string>("select name from [ActiveTenants]").ToList();
            tenants = "_global_," + String.Join(",", tentantsList);

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
					if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
					{
						NetworkCredential credentials = new NetworkCredential(user, pass);
						client.UseDefaultCredentials = false;
						client.Credentials = credentials;
					}
					else
						client.UseDefaultCredentials = true;
					string url = string.Format("{0}?run_scheduled_reports={1}{2}{3}{4}", 
						rsPath, 
						timePeriod, 
						string.IsNullOrEmpty(tenants) ? "" : ("&tenants=" + tenants), 
						string.IsNullOrEmpty(izUser) ? "" : "&izUser=" + izUser, 
						string.IsNullOrEmpty(izPassword) ? "" : "&izPassword=" + izPassword);

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
		    

            rsPath = (ConfigurationManager.AppSettings["responseServerPath"] ?? "").ToString();
			user = (ConfigurationManager.AppSettings["user"] ?? "").ToString();
			pass = (ConfigurationManager.AppSettings["password"] ?? "").ToString();
			timePeriod = (ConfigurationManager.AppSettings["timePeriod"] ?? "1").ToString();
			izUser = (ConfigurationManager.AppSettings["izu"] ?? "").ToString();
			izPassword = (ConfigurationManager.AppSettings["izp"] ?? "").ToString();
            int interval = Convert.ToInt32((ConfigurationManager.AppSettings["interval"] ?? "-1").ToString());
			if (string.IsNullOrEmpty(rsPath))
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

using System;
using System.IO;
using System.Net.Mail;
using System.Text;
using System.Web;
using Izenda.AdHoc;
using System.Net;

public class CustomScheduler 
{
	public static void RunScheduledReports(TextWriter log, DateTime dateTime) 
	{
		string[] tenants = ListTenants();
		RunSchedulerForTenant(log, dateTime, null);
		foreach (string tenant in tenants)
			RunSchedulerForTenant(log, dateTime, tenant);
	}

	protected static void RunSchedulerForTenant(TextWriter log, DateTime dateTime, string tenantId) 
	{
		AdHocSettings.CurrentUserIsAdmin = true;
		AdHocSettings.CurrentUserTenantId = tenantId;
		if (System.Web.HttpContext.Current.Items.Contains("ListReportsWasCalledOnce"))
			System.Web.HttpContext.Current.Items.Remove("ListReportsWasCalledOnce");
		AdHocContext.SetSchedulerExecuting(true);
		try 
		{
			string delimiter = HttpContext.Current == null ? Environment.NewLine : "<br/>";
			bool reportsFound = false;

			// Send all emails using the same SmtpClient
			SmtpClient client = new SmtpClient(AdHocSettings.SmtpServer);

			ReportInfo[] reports = AdHocSettings.AdHocConfig.ListReports();
			foreach (ReportInfo reportInfo in reports) 
			{
				if (string.IsNullOrEmpty(reportInfo.Name) || reportInfo.TenantID.ToUpperInvariant() != tenantId.ToUpperInvariant())
					continue;
				ReportSet reportSet = null;
				try 
				{
					reportSet = AdHocSettings.AdHocConfig.LoadFilteredReportSet(reportInfo.FullName);
				} 
				catch { }
				if (reportSet == null)
					continue;

				DateTime schedule = reportSet.Schedule;
				DateTime scheduleUtc = reportSet.ScheduleUtc;
				string reportName = reportInfo.Name;
				if (!string.IsNullOrEmpty(reportInfo.Category))
					reportName = reportInfo.Category + AdHocSettings.CategoryCharacter + reportInfo.Name;
				reportsFound = true;
				if (scheduleUtc == Utility.NullDateTime || string.IsNullOrEmpty(reportSet.Recipients))
				{
					log.Write(string.Format("{1}{0} - No Schedule{1}", reportName, delimiter));
					continue;
				}
				if (schedule.Year == DateTime.MaxValue.Year)
				{
					log.Write(string.Format("{1}{0} - Scheduled For Past {1}", reportName, delimiter));
					continue;
				}
				if (scheduleUtc > dateTime.ToUniversalTime())
				{
					log.Write(string.Format("{1}{0} - Scheduled In Future({2} @ {3}){1}", reportName, delimiter, schedule.ToShortDateString(), schedule.ToShortTimeString()));
					continue;
				}
				try 
				{
					log.Write(string.Format("{1}{0} - Scheduled For This Interval ({2} @ {3}){1}", reportName, delimiter, schedule.ToShortDateString(), schedule.ToShortTimeString()));
					string[] recipients = reportSet.Recipients.Split(',', ';');
					MailMessage message = null;

					#region Preparing mail

					if (recipients.Length > 0) 
					{
						DateTime nextScheduleUtc = Utility.GetNextTime(scheduleUtc, reportSet.RepeatType).DateTime;
						DateTime nowUtc = DateTime.UtcNow;
						while (nextScheduleUtc < nowUtc)
							nextScheduleUtc = Utility.GetNextTime(nextScheduleUtc, reportSet.RepeatType).DateTime;
						reportSet.ScheduleUtc = nextScheduleUtc;
						AdHocSettings.AdHocConfig.SaveReportSet(reportInfo, reportSet);

						log.Write("Preparing mail - ");
						if (!Utility.CheckCondition(reportSet))
						{
							log.WriteLine("Condition is false" + delimiter);
							continue;
						}
						SchedulerOutput sh = AdHocSettings.SchedulerOutputTypes[reportSet.SendEmailAs];

						message = sh.GenerateMessage(reportSet, reportSet.IsDashBoard);

						message.Subject = string.Format(AdHocSettings.EmailSubjectFormatString, reportName);
						message.From = new MailAddress(AdHocSettings.EmailFromAddress);

						log.Write(string.Format("success{0}", delimiter));
					}

					#endregion

					log.Write(string.Format("Sending To:{0}", delimiter));
					foreach (string recipient in recipients)
						try 
						{
							log.Write(string.Format("{0} - ", recipient));
							Utility.SendEmailEx(client, recipient, message, AdHocSettings.SmtpSecureConnection, AdHocSettings.SmtpPort);
							log.Write(string.Format("success{0}", delimiter));
						}
						catch (Exception ex) 
						{
							StringBuilder emessage = new StringBuilder();
							while (ex != null) 
							{
								emessage.Append(ex.Message + " ");
								ex = ex.InnerException;
							}
							log.Write(string.Format("failure ({0}){1}", emessage, delimiter));
						}
					log.Write("Done.{0}", delimiter);
				}
				catch (Exception ex) 
				{
					StringBuilder emessage = new StringBuilder();
					while (ex != null) 
					{
						emessage.Append(ex.Message + " ");
						ex = ex.InnerException;
					}
					log.Write(string.Format("failure ({0}){1}", emessage, delimiter));
				}
			}
			if (!reportsFound)
				log.Write(string.Format("Reports Not Found {0}", delimiter));
		}
		catch { }
		finally
		{
			AdHocContext.SetSchedulerExecuting(false);
		}
	}

	protected static string[] ListTenants()
	{
		if (string.IsNullOrEmpty(HttpContext.Current.Request.Params["tenants"]))
			return new string[] { };
		return HttpContext.Current.Request.Params["tenants"].Split(',');
	}
}

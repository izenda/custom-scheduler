#region Copyright (c) 2005 Izenda, Inc.
/*
 ____________________________________________________________________
|                                                                   |
|   Izenda .NET Component Library                                   |
|                                                                   |
|   Copyright (c) 2005 Izenda, Inc.                                 |
|   ALL RIGHTS RESERVED                                             |
|                                                                   |
|   The entire contents of this file is protected by U.S. and       |
|   International Copyright Laws. Unauthorized reproduction,        |
|   reverse-engineering, and distribution of all or any portion of  |
|   the code contained in this file is strictly prohibited and may  |
|   result in severe civil and criminal penalties and will be       |
|   prosecuted to the maximum extent possible under the law.        |
|                                                                   |
|   RESTRICTIONS                                                    |
|                                                                   |
|   THIS SOURCE CODE AND ALL RESULTING INTERMEDIATE FILES           |
|   ARE CONFIDENTIAL AND PROPRIETARY TRADE                          |
|   SECRETS OF IZENDA INC. THE REGISTERED DEVELOPER IS              |
|   LICENSED TO DISTRIBUTE THE PRODUCT AND ALL ACCOMPANYING .NET    |
|   CONTROLS AS PART OF AN EXECUTABLE PROGRAM ONLY.                 |
|                                                                   |
|   THE SOURCE CODE CONTAINED WITHIN THIS FILE AND ALL RELATED      |
|   FILES OR ANY PORTION OF ITS CONTENTS SHALL AT NO TIME BE        |
|   COPIED, TRANSFERRED, SOLD, DISTRIBUTED, OR OTHERWISE MADE       |
|   AVAILABLE TO OTHER INDIVIDUALS WITHOUT EXPRESS WRITTEN CONSENT  |
|   AND PERMISSION FROM IZENDA INC.                                 |
|                                                                   |
|   CONSULT THE END USER LICENSE AGREEMENT(EULA FOR INFORMATION ON  |
|   ADDITIONAL RESTRICTIONS.                                        |
|                                                                   |
|___________________________________________________________________|
*/
#endregion Copyright (c) 2005 Izenda, Inc.
using System.IO;
using System;
using System.Net.Mail;
using System.Text;
using System.Web;

namespace Izenda.AdHoc
{
	/// <summary>
	/// Represents a set of methods that run scheduled reports.
	/// </summary>
	public class AdHocScheduler
	{
		/// <summary>
		/// Scans all reports currently saved to the storage device for reports and checks the schedule of each report, running the required action for reports that are set to be executed.
		/// </summary>
		/// <param name="log">A <see cref="System.IO.TextWriter"/> message logging stream to insert messages into concerning the scheduled reports.</param>
		/// <param name="dateTime">The <see cref="System.DateTime"/> that the schedule is running at.</param>
		/// <remarks>Usually the dateTime parameter is the current system date.</remarks>
		public static void RunScheduledReports(TextWriter log, DateTime dateTime)
		{
			string[] tenants = ListTenants();
			RunSchedulerForTenant(log, dateTime, null);
			foreach (string tenant in tenants)
				RunSchedulerForTenant(log, dateTime, tenant);
		}

		/// <summary>
		/// Returns array of tenants for which reports will be processed by the Scheduler
		/// </summary>
		/// <returns>Array of tenants for which reports will be processed by the Scheduler</returns>
		public static string[] ListTenants()
		{
			if (string.IsNullOrEmpty(HttpContext.Current.Request.Params["tenants"]))
				return new string[] { };
			return HttpContext.Current.Request.Params["tenants"].Split(',');
		}

		/// <summary>
		/// Scans reports for the given TenantID currently saved to the storage device for reports and checks the schedule of each report, running the required action for reports that are set to be executed.
		/// </summary>
		/// <param name="log">A <see cref="System.IO.TextWriter"/> message logging stream to insert messages into concerning the scheduled reports.</param>
		/// <param name="dateTime">The <see cref="System.DateTime"/> that the schedule is running at.</param>
		/// <param name="tenantId">AdHocSettings.CurrentUserTenantId value for which reports will be processed</param>
		public static void RunSchedulerForTenant(TextWriter log, DateTime dateTime, string tenantId) 
		{
			if (AdHocSettings.ResponseServer == "rs.aspx")
			{
				string url = HttpContext.Current.Request.Url.ToString();
				int index = url.LastIndexOfAny(new char[] { '/', '\\' });
				if (index > 0)
				{
					string path = url.Substring(0, index);
					if (!path.EndsWith("/") && !path.EndsWith("\\"))
						path += "/";
					AdHocSettings.ResponseServer = string.Format("{0}{1}", path, "rs.aspx");
				}
			}

			AdHocSettings.CurrentUserIsAdmin = true;
			AdHocSettings.CurrentUserTenantId = tenantId;
			if (System.Web.HttpContext.Current.Items.Contains("ListReportsWasCalledOnce"))
				System.Web.HttpContext.Current.Items.Remove("ListReportsWasCalledOnce");

			AdHocContext.SetSchedulerExecuting(true);
			try
			{
				string delimiter = HttpContext.Current == null ? Environment.NewLine : "<br/>";
				bool reportsFuond = false;

				// Send all emails using the same SmtpClient
				SmtpClient client = new SmtpClient(AdHocSettings.SmtpServer);

				ReportInfo[] reports = AdHocSettings.AdHocConfig.ListReports();
				foreach (ReportInfo reportInfo in reports)
				{
					if (string.IsNullOrEmpty(reportInfo.Name))
						continue;
					// Do not process _global_ reports if they had been processed already
					if (!string.IsNullOrEmpty(tenantId) && tenantId != "_global_" && reportInfo.TenantID == "_global_")
						continue;
					ReportSet reportSet = null;
					try
					{
						reportSet = AdHocSettings.AdHocConfig.LoadReportSetInternal(reportInfo);
					}
					catch
					{
					}
					if (reportSet == null)
						continue;

					DateTime schedule = reportSet.Schedule;
					DateTime scheduleUtc = reportSet.ScheduleUtc;
					string reportName = reportInfo.Name;
					if (!string.IsNullOrEmpty(reportInfo.Category))
						reportName = reportInfo.Category + AdHocSettings.CategoryCharacter + reportInfo.Name;
					reportsFuond = true;
					if (scheduleUtc == Utility.NullDateTime 
						|| string.IsNullOrEmpty(reportSet.Recipients) 
						|| reportSet.RepeatType == RepeatType.None)
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
						log.Write(
							string.Format("{1}{0} - Scheduled In Future({2} @ {3}){1}", reportName, delimiter, schedule.ToShortDateString(),
														schedule.ToShortTimeString()));
						continue;
					}
					try
					{
						log.Write(
							string.Format("{1}{0} - Scheduled For This Interval ({2} @ {3}){1}", reportName, delimiter,
														schedule.ToShortDateString(), schedule.ToShortTimeString()));
						string[] recipients = reportSet.Recipients.Split(',', ';');
						MailMessage message = null;

						#region Preparing mail

						if (recipients.Length > 0)
						{
							// if report has "Run once" repeat type - send it to recipients and set None repeat type.
							if (reportSet.RepeatType != RepeatType.RunOnce)
							{
								DateTime nextSchedule = Utility.GetNextTime(schedule, reportSet.RepeatType).DateTime;
								DateTime now = DateTime.Now;

								while (nextSchedule < now && nextSchedule.Date != Utility.NullDateTime)
									nextSchedule = Utility.GetNextTime(nextSchedule, reportSet.RepeatType).DateTime;

								reportSet.Schedule = nextSchedule;

							}
							else
								reportSet.RepeatType = RepeatType.None;

							AdHocSettings.AdHocConfig.SaveReportSetInternal(
								reportInfo,
								reportSet);

							log.Write("Preparing mail - ");
							if (!Utility.CheckCondition(reportSet))
							{
								log.WriteLine("Condition is false" + delimiter);
								continue;
							}
							SchedulerOutput sh = AdHocSettings.SchedulerOutputTypes[reportSet.SendEmailAs];
							if (sh == null)
							{
								log.WriteLine("Scheduler output type is unknown or undefined" + delimiter);
								continue;
							}
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
								int port = AdHocSettings.SmtpPort;
								if (HttpContext.Current.Request["port"] != null)
									int.TryParse(HttpContext.Current.Request["port"], out port);
								bool useSsl = AdHocSettings.SmtpSecureConnection || HttpContext.Current.Request["ssl"] == "1";
								Utility.SendEmailEx(client, recipient, message, useSsl, port);
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
				if (!reportsFuond)
				{
					log.Write(string.Format("Reports Not Found {0}", delimiter));
				}
			}
			finally
			{
				AdHocContext.SetSchedulerExecuting(false);
			}
		}
	}
}

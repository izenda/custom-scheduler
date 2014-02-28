using System;
using System.Collections.Generic;
using System.Text;
using Izenda.AdHoc;
using System.IO;
using System.Net.Mail;

namespace CustomScheduler {
  class CustomScheduler {
    static void Main(string[] args) {
      Izenda.AdHoc.AdHocSettings.LicenseKey = "Insert Key Here";
      Izenda.AdHoc.AdHocSettings.SqlServerConnectionString = @"Insert Connection String Here";
      Izenda.AdHoc.AdHocSettings.AdHocConfig = new CustomAdHocConfig();
      Izenda.AdHoc.AdHocSettings.ReportsPath = @"Path to reports folder";
      Izenda.AdHoc.AdHocSettings.ShowPoweredByLogo = false;
      AdHocSettings.SmtpServer = "Mail server";
      AdHocSettings.EmailFromAddress = "Someone@domain.com";
      AdHocSettings.ResponseServer = @"URl of rs.aspx";
      RunScheduledReports();
    }

    private static void RunScheduledReports() {
      string delimiter = Environment.NewLine;
      try {
        bool reportsFound = false;
        DateTime dateTime = DateTime.Now;

        ReportInfo[] reports = AdHocSettings.AdHocConfig.ListReports();
        foreach (ReportInfo reportInfo in reports) {
          if (reportInfo.FullName == null || reportInfo.FullName == "") {
            continue;
          }
          ReportSet reportSet = null;
          try {
            //reportSet = AdHocSettings.AdHocConfig.LoadFilteredReportSet(reportInfo.FullName);
            reportSet = AdHocSettings.AdHocConfig.LoadReportSet(reportInfo.FullName);
          } catch {
          }
          if (reportSet == null) {
            continue;
          }

          DateTime schedule = reportSet.Schedule;
          string reportName = reportInfo.Name;
          if (reportInfo.Category != null && reportInfo.Category != "") {
            reportName = reportInfo.Category + "\\" + reportInfo.Name;
          }
          reportSet.ReportName = reportName;
          reportsFound = true;

          if (schedule == Utility.NullDateTime || reportSet.Recipients == "" || reportSet.Recipients == null) {
            Console.WriteLine(string.Format("{1}{0} - No Schedule{1}", reportName, delimiter));
            continue;
          }
          if (schedule.Year == DateTime.MaxValue.Year) {
            Console.WriteLine(string.Format("{1}{0} - Scheduled For Past {1}", reportName, delimiter));
            continue;
          }
          if (schedule > dateTime) {
            Console.WriteLine(
              string.Format("{1}{0} - Scheduled In Future({2} @ {3}){1}", reportName, delimiter, schedule.ToShortDateString(),
                            schedule.ToShortTimeString()));
            continue;
          }
          try {
            Console.WriteLine(
              string.Format("{1}{0} - Scheduled For This Interval ({2} @ {3}){1}", reportName, delimiter,
                            schedule.ToShortDateString(), schedule.ToShortTimeString()));

            string[] recipients = reportSet.Recipients.Split(',', ';');


            if (recipients.Length > 0) {
              DateTime nextSchedule = Utility.GetNextTime(schedule, reportSet.RepeatType).DateTime;
              while (nextSchedule < DateTime.Now) {
                nextSchedule = Utility.GetNextTime(DateTime.Now, reportSet.RepeatType).DateTime;
                nextSchedule =
                  new DateTime(nextSchedule.Year, nextSchedule.Month, nextSchedule.Day,
                               schedule.Hour, schedule.Minute, 0);
              }
              reportSet.Schedule = nextSchedule;
              AdHocSettings.AdHocConfig.SaveReportSet(
                reportInfo,
                reportSet);
            }


            foreach (string recipient in recipients) {
              Console.WriteLine(string.Format("Sending To:{1}{0}", delimiter, recipient));
              ReportSet rs = reportSet;
              //!!Add Security to report set Here

              MailMessage message = null;
              SchedulerOutput sh = AdHocSettings.SchedulerOutputTypes[rs.SendEmailAs];
              message = sh.GenerateMessage(rs);
              message.Subject = string.Format(AdHocSettings.EmailSubjectFormatString, reportName);
              message.From = new MailAddress(AdHocSettings.EmailFromAddress);

              try {
                MailAddress addr = new MailAddress(recipient);
                message.To.Add(addr);
                SmtpClient client = new SmtpClient(AdHocSettings.SmtpServer);
                client.Send(message);
                Console.WriteLine(string.Format("success{0}", delimiter));
              } catch (Exception ex) {
                StringBuilder emessage = new StringBuilder();
                while (ex != null) {
                  emessage.Append(ex.Message + " ");
                }
                Console.WriteLine(emessage.ToString());
                //message failed
              }
            }
          } catch (Exception ex) {
            StringBuilder emessage = new StringBuilder();
            while (ex != null) {
              emessage.Append(ex.Message + " ");
            }
            //messagefailed
            Console.WriteLine(emessage.ToString());
          }
        }
        if (!reportsFound) {
          //No Reports Found
          Console.WriteLine("No ReportsFound");
        }
      } catch { }
      Console.ReadLine();
    }

  }
  public class CustomAdHocConfig : Izenda.AdHoc.FileSystemAdHocConfig {
    public override void ConfigureSettings() {
    }

    public override ReportSet LoadReportSet(string reportName) {
      ReportInfo reportInfo = new ReportInfo(reportName);
      string fileName = Path.Combine(ReportPath, reportInfo.FullName + ".xml");
      FileInfo file = new FileInfo(fileName);
      if (!file.Exists) {
        return null;
      }
      try {
        StreamReader reader = file.OpenText();
        string xml = reader.ReadToEnd();
        reader.Close();
        ReportSet reportSet = new ReportSet();
        reportSet.ReadXml(xml);
        reportSet.Global = reportInfo.TenantID == "_global_";
        reportSet.ReportName = reportInfo.Name;
        reportSet.ReportCategory = reportInfo.Category;
        return reportSet;
      } catch {
        return null;
      }
    }

    public override Izenda.AdHoc.ReportInfo[] ListReports() {
      return base.ListReports();
    }

    public override void PreExecuteReportSet(Izenda.AdHoc.ReportSet reportSet) {
    }
  }
}

using System;

public class RunScheduler : System.Web.UI.Page {
  protected void Page_Load(object sender, EventArgs e) {
    CustomScheduler.RunScheduledReports(Response.Output, DateTime.Now);
  }
}

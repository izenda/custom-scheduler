using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

public partial class Scheduler : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
		ASP.global_asax.CustomAdHocConfig.InitializeReporting();
		CustomScheduler.RunScheduledReports(Response.Output, DateTime.Now);
		Response.Flush();
		Response.End();
    }
}
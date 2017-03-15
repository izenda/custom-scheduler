# custom-scheduler
Summary: Izenda Custom Scheduler Implementation to query AuditPro database for active Organizations and Institutions in AuditPro

**Description:**
Implementation of a custom report scheduler that will retrieve a list of active orgs and institutions from a database view called ActiveTenants (already created by my awesome self). How this will work: 
1. The service will be customized to query the database view #ActiveTenants# every time it runs to ensure we are executing against the currently active clients and institutions. This will accomplish two things:

      a. It will eliminate the need to update the service configuration everytime a new Client or Institution is added to AuditPro
  
      b. It will ensure no reports run for inactive Clients or Institutions regardless of schedule settings
  
2. The list will be appended as a comma delimited list of all active Organizations and Institutions to the url submitted to the webserver reporting page to tell the page for which tenants we want to run reports. 
3. The service will also pass izUser and izPassword values to the reporting page (rs.aspx or rp.aspx) to provide for runtime security for the reporting service. The isUser and isPassword values will be configured in two places:

      a. In the **C:\Program Files (x86)\IzendaService\IzendaService.exe.config** using the App Keys provided.
      
      b. In the **Web.Config** using custom appSettings keys called ReportScvUser and ReportSvcPassword
      
When the rp.aspx page is called from the service, the format should look something like this:
```
<host url>/reporting/rs.aspx?run_scheduled_reports=<timePeriod>&izUser=<username>&izPassword=<password>&tenants=_global_,<list of returned tenant names>
```

Where:
```<hosturl>```,```<timePeriod>```, ```<username>``` and ```<password>```are defined in the service config located on the server at **C:\Program Files (x86)\IzendaService\IzendaService.exe.config**

The logical flow diagram for these changes is located at:

These changes will also require the cusotmization of the Global.aspx page to authenticate as explained in the Izenda Wiki at http://wiki.izenda.us/FAQ/Implementing-Scheduler-Security. The example C# code provided is below:
```
public class CustomAdHocConfig : FileSystemAdHocConfig
{
    public static void InitializeReporting()
    {
        if (AdHocContext.Initialized)
            return;
        AdHocSettings.LicenseKey = "SET_LICENSE_KEY_HERE";
        string izUserName = string.Empty;
        string izPassword = string.Empty;
        if (!string.IsNullOrEmpty(HttpContext.Current.Request["izUser"]))
            izUserName = HttpContext.Current.Request["izUser"];
        if (!string.IsNullOrEmpty(HttpContext.Current.Request["izPassword"]))
            izPassword = HttpContext.Current.Request["izPassword"];
        if (!string.IsNullOrEmpty(izUserName) && !string.IsNullOrEmpty(izPassword))
        {
            if (AuthenticateSchedulerUser(izUserName, izPassword)) //AuthenticateSchedulerUser is a method that will need to be created specially to handle this case and returns a boolean value.
                AdHocSettings.CurrentUserName = izUserName;
        }
        AdHocSettings.RequireLogin = true;
        AdHocSettings.AdHocConfig = new CustomAdHocConfig(); //setting this after RequireLogin will force a redirect if AdHocSettings.CurrentUserName has not been set. This assumes that different login logic handles a standard user login.
        //continue to initialize normally
        AdHocContext.Initialized = true;
    }
}
```

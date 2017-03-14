# custom-scheduler
Izenda Custom Scheduler Implementation to query db for active Org and Institutions in AuditPro

Implementation of a custom report scheduler that will grab a list of active orgs and institutions from a database view called ActiveTenants (already created by my awesome self). How this will work: 
1. The service will be customized to query the database every time it runs to ensure we are executing against the currently active clients and institutions.
2. The list will be appended as a comma delimited list to the url submitted to the webserver reporting page to tell the page for which tenants we want to run reports. The format should look something like this:
```
<host url>/reporting/rs.aspx?run_scheduled_reports=<timePeriod>&tenants=_global_,<list of returned tenant names>
```

Where:
<hosturl> and <timePeriod> are defined in the service config located on the server at **C:\Program Files (x86)\IzendaService\IzendaService.exe.config**


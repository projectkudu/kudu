<%@ Page Language="C#" %>
<%@ Import Namespace="System.IO" %>
<%@ Import Namespace="Kudu.Services.Web" %>
<%@ Import Namespace="Kudu.Services" %>

<% 
    string appPath = MapPath("_app");
    string filePath = Path.Combine(appPath, "test");

    try
    {
        File.WriteAllText(filePath, "Test file");

        Response.Write("Success");

        File.Delete(filePath);
    }
    catch(Exception ex)
    {
        Response.Write("Failed to write test file. Check permissions");
    }
%>

namespace RpCCTransferJsonToDb.Configuration;

public class AppSettings
{
    public ConnectionStringsSettings ConnectionStrings { get; set; } = new();
}

public class ConnectionStringsSettings
{
    /// <summary>
    /// SQL Server connection string for the Call_Center database.
    /// Default targets localhost with integrated (Windows) auth.
    /// </summary>
    public string CallCenter { get; set; } = "";
}

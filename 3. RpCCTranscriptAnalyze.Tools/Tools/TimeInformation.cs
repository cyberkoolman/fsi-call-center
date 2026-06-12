using System.ComponentModel;

namespace RpCCTranscriptAnalyze.Tools.Tools;

/// <summary>
/// Native function exposed to the Microsoft Agent Framework agent as an AITool.
/// The agent's auto function-calling will invoke <see cref="GetCurrentUtcDate"/>
/// when the prompt asks for "today's date" (e.g. for the call_date field).
/// </summary>
public class TimeInformation
{
    [Description("Returns today's date in UTC (yyyy-MM-dd).")]
    public string GetCurrentUtcDate() => DateTime.UtcNow.ToString("yyyy-MM-dd");
}

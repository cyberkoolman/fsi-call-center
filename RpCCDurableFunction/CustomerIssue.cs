using System;
using System.Text.Json.Serialization;

public class CustomerIssue
{
    [JsonPropertyName("classified_reason")]
    public string ClassifiedReason { get; set; }

    [JsonPropertyName("resolve_status")]
    public string ResolveStatus { get; set; }

    [JsonPropertyName("call_summary")]
    public string CallSummary { get; set; }

    [JsonPropertyName("customer_name")]
    public string CustomerName { get; set; }

    [JsonPropertyName("employee_name")]
    public string EmployeeName { get; set; }

    [JsonPropertyName("order_number")]
    public string OrderNumber { get; set; }

    [JsonPropertyName("customer_contact_nr")]
    public string CustomerContactNr { get; set; }

    [JsonPropertyName("new_address")]
    public string NewAddress { get; set; }

    [JsonPropertyName("sentiment_initial")]
    public string[] SentimentInitial { get; set; }

    [JsonPropertyName("sentiment_final")]
    public string[] SentimentFinal { get; set; }

    [JsonPropertyName("satisfaction_score_initial")]
    public int SatisfactionScoreInitial { get; set; }

    [JsonPropertyName("satisfaction_score_final")]
    public int SatisfactionScoreFinal { get; set; }

    [JsonPropertyName("eta")]
    public string Eta { get; set; }

    [JsonPropertyName("action_item")]
    public string[] ActionItem { get; set; }

    [JsonPropertyName("call_date")]
    public DateTime CallDate { get; set; }    
}
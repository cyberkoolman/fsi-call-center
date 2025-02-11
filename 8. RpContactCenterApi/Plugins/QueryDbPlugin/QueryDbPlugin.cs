using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Data.SqlClient;

public class QueryDbPlugin
{
    private readonly SqlConnection _connection;
    private readonly IConfiguration _configuration;

    public QueryDbPlugin()
    {
        _configuration = new ConfigurationBuilder()
                            .AddJsonFile("appsettings.json")
                            .Build();

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = _configuration["Database:Server"],
            UserID = _configuration["Database:UserId"],
            Password = _configuration["Database:Password"],
            InitialCatalog = _configuration["Database:InitialCatalog"]
        };

        _connection = new SqlConnection(builder.ConnectionString);
        try
        {
            _connection.Open();
        }
        catch (Exception)
        {
            throw;
        }
    }

    [KernelFunction("QueryDb")]
    [Description("Query a database using a SQL query")]
    public async Task<string> QueryDb([Description("Search query to be executed")] string input)
    {
        Console.WriteLine($"Querying: {input}");
        var response = "";
        var results = new List<Dictionary<string, object>>();
        try 
        {        
            using (SqlCommand command = new SqlCommand(input, _connection))
            {
                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    // Get the column names from reader.GetSchemaTable()
                    List<string> columns = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        columns.Add(reader.GetName(i));
                    }

                    // Fetch all rows and create dictionaries
                    while (reader.Read())
                    {
                        Dictionary<string, object> row = new Dictionary<string, object>();
                        foreach (string column in columns)
                        {
                            row[column] = reader[column];
                        }
                        results.Add(row);
                    }
                }
                response = string.Join("\n", results.Select(
                                                row => string.Join(", ", 
                                                    row.Select(kvp => $"{kvp.Key}: {kvp.Value}"))));
            }                    
        }
        catch (SqlException e)
        {
            response = e.ToString();
        }
        return response;
    }
}

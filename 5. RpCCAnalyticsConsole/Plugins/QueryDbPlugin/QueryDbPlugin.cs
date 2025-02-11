using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Data.SqlClient;

public class QueryDbPlugin
{
    [KernelFunction("QueryDb")]
    [Description("Query a database using a SQL query")]
    public async Task<string> QueryDb([Description("Search query to be executed")] string input)
    {
        Console.WriteLine("\nQuerying the database: {0}", input);

        var configuration = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        .Build();

        var response = "";
        var results = new List<Dictionary<string, object>>();
        try 
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            builder.DataSource = configuration["Database:Server"];
            builder.UserID = configuration["Database:UserId"];
            builder.Password = configuration["Database:Password"];
            builder.InitialCatalog = configuration["Database:InitialCatalog"];
        
            using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
            {
                connection.Open();       

                // String sql = "SELECT name, collation_name FROM sys.databases";
                string sql = input;

                using (SqlCommand command = new SqlCommand(sql, connection))
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
        }
        catch (SqlException e)
        {
            response = e.ToString();
        }
        return response;
    }
}

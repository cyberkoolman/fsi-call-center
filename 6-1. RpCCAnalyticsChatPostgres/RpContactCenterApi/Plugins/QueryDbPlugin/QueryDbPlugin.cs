using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Npgsql;

public class QueryDbPlugin
{
    [KernelFunction("QueryDb")]
    [Description("Query a database using a SQL query")]
    public async Task<string> QueryDb([Description("Search query to be executed")] string input)
    {
        Console.WriteLine("\nQuerying the database: {0}", input);

        var configuration = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        .Build();        var response = "";
        var results = new List<Dictionary<string, object>>();
        try 
        {
            var connectionString = $"Host={configuration["Database:Host"]};" +
                                 $"Port={configuration["Database:Port"]};" +
                                 $"Database={configuration["Database:Database"]};" +
                                 $"Username={configuration["Database:Username"]};" +
                                 $"Password={configuration["Database:Password"]}";
        
            using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();       

                // String sql = "SELECT datname, datcollate FROM pg_database";
                string sql = input;

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // Get the column names from reader.GetSchemaTable()
                        List<string> columns = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columns.Add(reader.GetName(i));
                        }

                        // Fetch all rows and create dictionaries
                        while (await reader.ReadAsync())
                        {
                            Dictionary<string, object> row = new Dictionary<string, object>();
                            foreach (string column in columns)
                            {
                                var value = reader[column];
                                row[column] = value == DBNull.Value ? null : value;
                            }
                            results.Add(row);
                        }
                    }
                    response = string.Join("\n", results.Select(
                                                    row => string.Join(", ", 
                                                        row.Select(kvp => $"{kvp.Key}: {kvp.Value ?? "NULL"}"))));
                }                    
            }            
        }
        catch (NpgsqlException e)
        {
            response = e.ToString();
        }
        return response;
    }
}

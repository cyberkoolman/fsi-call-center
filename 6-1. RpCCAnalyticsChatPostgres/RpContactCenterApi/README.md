# RP Contact Center API - OpenAI Compatible

This API provides an OpenAI-compatible interface for the RP Contact Center's RAG (Retrieval-Augmented Generation) system. It can be used with OpenWebUI and other OpenAI-compatible clients.

## Features

- **OpenAI Chat Completions API**: `/v1/chat/completions`
- **OpenAI Models API**: `/v1/models`
- **Streaming Support**: Real-time response streaming
- **RAG Integration**: Uses Semantic Kernel with Azure OpenAI and PostgreSQL
- **Natural Language to SQL**: Converts natural language queries to SQL and executes them
- **CORS Enabled**: Ready for web-based clients like OpenWebUI

## API Endpoints

### Chat Completions
`POST /v1/chat/completions`

Compatible with OpenAI's chat completions API. Supports:
- Multiple message roles (system, user, assistant)
- Temperature control
- Max tokens limit
- Streaming responses
- Function calling (for database queries)

**Example Request:**
```json
{
  "model": "gpt-4",
  "messages": [
    {
      "role": "user",
      "content": "Show me the top 5 customers by total orders"
    }
  ],
  "temperature": 0.7,
  "stream": false
}
```

### Models
`GET /v1/models`

Returns available models in OpenAI format.

### Health Check
`GET /health`

Returns API health status.

## Configuration

Update `appsettings.json` with your Azure OpenAI and PostgreSQL settings:

```json
{
  "AzureOpenAI": {
    "ApiKey": "your-azure-openai-key",
    "DeploymentChatName": "gpt-4",
    "DeploymentEmbeddingName": "text-embedding-3-small",
    "Endpoint": "https://your-instance.cognitiveservices.azure.com/"
  },
  "Database": {
    "Host": "localhost",
    "Port": "5432",
    "Database": "postgres",
    "Username": "postgres",
    "Password": "postgres"
  }
}
```

## Using with OpenWebUI

1. **Start the API**: Run the application (default port: 5000 or 7000)

2. **Configure OpenWebUI**: 
   - Base URL: `http://localhost:5000/v1` (adjust port as needed)
   - API Key: Not required, but you can set any value
   - Model: Select "gpt-4" or "gpt-3.5-turbo"

3. **Start Chatting**: Ask questions about your database in natural language

## Example Queries

The system can handle various database-related queries:

- **"Show me all customers from California"**
- **"What are the top 10 products by sales?"**
- **"How many orders were placed last month?"**
- **"Which customers have spent more than $1000?"**

## RAG Capabilities

The API includes:

1. **NLP to SQL Plugin**: Converts natural language to SQL queries
2. **Database Query Plugin**: Executes SQL queries against PostgreSQL
3. **Response Generation**: Formats results in natural language

## Running the Application

```bash
# Restore dependencies
dotnet restore

# Run the application
dotnet run

# Or in development mode
dotnet run --environment Development
```

The API will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

## API Documentation

When running in development mode, Swagger documentation is available at:
- `http://localhost:5000/swagger`

## Security Considerations

- **Database Security**: Use parameterized queries (implemented in QueryDbPlugin)
- **CORS**: Currently allows all origins for development - restrict in production
- **Authentication**: Consider adding API key authentication for production use
- **Rate Limiting**: Consider implementing rate limiting for production

## Integration with Other Clients

This API is compatible with any OpenAI-compatible client, including:

- **OpenWebUI**: Web-based chat interface
- **Continue**: VS Code extension
- **Chatbot UI**: React-based chat interface
- **Custom Applications**: Using OpenAI SDKs

## Troubleshooting

### Common Issues:

1. **CORS Errors**: Ensure the API is running and CORS is properly configured
2. **Database Connection**: Verify PostgreSQL connection string in appsettings.json
3. **Azure OpenAI**: Check API key and endpoint configuration
4. **Port Conflicts**: Change port in launchSettings.json if needed

### Logs:

The application logs to console with detailed information about:
- Incoming requests
- Database queries
- Azure OpenAI interactions
- Errors and exceptions

## Development

To extend the API:

1. **Add New Plugins**: Create new Semantic Kernel plugins in the `/Plugins` folder
2. **Modify Models**: Update models in `/Models/OpenAIModels.cs`
3. **Add Endpoints**: Extend the OpenAI-compatible controller
4. **Database Schema**: Modify the SQL generation logic for your specific schema

## License

This project is part of the RP Contact Center system.

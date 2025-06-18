# OpenWebUI Configuration Guide

## Quick Setup for OpenWebUI

### 1. Ensure Your API is Running
```bash
cd "c:\FSI\call-center\6-1. RpCCAnalyticsChatPostgres\RpContactCenterApi"
dotnet run
```

The API will be available at: `http://localhost:5108`

### 2. Configure OpenWebUI

**Step 1: Access OpenWebUI Settings**
- Open OpenWebUI in your browser
- Go to Settings (gear icon) → Connections

**Step 2: Add New OpenAI Connection**
- Click "Add Connection" or "+"
- Fill in the following details:

```
Name: RP Contact Center RAG
Base URL: http://localhost:5108/v1
API Key: dummy-key (any value works)
```

**Step 3: Test Connection**
- Click "Test Connection" - it should show success
- Save the configuration

**Step 4: Select Model**
- In the chat interface, select "gpt-4" or "gpt-3.5-turbo" from the model dropdown
- Both models point to the same backend but allow you to test different configurations

### 3. Example Queries to Try

**Database Schema Exploration:**
- "What tables are in the database?"
- "Describe the customer_issues table"
- "Show me the column names in customer_issues"

**Data Analysis Queries:**
- "How many customer issues do we have?"
- "What are the most common issue types?"
- "Show me unresolved issues"
- "Which customers have the most issues?"
- "What's the average satisfaction score?"

**Complex Analytics:**
- "Show me issues by month"
- "Which employees handle the most cases?"
- "What's the resolution rate by issue type?"
- "Find customers with low satisfaction scores"

### 4. Advanced Features

**Streaming Chat:**
- Enable streaming in OpenWebUI settings for real-time responses
- The API supports both streaming and non-streaming modes

**Multi-turn Conversations:**
- The API maintains conversation context
- You can ask follow-up questions about previous queries

**Function Calling:**
- The backend automatically determines when to query the database
- Natural language is converted to SQL using Semantic Kernel plugins

### 5. API Endpoints Available

- **Chat Completions:** `/v1/chat/completions`
- **Models List:** `/v1/models`
- **Health Check:** `/health`
- **API Info:** `/` (root endpoint)
- **Swagger UI:** `/swagger` (when running in development)

### 6. Troubleshooting

**Connection Issues:**
- Ensure the API is running on port 5108
- Check that CORS is enabled (already configured)
- Verify the Base URL in OpenWebUI settings

**Database Connection:**
- Check PostgreSQL is running
- Verify connection string in `appsettings.json`
- Ensure the `customer_issues` table exists

**Azure OpenAI Issues:**
- Verify API key and endpoint in configuration
- Check Azure OpenAI deployment is active
- Confirm model deployment name matches configuration

### 7. Security Notes

**For Production Use:**
- Add authentication to the API
- Restrict CORS to specific origins
- Use environment variables for sensitive configuration
- Implement rate limiting
- Add input validation and sanitization

**Current Development Setup:**
- No authentication required
- CORS allows all origins
- Swagger UI enabled
- Detailed error logging enabled

### 8. Performance Tips

- Use streaming for better user experience
- The API includes connection pooling for database queries
- Responses include token usage estimates
- Background functions handle SQL generation and execution

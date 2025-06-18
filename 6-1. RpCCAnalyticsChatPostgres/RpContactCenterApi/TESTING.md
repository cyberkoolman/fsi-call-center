# Test OpenAI Compatibility

## Using curl to test the API

### 1. Test Health Endpoint
```bash
curl -X GET http://localhost:5000/health
```

### 2. Test Models Endpoint
```bash
curl -X GET http://localhost:5000/v1/models
```

### 3. Test Chat Completions (Non-streaming)
```bash
curl -X POST http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "gpt-4",
    "messages": [
      {
        "role": "user",
        "content": "Show me all database tables"
      }
    ],
    "temperature": 0.7
  }'
```

### 4. Test Chat Completions (Streaming)
```bash
curl -X POST http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "gpt-4",
    "messages": [
      {
        "role": "user",
        "content": "What data do we have in our database?"
      }
    ],
    "stream": true
  }'
```

## Python Test Script

```python
import requests
import json

# Test the API
base_url = "http://localhost:5000"

# Test health
response = requests.get(f"{base_url}/health")
print("Health:", response.json())

# Test models
response = requests.get(f"{base_url}/v1/models")
print("Models:", response.json())

# Test chat completion
payload = {
    "model": "gpt-4",
    "messages": [
        {"role": "user", "content": "Hello, can you help me query the database?"}
    ],
    "temperature": 0.7
}

response = requests.post(f"{base_url}/v1/chat/completions", json=payload)
print("Chat:", response.json())
```

## OpenWebUI Configuration

1. Open OpenWebUI
2. Go to Settings → Connections
3. Add new connection:
   - **Name**: RP Contact Center
   - **Base URL**: `http://localhost:5000/v1`
   - **API Key**: `any-key` (not validated)
4. Save and select the connection
5. Start chatting with your database!

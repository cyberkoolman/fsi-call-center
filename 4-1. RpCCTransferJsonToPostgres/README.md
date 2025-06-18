# Call Center Data Transfer - PostgreSQL Migration

This project has been refactored to use PostgreSQL instead of Azure SQL Server.

## Prerequisites

- .NET 8.0 SDK
- Docker and Docker Compose

## Setup Instructions

1. **Start PostgreSQL Container**
   ```bash
   docker-compose up -d
   ```
   This will start:
   - PostgreSQL database on port 5432
   - Adminer (database management tool) on port 8080

2. **Create Database Schema**
   
   The database schema needs to be created manually. You can use one of these methods:

   **Method 1: Using the setup script (Windows)**
   ```bash
   ./setup-db.bat
   ```

   **Method 2: Using Docker exec directly**
   ```bash
   docker exec postgres psql -U postgres -d postgres -c "CREATE TABLE IF NOT EXISTS customer_issues (id SERIAL PRIMARY KEY, classified_reason VARCHAR(500), resolve_status VARCHAR(100), call_summary TEXT, customer_name VARCHAR(200), employee_name VARCHAR(200), order_number VARCHAR(100), customer_contact_nr VARCHAR(50), new_address TEXT, sentiment_initial TEXT, sentiment_final TEXT, satisfaction_score_initial INTEGER, satisfaction_score_final INTEGER, eta VARCHAR(100), action_item TEXT, call_date TIMESTAMP, created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP);"
   ```

   **Method 3: Using Adminer Web Interface**
   - Open http://localhost:8080
   - Login with PostgreSQL credentials (see below)
   - Execute the SQL from `schema.sql`

3. **Install Dependencies**
   ```bash
   dotnet restore
   ```

4. **Build the Project**
   ```bash
   dotnet build
   ```

5. **Run the Application**
   ```bash
   dotnet run
   ```

   Or to generate sample data:
   ```bash
   dotnet run generate <number_of_records>
   ```

## Database Access

- **PostgreSQL Connection:**
  - Host: localhost
  - Port: 5432
  - Database: postgres
  - Username: postgres
  - Password: postgres

- **Adminer Web Interface:**
  - URL: http://localhost:8080
  - Use the PostgreSQL connection details above

## Database Schema

The `customer_issues` table is automatically created when the PostgreSQL container starts for the first time. The schema includes:

- All original fields from the SQL Server version
- Proper PostgreSQL data types
- Indexes for better performance
- Auto-incrementing primary key

## Configuration

Database connection settings are in `appsettings.json`:

```json
{
    "Database": {
        "Host": "localhost",
        "Port": "5432",
        "Database": "postgres",
        "Username": "postgres",
        "Password": "postgres"
    }
}
```

## Changes Made

1. Replaced `Microsoft.Data.SqlClient` with `Npgsql` PostgreSQL driver
2. Updated connection string configuration for PostgreSQL
3. Modified SQL queries to use PostgreSQL syntax (lowercase table/column names)
4. Created PostgreSQL schema with appropriate data types
5. Updated Docker Compose to initialize the database schema automatically

## Stopping the Services

To stop the PostgreSQL container:
```bash
docker-compose down
```

To remove all data (including database):
```bash
docker-compose down -v
```

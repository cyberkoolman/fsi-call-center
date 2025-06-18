#!/bin/bash

# Script to create database schema in PostgreSQL
echo "Creating customer_issues table..."

docker exec postgres psql -U postgres -d postgres -c "
CREATE TABLE IF NOT EXISTS customer_issues (
    id SERIAL PRIMARY KEY,
    classified_reason VARCHAR(500),
    resolve_status VARCHAR(100),
    call_summary TEXT,
    customer_name VARCHAR(200),
    employee_name VARCHAR(200),
    order_number VARCHAR(100),
    customer_contact_nr VARCHAR(50),
    new_address TEXT,
    sentiment_initial TEXT,
    sentiment_final TEXT,
    satisfaction_score_initial INTEGER,
    satisfaction_score_final INTEGER,
    eta VARCHAR(100),
    action_item TEXT,
    call_date TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);"

echo "Creating indexes..."

docker exec postgres psql -U postgres -d postgres -c "
CREATE INDEX IF NOT EXISTS idx_customer_issues_customer_name ON customer_issues(customer_name);
CREATE INDEX IF NOT EXISTS idx_customer_issues_call_date ON customer_issues(call_date);
CREATE INDEX IF NOT EXISTS idx_customer_issues_resolve_status ON customer_issues(resolve_status);
"

echo "Database schema created successfully!"

# Display table structure
echo "Table structure:"
docker exec postgres psql -U postgres -d postgres -c "\d customer_issues"

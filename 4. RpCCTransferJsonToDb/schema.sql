-- Schema for the Call_Center database used by RpCCTransferJsonToDb.
-- Run once against your local SQL Server, e.g.:
--   sqlcmd -S localhost -E -Q "IF DB_ID('Call_Center') IS NULL CREATE DATABASE Call_Center"
--   sqlcmd -S localhost -d Call_Center -E -i schema.sql
--
-- The application also calls EnsureSchemaAsync() at startup, so this script
-- is mostly here as documentation of the table layout.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomerIssues')
BEGIN
    CREATE TABLE CustomerIssues (
        Id                        INT IDENTITY(1,1) PRIMARY KEY,
        ClassifiedReason          NVARCHAR(64)  NOT NULL,
        ResolveStatus             NVARCHAR(32)  NOT NULL,
        CallSummary               NVARCHAR(500) NULL,
        CustomerName              NVARCHAR(128) NULL,
        EmployeeName              NVARCHAR(128) NULL,
        OrderNumber               NVARCHAR(32)  NULL,
        CustomerContactNr         NVARCHAR(32)  NULL,
        NewAddress                NVARCHAR(256) NULL,
        SentimentInitial          NVARCHAR(128) NULL,
        SentimentFinal            NVARCHAR(128) NULL,
        SatisfactionScoreInitial  INT           NOT NULL,
        SatisfactionScoreFinal    INT           NOT NULL,
        Eta                       NVARCHAR(32)  NULL,
        ActionItem                NVARCHAR(256) NULL,
        CallDate                  DATE          NOT NULL,
        InsertedAt                DATETIME2     NOT NULL CONSTRAINT DF_CustomerIssues_InsertedAt DEFAULT SYSUTCDATETIME()
    );
END

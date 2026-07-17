namespace RpCCKbRAG.Models;

/// <summary>Initial trigger sent into the ContosoKB workflow.</summary>
public record UserQuery(string Query);

/// <summary>Carries the retrieved chunks from VectorSearch to Answer.</summary>
public record RetrievedContext(string Query, List<RagDocument> Documents);

/// <summary>Final workflow output with answer text and token usage.</summary>
public record AnswerResult(string Text, int InputTokens, int OutputTokens, int TotalTokens);

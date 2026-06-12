namespace RpCCKbRAG.Models;

/// <summary>Initial trigger sent into the ContosoKB workflow.</summary>
public record UserQuery(string Query);

/// <summary>Carries the retrieved chunks from VectorSearch to Answer.</summary>
public record RetrievedContext(string Query, List<RagDocument> Documents);

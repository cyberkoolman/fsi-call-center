using RpContactCenterApi.Configuration;
using RpContactCenterApi.Models;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace RpContactCenterApi.Services;

/// <summary>
/// Loads PDF documents, splits them into overlapping chunks with section
/// metadata (page number), and generates embeddings ready for the VectorStore.
///
/// The Agentic.RAG reference loader handles HTML / plain text from URLs;
/// this build is targeted at the Contoso PDF corpus shipped under Documents/.
/// </summary>
public class DocumentLoader
{
    private readonly PipelineSettings _pipeline;
    private const int CharsPerToken = 4;

    public DocumentLoader(AppSettings settings)
    {
        _pipeline = settings.Pipeline;
    }

    /// <summary>
    /// Loads, chunks, and embeds every configured document source.
    /// </summary>
    public async Task<List<RagDocument>> LoadAllAsync(
        IEnumerable<DocumentSource> sources,
        AzureAIService aiService,
        CancellationToken cancellationToken = default)
    {
        var all = new List<RagDocument>();

        foreach (var source in sources)
        {
            Console.WriteLine($"  Loading: {source.Name}");
            var docs = await LoadSourceAsync(source, aiService, cancellationToken);
            all.AddRange(docs);
            Console.WriteLine($"  → {docs.Count} chunks from '{source.Name}'");
        }

        return all;
    }

    // ─────────────────────────────────────────────────────────────
    // Per-source loading
    // ─────────────────────────────────────────────────────────────

    private async Task<List<RagDocument>> LoadSourceAsync(
        DocumentSource source,
        AzureAIService aiService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.FilePath) || !File.Exists(source.FilePath))
            throw new FileNotFoundException(
                $"DocumentSource '{source.Name}' references missing file: {source.FilePath}");

        var chunks = ChunkPdf(source.FilePath, source.Name);
        Console.WriteLine($"    Created {chunks.Count} chunks, generating embeddings...");
        return await EmbedChunksAsync(chunks, source, aiService, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────
    // PDF chunking — page-aware, overlapping
    // ─────────────────────────────────────────────────────────────

    private List<(string Content, string Section)> ChunkPdf(string path, string sourceName)
    {
        var chunks       = new List<(string, string)>();
        int chunkChars   = _pipeline.ChunkSize    * CharsPerToken;
        int overlapChars = _pipeline.ChunkOverlap * CharsPerToken;

        using var pdf = PdfDocument.Open(path);

        var perPage = new List<(int PageNumber, string Text)>();
        foreach (Page page in pdf.GetPages())
        {
            var text = NormalizeWhitespace(page.Text);
            if (!string.IsNullOrWhiteSpace(text))
                perPage.Add((page.Number, text));
        }

        // Concatenate pages with markers so we can attribute chunks back to a page range.
        var sb = new StringBuilder();
        foreach (var (pageNo, text) in perPage)
        {
            sb.Append("\u0001PAGE:").Append(pageNo).Append('\u0001').Append(' ').Append(text).Append(' ');
        }

        var combined = sb.ToString();
        int start    = 0;

        while (start < combined.Length)
        {
            int end   = Math.Min(start + chunkChars, combined.Length);
            var slice = combined[start..end];

            // Find the last page marker at or before the slice start so we
            // can label the chunk with the appropriate page number.
            int pageNumber = FindPageForOffset(combined, start);
            var content    = StripPageMarkers(slice).Trim();

            if (content.Length > 80)
                chunks.Add((content, $"{sourceName} — page {pageNumber}"));

            start += chunkChars - overlapChars;
        }

        return chunks;
    }

    private static int FindPageForOffset(string combined, int offset)
    {
        // Scan backwards for the most recent \u0001PAGE:n\u0001 marker.
        int marker = combined.LastIndexOf("\u0001PAGE:", offset, StringComparison.Ordinal);
        if (marker < 0) return 1;
        int colonEnd = combined.IndexOf('\u0001', marker + 1);
        if (colonEnd < 0) return 1;
        var num = combined[(marker + 6)..colonEnd];
        return int.TryParse(num, out var n) ? n : 1;
    }

    private static string StripPageMarkers(string text)
    {
        var sb = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\u0001')
            {
                int next = text.IndexOf('\u0001', i + 1);
                if (next < 0) break;
                i = next + 1;
                continue;
            }
            sb.Append(text[i++]);
        }
        return sb.ToString();
    }

    private static string NormalizeWhitespace(string text)
    {
        var sb   = new StringBuilder(text.Length);
        bool sp  = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!sp) { sb.Append(' '); sp = true; }
            }
            else
            {
                sb.Append(ch);
                sp = false;
            }
        }
        return sb.ToString().Trim();
    }

    // ─────────────────────────────────────────────────────────────
    // Embed in batches
    // ─────────────────────────────────────────────────────────────

    private async Task<List<RagDocument>> EmbedChunksAsync(
        List<(string Content, string Section)> chunks,
        DocumentSource source,
        AzureAIService aiService,
        CancellationToken cancellationToken)
    {
        const int BatchSize = 16;
        var documents = new List<RagDocument>(chunks.Count);

        for (int i = 0; i < chunks.Count; i += BatchSize)
        {
            var batch  = chunks.Skip(i).Take(BatchSize).ToList();
            var embeds = await aiService.GenerateEmbeddingsAsync(
                batch.Select(c => c.Content), cancellationToken);

            for (int j = 0; j < batch.Count; j++)
            {
                documents.Add(new RagDocument
                {
                    Content   = batch[j].Content,
                    Section   = batch[j].Section,
                    Source    = source.Name,
                    Url       = source.FilePath,
                    Embedding = embeds[j]
                });
            }

            int done = Math.Min(i + BatchSize, chunks.Count);
            Console.WriteLine($"    Embedded {done}/{chunks.Count}...");
        }

        return documents;
    }
}

using Onyx.Domain.Common;

namespace Onyx.Domain.Products;

/// <summary>
/// One AI demo request by a verified customer. Queued by the API after the abuse and budget checks, processed by the Worker
/// (never inside a web request), and kept as the usage record. The prompt is customer text: treated as untrusted data and as
/// personal data. The output is untrusted generated text, rendered encoded.
/// </summary>
public sealed class AiDemoRun : Entity
{
    public const int PromptMaxLength = 1000;
    public const int OutputMaxLength = 4000;
    public const int FailureCodeMaxLength = 60;

    private AiDemoRun()
    {
        Language = string.Empty;
        Prompt = string.Empty;
    }

    public Guid CustomerId { get; private set; }

    public Guid ProductId { get; private set; }

    public string Language { get; private set; }

    [SensitiveData]
    public string Prompt { get; private set; }

    public AiDemoRunStatus Status { get; private set; }

    public string? Output { get; private set; }

    public string? FailureCode { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static Result<AiDemoRun> Queue(Guid customerId, Guid productId, string language, string? prompt, DateTimeOffset nowUtc)
    {
        var clean = prompt?.Trim();
        if (string.IsNullOrEmpty(clean) || clean.Length > PromptMaxLength)
        {
            return Error.Validation("AiDemo.InvalidPrompt", $"Write your question (at most {PromptMaxLength} characters).");
        }

        return new AiDemoRun
        {
            CustomerId = customerId,
            ProductId = productId,
            Language = language,
            Prompt = clean,
            Status = AiDemoRunStatus.Queued,
            CreatedAtUtc = nowUtc,
        };
    }

    public void Start(DateTimeOffset nowUtc)
    {
        Status = AiDemoRunStatus.Running;
        StartedAtUtc = nowUtc;
    }

    public void Complete(string output, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(output);
        Output = output.Length > OutputMaxLength ? output[..OutputMaxLength] : output;
        Status = AiDemoRunStatus.Completed;
        CompletedAtUtc = nowUtc;
    }

    public void Fail(string code, DateTimeOffset nowUtc)
    {
        FailureCode = code.Length > FailureCodeMaxLength ? code[..FailureCodeMaxLength] : code;
        Status = AiDemoRunStatus.Failed;
        CompletedAtUtc = nowUtc;
    }
}

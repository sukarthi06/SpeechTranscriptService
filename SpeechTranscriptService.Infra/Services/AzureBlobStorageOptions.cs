namespace SpeechTranscriptService.Infra.Services;

public sealed class AzureBlobStorageOptions
{
    public const string SectionName = "Azure";
    public required string SourceContainer { get; init; }
    public required string WavContainer { get; init; }
}

namespace VannaLight.Api.Services.Predictions;

public sealed class MlModelArtifactMetadata
{
    public string ProfileSignature { get; set; } = string.Empty;
    public string TrainedUtc { get; set; } = string.Empty;
    public string SourceMode { get; set; } = string.Empty;
    public string? ConnectionName { get; set; }
    public string? DisplayName { get; set; }
}

namespace Weda.SubNode.Abstractions.Storage.Recordings;

public class RecordingOptions
{
    public const string SectionName = "Record";

    public const string StorageDirectory = "./data/recording";

    public int MinFreeDiskSpaceMb { get; set; } = 128;    
    
    public int RetentionDays { get; set; } = 7;
}
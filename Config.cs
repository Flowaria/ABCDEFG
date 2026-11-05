namespace ABCDEFG;

internal sealed class Config
{
    public string GTFODataPath { get; set; } = string.Empty;
    public int MaxThread { get; set; } = 1;
    public bool WriteReroutedOnDisk { get; set; } = false;
    public bool LogVerbose { get; set; } = false;
}

namespace V81TestChn;

public sealed class TranslationEntry
{
    public string source { get; set; } = string.Empty;
    public string target { get; set; } = string.Empty;
    public string mode { get; set; } = "translate";
    public string note { get; set; } = string.Empty;
}

public sealed class TranslationFile
{
    public int version { get; set; } = 1;
    public string locale { get; set; } = "zh-CN";
    public TranslationEntry[] entries { get; set; } = new TranslationEntry[0];
}


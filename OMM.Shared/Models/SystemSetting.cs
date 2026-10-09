namespace OMM.Shared.Models;

public class SystemSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public string? ModifiedByUserId { get; set; }
}

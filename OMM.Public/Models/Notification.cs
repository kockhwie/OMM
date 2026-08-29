namespace OMM.Public.Models;

public class Notification
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Type { get; set; } = "maturity"; // maturity, goal, data, product
    public string Date { get; set; } = string.Empty;
    public bool Read { get; set; }
}

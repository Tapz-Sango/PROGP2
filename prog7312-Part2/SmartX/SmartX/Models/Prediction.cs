namespace SmartX.Models
{
    public class Prediction
    {
        public string Context { get; set; } = "";
        public string SuggestedAction { get; set; } = "";
        public int Confidence { get; set; }
        public string Reason { get; set; } = "";
    }
}
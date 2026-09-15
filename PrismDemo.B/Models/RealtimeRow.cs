namespace PrismDemo.B.Models
{
    public class RealtimeRow
    {
        public string Time { get; set; } = "";
        public double FrontB { get; set; }
        public double BackB { get; set; }
        public double Flow { get; set; }
        public double PredictB { get; set; }
        public double ActualB { get; set; }
        public double PredictBUnitRate { get; set; }
    }
}

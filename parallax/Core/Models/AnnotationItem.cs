namespace parallax.Core.Models
{
    public class AnnotationItem
    {
        public string Type { get; set; } = "Pen"; // "Pen", "Arrow", "Rectangle", "Ellipse", "Text", "Blur"
        public System.Windows.Point StartPoint { get; set; }
        public System.Windows.Point EndPoint { get; set; }
        public System.Windows.Media.Color Color { get; set; } = System.Windows.Media.Colors.Red;
        public double StrokeThickness { get; set; } = 2.0;
        public string? Text { get; set; }
        public List<System.Windows.Point> Points { get; set; } = new();
    }
}

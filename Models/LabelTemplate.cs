using System.Collections.Generic;

namespace EvoDesigner.Models
{
    public class LabelTemplate
    {
        public string TemplateName { get; set; }
        public double LabelWidth { get; set; }
        public double LabelHeight { get; set; }
        public double MarginLeft { get; set; }
        public double MarginRight { get; set; }
        public double MarginTop { get; set; }
        public double MarginBottom { get; set; }
        public List<LabelElement> Elements { get; set; } = new();
        public List<FieldMapping> FieldMappings { get; set; } = new();
    }
}

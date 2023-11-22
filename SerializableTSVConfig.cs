using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TootTallyTootScoreVisualizer
{
    [XmlRoot("tsv")]
    public class SerializableTSVConfig
    {
        [XmlAttribute("modversion")]
        public string modversion;

        [XmlAttribute("decimalprecision")]
        public int decimalprecision;

        [XmlArray("scorethresholdlist"), XmlArrayItem("threshold")]
        public List<ThresholdData> scoreThreshold;

        [XmlArray("multiplierthresholdlist"), XmlArrayItem("threshold")]
        public List<ThresholdData> multiplierThreshold;


        public class ThresholdData
        {
            [XmlAttribute("value")]
            public float threshold;

            [XmlAttribute("textsize")]
            public int size;

            [XmlElement("text")]
            public string text;

            [XmlElement("color")]
            public string color;
        }
    }
}

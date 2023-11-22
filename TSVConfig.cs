using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using UnityEngine;

namespace TootTallyTootScoreVisualizer
{
    public static class TSVConfig
    {
        public static List<Threshold> scoreThresholdList;
        public static bool configLoaded;

        public static void LoadConfig(string configName)
        {
            configLoaded = false;
            var configFileName = configName + ".xml";
            string folderPath = Path.Combine(Paths.BepInExRootPath + Plugin.CONFIGS_FOLDER_NAME);

            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            if (!File.Exists(folderPath + configFileName))
            {
                Plugin.LogError($"TSV config {configFileName} not found, using default config");
                configFileName = "Default.xml";
                Plugin.Instance.TSVName.Value = "Default";
                if (!File.Exists(folderPath + configFileName))
                {
                    Plugin.LogError($"Couldn't find {configFileName}. Generating new Default config");
                    File.WriteAllText(folderPath + configFileName, GetHardCodedDefaultConfigString);
                }

            }

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SerializableTSVConfig));
                SerializableTSVConfig config = (SerializableTSVConfig)serializer.Deserialize(File.OpenRead(folderPath + configFileName));

                List<MultiplierThreshold> multiplierThresholdList = new List<MultiplierThreshold>();
                foreach (SerializableTSVConfig.ThresholdData data in config.multiplierThreshold)
                {
                    multiplierThresholdList.Add(new MultiplierThreshold(data.threshold, data.text, data.size, data.color));
                }



                scoreThresholdList = new List<Threshold>();
                foreach (SerializableTSVConfig.ThresholdData data in config.scoreThreshold)
                {
                    Color tempColor;
                    ColorUtility.TryParseHtmlString(data.color, out tempColor);
                    scoreThresholdList.Add(new Threshold(data.threshold, data.text, data.size, tempColor, config.decimalprecision, multiplierThresholdList));
                }
                scoreThresholdList.Sort((x, y) => x.value > y.value ? 1 : 0);
                Plugin.currentLoadedConfigName = configName;
                Plugin.LogInfo($"TSV config {configName} has been loaded");
                configLoaded = true;
            }
            catch (Exception ex)
            {
                Plugin.LogError(ex.Message);
            }

        }

        public static Threshold GetScoreThreshold(float value)
        {
            for (int i = 0; i < scoreThresholdList.Count; i++)
            {
                if (value > scoreThresholdList[i].value)
                    return scoreThresholdList[i];
            }

            return scoreThresholdList.Last();
        }

        public static string GetHardCodedDefaultConfigString =>
        @"<!--
        %P = Accuracy score (0 to 100)
        %m = Uses the text that matches the threshold as specified in multiplierthresholdlist
        %M = In-game value of multiplier (1x to 10x)
        threshold = if the value is greater than the given threshold
        decimalprecision = number of digits 
        textsize = the size of the text (defaulted at 20)
        -->
        <tsv modversion = ""1.0.0"" decimalprecision=""2"">
	        <scorethresholdlist>
		        <threshold value = ""88"" textsize=""20"" >
                    <text>%MX</text>
			        <color>#FFFFFFFF</color>
		        </threshold>
				<threshold value = ""79"" textsize=""20"">
					<text>OK</text>
					<color>#FFFFFFFF</color>
				</threshold>
				<threshold value = ""70"" textsize=""20"" >
					<text>MEH</text>
					<color>#FFFFFFFF</color>
				</threshold>
				<threshold value = ""0"" textsize=""20"">
					<text>x</text>
					<color>#FF0000FF</color>
				</threshold>
	        </scorethresholdlist>
	        <multiplierthresholdlist>
		        <threshold value = ""0"">
                    <text></text>
                    <color>#FFFFFFFF</color>
		        </threshold>
            </multiplierthresholdlist>
        </tsv>";
    }

    public class Threshold
    {
        public float value;
        public string text;
        public int size;
        public Color color;
        public int decimalprecision;
        public List<MultiplierThreshold> multiplierThresholdList;

        public Threshold(float value, string text, int size, Color color, int decimalprecision, List<MultiplierThreshold> multiplierThresholdList)
        {
            this.value = value;
            this.text = text;
            this.size = size;
            this.color = color;
            this.decimalprecision = decimalprecision;
            this.multiplierThresholdList = multiplierThresholdList;

            if (this.size == 0)
            {
                this.size = 20;
            }
        }

        public string GetFormattedText(float notescoreaverage, int multiplier)
        {
            //Potentially more formatting options in the future
            string formattedText = text.Replace("%P", $"<size={size}>" + notescoreaverage.ToString($"F{decimalprecision}") + "</size>");
            formattedText = formattedText.Replace("%m", GetMultiplierThreshold(multiplier).GetFormattedMultiplierText(multiplier));
            formattedText = formattedText.Replace("%M", $"<size={size}>" + multiplier + "</size>");
            formattedText = formattedText.Replace("%n", "\n");

            return formattedText;
        }
        public string GetFormattedTextNoColor(float notescoreaverage, int multiplier)
        {
            //Potentially more formatting options in the future
            string formattedText = text.Replace("%P", $"<size={size}>" + notescoreaverage.ToString($"F{decimalprecision}") + "</size>");
            formattedText = formattedText.Replace("%m", GetMultiplierThreshold(multiplier).GetFormattedMultiplierTextNoColor(multiplier));
            formattedText = formattedText.Replace("%M", $"<size={size}>" + multiplier + "</size>");
            formattedText = formattedText.Replace("%n", "\n");

            return formattedText;
        }

        public MultiplierThreshold GetMultiplierThreshold(float value)
        {
            for (int i = 0; i < multiplierThresholdList.Count; i++)
            {
                if (value >= multiplierThresholdList[i].value)
                    return multiplierThresholdList[i];
            }

            return multiplierThresholdList.Last();
        }
    }

    public class MultiplierThreshold
    {
        public float value;
        public string text;
        public int size;
        public string color;

        public MultiplierThreshold(float value, string text, int size, string color)
        {
            this.value = value;
            this.text = text;
            this.size = size;
            this.color = color;

            if (this.size == 0)
            {
                this.size = 20;
            }
        }

        public string GetFormattedMultiplierText(int multiplier)
        {
            //Potentially more formatting options in the future
            string formattedText = $"<size={size}><color={color}>" + text.Replace("%M", multiplier.ToString()) + "</color></size>";
            return formattedText;
        }
        public string GetFormattedMultiplierTextNoColor(int multiplier)
        {
            string formattedText = $"<size={size}>" + text.Replace("%M", multiplier.ToString()) + "</size>";
            return formattedText;
        }

    }
}

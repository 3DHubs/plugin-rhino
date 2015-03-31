using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Reflection;

namespace _3dhubs
{
    public class ConfigurationError : Exception { }

    public class Configuration
    {
        const string SettingsFileName = "3dhubs.xml";
        const string SettingsRoot = "plugin3hubs";

        public Dictionary<string, string> Main { get; private set; }
        public Dictionary<string, string> Export { get; private set; }

        public Configuration()
        {
            Main = new Dictionary<string, string>();
            Export = new Dictionary<string, string>();
        }

        /* Reads key-value pairs from a configuration file section. */
        private void ReadKeyValueList(XmlReader reader, Dictionary<string, string> data)
        {
            data.Clear();

            reader.ReadStartElement();
            reader.Read();
            reader.MoveToContent();

            while (reader.NodeType == XmlNodeType.Element)
            {
                string name = reader.LocalName;
                string value = reader.ReadElementContentAsString();
                data[name] = value;
                reader.MoveToContent();
            }
            reader.ReadEndElement();
        }

        public bool Load()
        {
            bool success = false;
            try
            {
                string filePath = Utilities.BuildPluginPath(SettingsFileName);
                using (XmlReader reader = XmlReader.Create(filePath))
                {
                    if (reader.ReadToFollowing("config"))
                        ReadKeyValueList(reader, Main);
                    if (reader.ReadToFollowing("export"))
                        ReadKeyValueList(reader, Export);
                    success = true;
                }
            }
            catch (IOException) { }
            catch (XmlException) { }
            return success;
        }

        /* Returns an item from the main configuration as an integer. */
        public int GetInteger(string key, int defval)
        {
            string valueString;
            if (!Main.TryGetValue(key, out valueString))
                return defval;
            int value;
            if (!int.TryParse(valueString, out value))
                value = defval;
            return value;
        }
    }
}

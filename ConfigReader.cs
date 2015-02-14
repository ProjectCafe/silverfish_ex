using System.IO;
using Newtonsoft.Json;

namespace silver
{
	public static class ConfigReader
	{
        /// <summary>
        /// Contains the configuration
        /// </summary>
		public static Config cfg { get; set; }

        /// <summary>
        /// True, if the file was read before, otherwise false
        /// </summary>
		private static bool bwas_read = false;
		/// <summary>
		/// Retrieves all values from the specific json-file
		/// <br>Only read the file one time and hold the information
		/// </summary>
		/// <param name="filename">Path to the json-file</param>
		public static void Read(string filename)
		{
            // Was it read before?
			if (bwas_read)
				return;

			string filecontent;

			// Read content of json-file
			using (var reader = new StreamReader (filename)) 
			{
				filecontent = reader.ReadToEnd ();
			}

			// Deserialize json to our Config-class
			cfg = JsonConvert.DeserializeObject<Config>(filecontent);

            // We read this file, so set this to true to avoid I/O-overhead
			bwas_read = true;
		}
	}
}


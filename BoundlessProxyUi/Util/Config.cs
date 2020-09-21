using System;
using System.Collections.Generic;
using System.Configuration;

namespace BoundlessProxyUi.Util
{
    /// <summary>
    /// Class to manage common ConfigurationManager use cases
    /// </summary>
    static class Config
    {
        /// <summary>
        /// Cache of the configuration items
        /// </summary>
        private static readonly Dictionary<string, string> s_configCache = new Dictionary<string, string>();

        /// <summary>
        /// Saves a setting in the app.config
        /// </summary>
        /// <typeparam name="T">The type to convert the configuration string to</typeparam>
        /// <param name="key">The name of the setting to get</param>
        /// <param name="defaultValue">The default value if the setting is not present</param>
        /// <returns>The value of the setting, or the passed in default value if not found</returns>
        public static T GetSetting<T>(string key, T defaultValue)
        {
            // Try to get the value from the cache
            if (!s_configCache.TryGetValue(key, out var result))
            {
                // If not cached, read it from the app.config file. If not present, use the default
                result = ConfigurationManager.AppSettings[key] ?? defaultValue.ToString();

                // Add the value to the cache for quick access later
                s_configCache.Add(key, result);
            }

            // Convert it to the requested type
            return (T)Convert.ChangeType(result, typeof(T));
        }

        /// <summary>
        /// Gets a setting from the app.config
        /// </summary>
        /// <typeparam name="T">The type to convert the input from</typeparam>
        /// <param name="key">The name of the setting to save</param>
        /// <param name="value">The value to save</param>
        public static void SetSetting<T>(string key, T value)
        {
            var valueString = value.ToString();

            // If the value is already in the cache and alrady matches what is being set, then just return
            if (!s_configCache.TryGetValue(key, out var result))
            {
                s_configCache.Add(key, result);
            }
            else if (result == valueString)
            {
                return;
            }

            // Add setting if not preset
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (configuration.AppSettings.Settings[key] == null)
            {
                configuration.AppSettings.Settings.Add(new KeyValueConfigurationElement(key, null));
            }

            // Update the value of the setting
            configuration.AppSettings.Settings[key].Value = valueString;

            // Write settings to file
            configuration.Save(ConfigurationSaveMode.Full, true);
            ConfigurationManager.RefreshSection("appSettings");

            // Save the value in the cache for quick access later
            s_configCache[key] = valueString;
        }
    }
}

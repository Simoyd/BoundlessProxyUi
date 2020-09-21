using BoundlessProxyUi.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoundlessProxyUi.JsonUpload
{
    class JsonUploadWindowViewModel : INotifyPropertyChanged
    {
        public bool JsonSaveFile
        {
            get
            {
                return Config.GetSetting(nameof(JsonSaveFile), false);
            }
            set
            {
                Config.SetSetting(nameof(JsonSaveFile), value);
                OnPropertyChanged(nameof(JsonSaveFile));
            }
        }

        public bool JsonSaveApi
        {
            get
            {
                return Config.GetSetting(nameof(JsonSaveApi), false);
            }
            set
            {
                Config.SetSetting(nameof(JsonSaveApi), value);
                OnPropertyChanged(nameof(JsonSaveApi));
            }
        }

        public string JsonApiKey
        {
            get
            {
                return Config.GetSetting(nameof(JsonApiKey), "");
            }
            set
            {
                Config.SetSetting(nameof(JsonApiKey), value);
                OnPropertyChanged(nameof(JsonApiKey));
            }
        }

        /// <summary>
        /// Occurs when a property value changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Called when a property value changes
        /// </summary>
        /// <param name="propertyName">The name  of the property that changed</param>
        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

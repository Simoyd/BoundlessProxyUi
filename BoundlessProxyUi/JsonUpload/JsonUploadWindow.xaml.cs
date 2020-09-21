using BoundlessProxyUi.ProxyManager;
using BoundlessProxyUi.WsData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BoundlessProxyUi.JsonUpload
{
    /// <summary>
    /// Interaction logic for JsonUploadWindow.xaml
    /// </summary>
    public partial class JsonUploadWindow : Window
    {
        public static JsonUploadWindow Instance { get; set; }

        public JsonUploadWindow()
        {
            Instance = this;

            InitializeComponent();

            DataContext = MyDataContext = new JsonUploadWindowViewModel();
        }

        JsonUploadWindowViewModel MyDataContext;

        private void Window_Closed(object sender, EventArgs e)
        {
            Instance = null;

            try
            {
                ProxyManagerWindow.Instance?.Close();
            }
            catch { }
        }

        public void OnFrameIn<T>(int planetId, string planetDisplayName, T frame_object)
        {
            var frame = frame_object as WsFrame;

            foreach (var curMessage in frame.Messages)
            {
                if (curMessage.ApiId.HasValue && curMessage.ApiId.Value == 0 && curMessage.Buffer.Length > 0)
                {
                    JObject payload = null;

                    try
                    {
                        payload = JObject.Parse(Encoding.UTF8.GetString(curMessage.Buffer));
                    }
                    catch (Exception ex)
                    {
                        return;
                    }

                    if (MyDataContext.JsonSaveFile)
                    {
                        var invalidFileNameChars = System.IO.Path.GetInvalidFileNameChars();
                        var fileName = new string(planetDisplayName.Where(cur => !invalidFileNameChars.Contains(cur)).ToArray());

                        try
                        {
                            File.WriteAllText($"{fileName}.json", payload.ToString(Formatting.Indented));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to write {fileName}.json:\r\n{ex.Message}", "Error writing json", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    if (MyDataContext.JsonSaveApi)
                    {
                        var client = new HttpClient();
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", MyDataContext.JsonApiKey);

                        payload["world_id"] = planetId;
                        //payload["display_name"] = planetDisplayName;

                        string aikjbhgshdoi = payload.ToString();

                        HttpResponseMessage response = null;

                        try
                        {
                            response = client.PostAsync("https://api.boundlexx.app/api/ingest-ws-data/", new StringContent(payload.ToString(), Encoding.UTF8, "application/json")).Result;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to upload {planetDisplayName} json: {ex.Message}", "Error uploading json", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        if (response != null && !response.IsSuccessStatusCode)
                        {
                            MessageBox.Show($"Failed to upload {planetDisplayName} json. Response code: {response.StatusCode}", "Error uploading json", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                }
            }
        }
    }
}

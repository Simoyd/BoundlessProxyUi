using BoundlessProxyUi.Mitm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BoundlessProxyUi.ProxyManager.Components
{
    public class ServerList
    {
        public string Hostname;
        public string Ip;
    }

    class ComponentEngine
    {
        public static ComponentEngine Instance { get; set; }

        public ComponentEngine(List<ComponentBase> clientComponents)
        {
            Instance = this;
            ClientComponents = clientComponents;
        }

        public List<ServerList> ServerList { get; set; } = new List<ServerList>();
        public Dictionary<string, string> ServerLookup => ServerList.ToDictionary(cur => cur.Hostname, cur => cur.Ip);

        public bool GreenLight => ClientComponents.All(cur => cur.IsEnabled & cur.IsFullyOn);

        private List<ComponentBase> ClientComponents;

        bool working = false;

        public void Start()
        {
            lock (this)
            {
                if (working)
                {
                    return;
                }

                working = true;
            }

            try
            {
                UserControl processing = null;

                ProxyManagerWindow.Instance.Dispatcher.Invoke(() =>
                {
                    ComponentBase.UpdateProcessingText("Please Wait");

                    processing = new Processing();
                    ProxyManagerWindow.Instance.FadeControl(processing);
                });

                new Thread(() =>
                {
                    try
                    {
                        int curIndex = 0;
                        bool error = false;

                        string message = string.Empty;

                        foreach (var curComponent in ClientComponents)
                        {
                            ComponentBase.UpdateProcessingText("Please Wait");

                            try
                            {
                                var result = curComponent.Start();

                                if (!string.IsNullOrEmpty(result))
                                {
                                    message = result;
                                    error = true;
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                message = ex.Message;
                                error = true;
                                break;
                            }

                            ++curIndex;
                        }

                        if (error && ReferenceEquals(ProxyManagerWindow.Instance.ActiveControl, processing))
                        {
                            throw new Exception($"{curIndex} Setup error occured, but no user flow present. {message}");
                        }

                        if (!error)
                        {
                            ProxyManagerWindow.Instance.Dispatcher.Invoke(() =>
                            {
                                ProxyManagerWindow.Instance.FadeControl(new Running());
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        ProxyManagerWindow.Instance.Dispatcher.Invoke(() => MessageBox.Show($"Fatal error in component engine:\r\n{ex.Message}\r\n\r\nApplication will now reset.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error));
                        ProxyManagerWindow.Instance.Dispatcher.Invoke(() =>
                        {
                            ProxyManagerWindow.Instance.FadeControl(new WelcomePage());
                        });
                    }
                    finally
                    {
                        working = false;
                    }
                }).Start();
            }
            catch
            {
                working = false;
            }
        }

        public async Task<List<string>> Stop()
        {
            while (true)
            {
                lock (this)
                {
                    if (!working)
                    {
                        working = true;
                        break;
                    }
                }

                Thread.Sleep(100);
            }

            ProxyManagerWindow.Instance.Dispatcher.Invoke(() =>
            {
                ComponentBase.UpdateProcessingText("Please Wait");
                ProxyManagerWindow.Instance.FadeControl(new Processing("Shutting down..."));
            });

            await Task.Delay(333);

            List<string> errors = new List<string>();
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();

            SslMitmInstance.Terminate = true;

            new Thread(() =>
            {
                if (ClientComponents != null)
                {
                    foreach (var curComponent in ClientComponents.ToArray().Reverse())
                    {
                        ComponentBase.UpdateProcessingText("Please Wait");

                        try
                        {
                            var curErrors = curComponent.Stop();

                            if (curErrors.Count > 0)
                            {
                            }

                            errors.AddRange(curErrors);
                        }
                        catch (Exception ex)
                        {
                            errors.Add(ex.Message);
                            break;
                        }
                    }
                }

                tcs.SetResult(null);
            }).Start();

            await tcs.Task;
            return errors;
        }

        public T GetComponent<T>()
            where T : ComponentBase
        {
            return ClientComponents.Where(cur => cur is T).FirstOrDefault() as T;
        }
    }
}

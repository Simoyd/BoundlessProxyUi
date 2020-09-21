using BoundlessProxyUi.JsonUpload;
using BoundlessProxyUi.ProxyManager.Components;
using BoundlessProxyUi.ProxyUi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BoundlessProxyUi.ProxyManager
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class ProxyManagerWindow : Window
    {
        public static ProxyManagerWindow Instance { get; set; }

        public ProxyManagerWindow()
        {
            Instance = this;

            InitializeComponent();

            Title = $"{Title} - {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(4)}";

            DataContext = new ManagerWindowViewModel();

            componentEngine = new ComponentEngine(ClientComponents);

            int i = 0;
            ClientComponents.ForEach(cur =>
            {
                cur.ComponentIndex = i++;
                cur.ManagerWindowViewModel = (ManagerWindowViewModel)DataContext;

                TextBox curComponentBlock = new TextBox
                {
                    Margin = new Thickness(5),
                    IsReadOnly = true,
                    Width = 50,
                    Padding = new Thickness(3),
                    TextAlignment = TextAlignment.Center,
                    Text = cur.Title,
                };

                stackMain.Children.Add(curComponentBlock);

                cur.UpdateBackgroundColor();
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FadeControl(new WelcomePage());
        }

        private List<ComponentBase> ClientComponents = new List<ComponentBase>
        {
            new GameComponent(),
            new CertComponent(),
            new HostsComponent(),
            new TcpComponent(),
            new UdpComponent(),
            //new BlocksComponent(),
        };

        public UserControl ActiveControl { get; set; } = null;

        private ComponentEngine componentEngine;

        public void FadeControl(UserControl nextPage, bool fadeIn = true, bool right = true, EventHandler completed = null)
        {
            if (nextPage == null)
            {
                return;
            }

            nextPage.Background = null;
            nextPage.Name = "A" + string.Join("", Guid.NewGuid().ToByteArray().Select(cur => cur.ToString("X2")));

            nextPage.SetBinding(WidthProperty, new Binding("ActualWidth")
            {
                Mode = BindingMode.OneWay,
                Source = canvasMain,
            });

            nextPage.SetBinding(HeightProperty, new Binding("ActualHeight")
            {
                Mode = BindingMode.OneWay,
                Source = canvasMain,
            });

            if (fadeIn)
            {
                canvasMain.Children.Add(nextPage);
                Canvas.SetTop(nextPage, 0);
            }

            var duration = new Duration(TimeSpan.FromMilliseconds(100));

            var story = new Storyboard();

            var fade = new DoubleAnimation
            {
                From = fadeIn ? 0.0 : 1.0,
                To = fadeIn ? 1.0 : 0.0,
                Duration = duration,
            };
            Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
            story.Children.Add(fade);

            var slide = new DoubleAnimation
            {
                From = fadeIn ? (right ? 50.0 : -50.0) : 0,
                To = fadeIn ? 0.0 : (right ? 50.0 : -50.0),
                Duration = duration,
            };
            Storyboard.SetTargetProperty(slide, new PropertyPath(Canvas.LeftProperty));
            story.Children.Add(slide);

            if (completed != null)
            {
                story.Completed += completed;
            }

            if (!fadeIn)
            {
                story.Completed += (sender, e) => canvasMain.Children.Remove(nextPage);
            }

            if (fadeIn && ActiveControl != null)
            {
                nextPage.Visibility = Visibility.Collapsed;

                FadeControl(ActiveControl, false, !right, (sender, e) =>
                {
                    nextPage.Visibility = Visibility.Visible;
                    nextPage.BeginStoryboard(story);
                });
            }
            else
            {
                nextPage.BeginStoryboard(story);
            }

            ActiveControl = fadeIn ? nextPage : null;
        }

        private bool shutdownStarted = false;
        private bool shutdownCompleted = false;

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (shutdownStarted)
            {
                if (!shutdownCompleted)
                {
                    e.Cancel = true;
                }

                return;
            }

            e.Cancel = true;
            shutdownStarted = true;

            try
            {
                ProxyUiWindow.Instance?.Close();
            }
            catch { }

            try
            {
                JsonUploadWindow.Instance?.Close();
            }
            catch { }

            ComponentEngine.Instance.Stop().ContinueWith(bla =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (bla.Result.Count > 0)
                    {
                        MessageBox.Show($"The following errors occured while trying to restore your system:\r\n{string.Join("\r\n", bla.Result)}", "Shutdown Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    shutdownCompleted = true;
                    Close();

                    Task.Run(async () =>
                    {
                        await Task.Delay(5000);
                        Application.Current.Shutdown(0);
                    });
                }));
            });
        }
    }
}

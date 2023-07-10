using System;
using System.Reflection;
using System.Windows;

namespace KikBot2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private App()
        {
            InitializeComponent();
        }

        [STAThread]
        private static void Main()
        {
            var main = new MainWindow();
            var g = new glueauth(main, "kikbot2", Assembly.GetExecutingAssembly().GetName().Version.ToString(), new string[0]);
            var app = new App();
#if DEBUG
            app.Run(main);
#else
            app.Run(g);
#endif
        }
    }
}

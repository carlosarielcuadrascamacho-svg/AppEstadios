using Microsoft.Extensions.DependencyInjection;

namespace AppEstadios
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Reemplazamos 'new AppShell()' por nuestra 'MainTabbedPage'
            // Esto hace que la app inicie directamente con las 3 pestañas
            return new Window(new MainTabbedPage());
        }
    }
}
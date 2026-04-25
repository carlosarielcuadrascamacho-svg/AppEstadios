using AppEstadios.Controllers;
using Microsoft.Extensions.Logging;


namespace AppEstadios
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {


            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiMaps()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialSymbolsRounded.ttf", "MaterialSymbols");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            // Construimos la app primero
            var app = builder.Build();

            // Inicializamos Supabase de forma asíncrona antes de que la UI arranque.
            // Usamos Task.Run para no bloquear el hilo principal (no hay await aquí).
            // Los controladores manejan errores internamente si la conexión falla.
            Task.Run(async () => await SupabaseService.InicializarAsync());

            return app;
        }
    }
}

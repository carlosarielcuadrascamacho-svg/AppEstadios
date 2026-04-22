using Supabase;

namespace AppEstadios.Controllers
{
    
    public static class SupabaseService
    {
        
        private const string SupabaseUrl = "https://xnqsxudypmweafnhhtpn.supabase.co";

        private const string SupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InhucXN4dWR5cG13ZWFmbmhodHBuIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzY4NDI5MTYsImV4cCI6MjA5MjQxODkxNn0.kQkriN-97RRm2RpOmEMNcRInUwKEuz09y_RhiASTgPg";

        // ──────────────────────────────────────────────────────────
        //  CLIENTE SINGLETON
        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// Instancia única del cliente de Supabase.
        /// Nula hasta que se llame a InicializarAsync().
        /// </summary>
        private static Supabase.Client? _cliente;

        /// <summary>
        /// Propiedad de acceso al cliente.
        /// Lanza una excepción clara si se intenta usar antes de inicializar.
        /// </summary>
        public static Supabase.Client Cliente
        {
            get
            {
                // Verificación de seguridad: el cliente debe estar inicializado
                if (_cliente is null)
                    throw new InvalidOperationException(
                        "SupabaseService no ha sido inicializado. " +
                        "Llama a InicializarAsync() en MauiProgram.cs o App.xaml.cs primero.");

                return _cliente;
            }
        }

        /// <summary>
        /// Inicializa la conexión con Supabase de forma asíncrona.
        /// Debe llamarse UNA SOLA VEZ al arrancar la aplicación,
        /// idealmente en MauiProgram.cs o en App.xaml.cs.
        ///
        /// Ejemplo de uso en MauiProgram.cs:
        ///   var app = builder.Build();
        ///   await SupabaseService.InicializarAsync();
        ///   return app;
        /// </summary>
        public static async Task InicializarAsync()
        {
            // Si ya fue inicializado, no hacemos nada (idempotente)
            if (_cliente is not null)
                return;

            // Configuramos las opciones del cliente de Supabase
            var opciones = new SupabaseOptions
            {
                // Auto-conectar el canal de tiempo real de Supabase (para subscriptions futuras)
                AutoConnectRealtime = true
            };

            // Creamos e inicializamos el cliente
            _cliente = new Supabase.Client(SupabaseUrl, SupabaseKey, opciones);
            await _cliente.InitializeAsync();
        }
    }
}

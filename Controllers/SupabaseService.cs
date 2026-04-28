// Traemos las herramientas necesarias para conectarnos con Supabase
using Supabase;

namespace AppEstadios.Controllers
{
    // Esta clase funciona como el puente principal entre nuestra aplicación y el servidor
    // donde guardamos toda la información. Se encarga de preparar y mantener abierta
    // la conexión para que otras partes del código puedan guardar o leer datos de manera
    // fácil y segura sin tener que configurar todo desde cero cada vez.
    public static class SupabaseService
    {
        // Esta es la dirección de internet privada de nuestro servidor de base de datos.
        // Le dice a la aplicación a qué lugar del mundo debe enviar y pedir la información.
        private const string SupabaseUrl = "https://xnqsxudypmweafnhhtpn.supabase.co";

        // Esta es una clave secreta y muy larga que funciona como una contraseña maestra.
        // Permite que nuestra aplicación tenga permiso para entrar y hacer cambios en el servidor.
        private const string SupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InhucXN4dWR5cG13ZWFmbmhodHBuIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzY4NDI5MTYsImV4cCI6MjA5MjQxODkxNn0.kQkriN-97RRm2RpOmEMNcRInUwKEuz09y_RhiASTgPg";

        // Aquí guardamos la conexión ya establecida para no tener que crear una nueva a cada rato.
        // Empieza estando vacía hasta que el sistema arranca por completo.
        private static Supabase.Client? _cliente;

        // Este apartado nos da acceso directo a la conexión ya lista con el servidor de base de datos.
        // Incluye una medida de seguridad muy importante que detiene la aplicación y avisa con un mensaje
        // claro si alguien intenta usar la conexión antes de que esté completamente preparada.
        public static Supabase.Client Cliente
        {
            get
            {
                // Revisamos con mucho cuidado si la conexión aún no ha sido creada
                if (_cliente is null)
                    // Si no está lista, lanzamos un aviso amigable explicando qué salió mal
                    // y cómo solucionarlo para que el programador sepa qué hacer.
                    throw new InvalidOperationException(
                        "SupabaseService no ha sido inicializado. " +
                        "Llama a InicializarAsync() en MauiProgram.cs o App.xaml.cs primero.");

                // Si todo está en orden, entregamos la conexión lista para usarse
                return _cliente;
            }
        }

        // Esta función es la que se encarga de encender los motores y configurar la conexión con la
        // base de datos por primera vez cuando abrimos la aplicación. Revisa si ya estamos conectados
        // para no hacer doble trabajo y deja todo listo para que podamos recibir actualizaciones en tiempo real.
        public static async Task InicializarAsync()
        {
            // Comprobamos si ya habíamos hecho la conexión anteriormente.
            // Si es así, nos salimos inmediatamente para ahorrar tiempo y memoria.
            if (_cliente is not null)
                return;

            // Creamos una lista de preferencias para configurar nuestra conexión
            var opciones = new SupabaseOptions
            {
                // Activamos una función para que el servidor nos avise al instante
                // si ocurre algún cambio en los datos sin tener que estar preguntando.
                AutoConnectRealtime = true
            };

            // Juntamos la dirección, la clave secreta y nuestras preferencias para crear el conector.
            _cliente = new Supabase.Client(SupabaseUrl, SupabaseKey, opciones);
            
            // Le damos la orden final al conector para que empiece a hablar con el servidor de internet.
            await _cliente.InitializeAsync();
        }
    }
}

// Importamos herramientas para formatear textos y números de manera internacional
using System.Globalization;
// Importamos librerías para analizar archivos de texto estructurados tipo JSON
using System.Text.Json;
// Importamos las herramientas básicas de ubicación de Microsoft Maui
using Microsoft.Maui.Maps;

namespace AppEstadios.Services
{
    // Esta clase sirve para trazar el camino en auto hacia los estadios utilizando una alternativa
    // gratuita llamada OSRM, la cual se basa en el mapa libre OpenStreetMap. A diferencia de los
    // servicios de pago, nos permite saber cómo llegar de un punto a otro sin necesidad de claves.
    public static class OsrmRoutingService
    {
        // Esta es la dirección base del servidor público y gratuito que calcula las rutas.
        private const string BaseUrl = "https://router.project-osrm.org";

        // Este es el cliente encargado de enviar los datos a través de la red.
        private static readonly HttpClient _http = new()
        {
            // Le damos un tiempo límite de 15 segundos para responder antes de cancelar la llamada.
            Timeout = TimeSpan.FromSeconds(15)
        };

        // Este paquete contiene las respuestas del cálculo de la ruta, guardando si el proceso
        // fue exitoso, la lista de coordenadas del camino y los mensajes de cualquier eventualidad.
        public record RutaResultado(
            // Indica si la ruta se encontró satisfactoriamente
            bool Exito,
            // Guarda la colección de puntos que forman el camino en el mapa
            List<Location> Puntos,
            // Indica el estado o código devuelto por el servidor
            string Estado,
            // Almacena la explicación en caso de que ocurra algún fallo
            string MensajeError);

        // Esta función realiza la petición al servidor gratuito de internet para conseguir el
        // trazado de las calles. Ajusta las coordenadas de origen y destino al formato requerido
        // y convierte el texto encriptado que recibe en puntos geográficos reales y visibles.
        public static async Task<RutaResultado> ObtenerRutaAsync(
            // La coordenada exacta del punto de partida del viaje
            Location origen,
            // La coordenada exacta del estadio al que queremos llegar
            Location destino)
        {
            // Abrimos un bloque de protección para detectar fallos de red
            try
            {
                // Extraemos la longitud y latitud del punto inicial usando puntos decimales.
                // OSRM nos pide colocar primero la longitud, al contrario que otros mapas.
                var orLon = origen.Longitude.ToString("F6", CultureInfo.InvariantCulture);
                var orLat = origen.Latitude.ToString("F6",  CultureInfo.InvariantCulture);
                
                // Hacemos lo mismo con las coordenadas del lugar de destino final.
                var deLon = destino.Longitude.ToString("F6", CultureInfo.InvariantCulture);
                var deLat = destino.Latitude.ToString("F6",  CultureInfo.InvariantCulture);

                // Armamos el enlace de internet completo pidiendo el camino detallado por carretera.
                var url = $"{BaseUrl}/route/v1/driving/{orLon},{orLat};{deLon},{deLat}" +
                          "?overview=full&geometries=polyline&steps=false";

                // Dejamos registro de la consulta que estamos a punto de realizar.
                Console.WriteLine($"[OSRM] GET {url}");

                // Despachamos la solicitud a través de internet hacia el servidor de rutas.
                var response = await _http.GetAsync(url);
                // Leemos todo el texto de respuesta que nos envió el servidor.
                var json = await response.Content.ReadAsStringAsync();

                // Registramos en la consola el código de estado de la respuesta web.
                Console.WriteLine($"[OSRM] HTTP {(int)response.StatusCode}");
                // Mostramos un fragmento inicial del texto para confirmar que llegó correctamente.
                Console.WriteLine($"[OSRM] JSON (primeros 300 chars): {json[..Math.Min(300, json.Length)]}");

                // Empezamos a desmenuzar el texto JSON recibido.
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Buscamos la palabra clave de respuesta que nos indica el éxito del proceso.
                var code = root.GetProperty("code").GetString() ?? "Unknown";
                Console.WriteLine($"[OSRM] code: {code}");

                // Evaluamos si la respuesta es diferente a la palabra de éxito esperada.
                if (code != "Ok")
                {
                    var message = string.Empty;
                    // Si el servidor nos dio un mensaje de error, lo guardamos aquí.
                    if (root.TryGetProperty("message", out var msgEl))
                        message = msgEl.GetString() ?? string.Empty;

                    // Registramos el fallo y devolvemos el paquete de error correspondiente.
                    Console.WriteLine($"[OSRM] Error: {code} — {message}");
                    return new RutaResultado(false, new(), code, message);
                }

                // Obtenemos la sección donde se guardan los caminos encontrados.
                var routes = root.GetProperty("routes");
                // Si la lista no tiene ningún camino trazado, cancelamos el proceso.
                if (routes.GetArrayLength() == 0)
                    return new RutaResultado(false, new(), "NoRoutes", "OSRM no encontró rutas entre los puntos.");

                // Extraemos el texto comprimido que dibuja la línea de la ruta.
                var polyline = routes[0]
                    .GetProperty("geometry")
                    .GetString() ?? string.Empty;

                // Imprimimos el tamaño del texto encriptado para verificar su peso.
                Console.WriteLine($"[OSRM] Polyline length: {polyline.Length} chars");

                // Traducimos el texto encriptado en coordenadas limpias para el mapa.
                var puntos = DecodificarPolyline(polyline);
                Console.WriteLine($"[OSRM] Puntos decodificados: {puntos.Count}");

                // Retornamos la ruta completada exitosamente con sus coordenadas reales.
                return new RutaResultado(true, puntos, "Ok", string.Empty);
            }
            // Si el servidor tarda demasiado tiempo en contestarnos.
            catch (TaskCanceledException)
            {
                // Registramos la tardanza en los registros de diagnóstico.
                Console.WriteLine("[OSRM] Timeout al conectar con el servidor.");
                // Informamos al usuario sobre la desconexión temporal.
                return new RutaResultado(false, new(), "Timeout",
                    "El servidor tardó demasiado en responder. Verifica tu conexión a internet.");
            }
            // Si ocurre cualquier otro fallo imprevisto durante el proceso.
            catch (Exception ex)
            {
                // Reportamos el error general en la bitácora del sistema.
                Console.WriteLine($"[OSRM] EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
                return new RutaResultado(false, new(), "Exception", ex.Message);
            }
        }

        // Esta herramienta funciona como una salida rápida para abrir la aplicación de mapas
        // instalada en el teléfono del usuario. Delega la tarea de navegación a aplicaciones
        // externas cuando no es posible dibujar la ruta dentro de nuestro propio sistema.
        public static async Task AbrirEnMapasNativoAsync(Location origen, Location destino)
        {
            // Preparamos las coordenadas de salida asegurándonos de usar puntos decimales.
            var orLat = origen.Latitude.ToString("F6",  CultureInfo.InvariantCulture);
            var orLon = origen.Longitude.ToString("F6", CultureInfo.InvariantCulture);
            // Hacemos exactamente lo mismo con los datos de llegada.
            var deLat = destino.Latitude.ToString("F6",  CultureInfo.InvariantCulture);
            var deLon = destino.Longitude.ToString("F6", CultureInfo.InvariantCulture);

            // Configuramos el enlace especial que le indica al móvil que abra su visor de mapas.
            var uri = $"https://www.google.com/maps/dir/?api=1" +
                      $"&origin={orLat},{orLon}" +
                      $"&destination={deLat},{deLon}" +
                      $"&travelmode=driving";

            // Dejamos constancia del enlace generado en el registro del dispositivo.
            Console.WriteLine($"[OSRM] Abriendo mapas nativo: {uri}");
            // Ejecutamos la orden que despierta a la aplicación externa de mapas.
            await Launcher.Default.OpenAsync(uri);
        }

        // Este algoritmo traduce una cadena de texto muy larga y comprimida en una lista ordenada
        // de coordenadas geográficas reales. Ahorra datos móviles comprimiendo las rutas.
        private static List<Location> DecodificarPolyline(string encoded)
        {
            // Creamos la colección temporal donde iremos apilando las ubicaciones.
            var puntos = new List<Location>();
            // Llevamos el conteo del avance dentro de la cadena de caracteres.
            int index = 0;
            // Inicializamos las variables base para las matemáticas de posición.
            int lat = 0, lng = 0;

            // Ciclo que procesa el texto completo de principio a fin.
            while (index < encoded.Length)
            {
                // Sumamos el valor rescatado a la latitud acumulada anterior.
                lat += DecodificarValor(encoded, ref index);
                // Sumamos el valor rescatado a la longitud acumulada anterior.
                lng += DecodificarValor(encoded, ref index);
                // Guardamos la coordenada final convirtiéndola al tamaño estándar.
                puntos.Add(new Location(lat / 1e5, lng / 1e5));
            }

            // Entregamos las ubicaciones en el orden cronológico del recorrido.
            return puntos;
        }

        // Esta es una función interna que ayuda al traductor de rutas a interpretar los números
        // ocultos en el texto comprimido, pasándolos de letras a valores matemáticos enteros.
        private static int DecodificarValor(string encoded, ref int index)
        {
            // Inicializamos los acumuladores para procesar el byte actual.
            int result = 0, shift = 0, b;
            
            // Bucle que procesa fragmentos de números de 5 en 5 bits.
            do
            {
                // Restamos 63 para mapear el carácter a su número original.
                b = encoded[index++] - 63;
                // Desplazamos los bits a su posición correcta en el entero.
                result |= (b & 0x1F) << shift;
                shift += 5;
            } while (b >= 0x20); // Seguirá repitiéndose mientras el bit de control esté activo.

            // Invierte el número si Google/OSRM lo guardaron con signo negativo.
            return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
        }
    }
}

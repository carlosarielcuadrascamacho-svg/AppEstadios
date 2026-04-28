// Importamos herramientas para dar formato a los textos y números según el país
using System.Globalization;
// Importamos herramientas para poder leer y entender textos en formato JSON
using System.Text.Json;
// Importamos las herramientas de mapas básicas que ofrece Microsoft Maui
using Microsoft.Maui.Maps;

namespace AppEstadios.Services
{
    // Esta clase se encarga de calcular el camino por carretera desde donde te encuentras hasta
    // el estadio de fútbol usando el servicio de mapas de Google. Básicamente, traduce las
    // coordenadas geográficas en una línea continua que se dibuja sobre el mapa para mostrarte
    // exactamente por qué calles y avenidas debes manejar.
    public static class GoogleDirectionsService
    {
        // Esta es la clave secreta que nos da permiso para usar el servicio de rutas de Google.
        // Funciona como una contraseña que se envía en cada petición a internet.
        private const string ApiKey = "AIzaSyATAKd13rosj5IJ3rM_F-43ojTXD0d8fnw";

        // Este es el cartero que se encarga de enviar y traer mensajes a través de internet.
        private static readonly HttpClient _http = new();

        // Este pequeño paquete guarda los resultados del viaje, indicando si se pudo encontrar
        // el camino con éxito, la lista de puntos geográficos para trazar la ruta y los posibles
        // mensajes de aviso en caso de que algo haya fallado en el camino.
        public record RutaResultado(
            // Indica con un sí o un no si la operación terminó con éxito
            bool Exito,
            // Guarda la lista ordenada de coordenadas que forman la línea de la ruta
            List<Location> Puntos,
            // Guarda el código de respuesta oficial que nos dio Google
            string Estado,           
            // Guarda el texto que explica qué falló en caso de haber un problema
            string MensajeError);

        // Esta función le pide a internet el camino exacto entre tu ubicación y el estadio.
        // Se encarga de enviar las coordenadas de inicio y fin en un formato que Google entienda,
        // recibe la respuesta en texto plano, y la convierte en puntos legibles.
        public static async Task<RutaResultado> ObtenerRutaAsync(
            // El punto de partida desde donde inicias tu viaje
            Location origen,
            // El punto de llegada que corresponde a la ubicación del estadio
            Location destino)
        {
            // Usamos un bloque de protección por si se cae el internet o falla la petición
            try
            {
                // Convertimos las coordenadas a texto usando puntos en vez de comas para los decimales.
                // Esto evita que Google se confunda si el teléfono está configurado en español.
                var orLat = origen.Latitude.ToString("F6", CultureInfo.InvariantCulture);
                var orLon = origen.Longitude.ToString("F6", CultureInfo.InvariantCulture);
                var deLat = destino.Latitude.ToString("F6", CultureInfo.InvariantCulture);
                var deLon = destino.Longitude.ToString("F6", CultureInfo.InvariantCulture);

                // Construimos la dirección de internet completa con todos los datos de nuestro viaje
                var url = $"https://maps.googleapis.com/maps/api/directions/json" +
                          $"?origin={orLat},{orLon}" +
                          $"&destination={deLat},{deLon}" +
                          $"&mode=driving" +
                          $"&key={ApiKey}";

                // Imprimimos en la bitácora interna el inicio del viaje para poder revisarlo luego
                Console.WriteLine($"[Directions] GET {url[..80]}...");

                // Enviamos la carta pidiendo la ruta a los servidores de Google
                var response = await _http.GetAsync(url);
                // Leemos la respuesta completa que nos devolvió el servidor en forma de texto
                var json = await response.Content.ReadAsStringAsync();

                // Anotamos en el registro el código de respuesta del servidor de internet
                Console.WriteLine($"[Directions] HTTP {(int)response.StatusCode}");
                // Mostramos un pedacito del texto recibido para comprobar que no esté vacío
                Console.WriteLine($"[Directions] JSON (primeros 300 chars): {json[..Math.Min(300, json.Length)]}");

                // Empezamos a analizar el texto recibido para extraer los datos importantes
                using var doc = JsonDocument.Parse(json);
                // Nos situamos en la raíz del texto analizado
                var root = doc.RootElement;
                // Buscamos el estado oficial de la operación que nos dio Google
                var status = root.GetProperty("status").GetString() ?? "UNKNOWN";

                // Anotamos el estado oficial en los registros de depuración
                Console.WriteLine($"[Directions] Status: {status}");

                // Si Google nos mandó un mensaje de error específico, lo guardamos
                if (root.TryGetProperty("error_message", out var errEl))
                    Console.WriteLine($"[Directions] error_message: {errEl.GetString()}");

                // Verificamos si Google nos dijo que algo no salió del todo bien
                if (status != "OK")
                {
                    // Creamos un espacio para guardar el detalle del problema
                    var errorDetail = string.Empty;
                    // Si existe el texto del error en la respuesta, lo capturamos
                    if (root.TryGetProperty("error_message", out var em))
                        errorDetail = em.GetString() ?? string.Empty;

                    // Registramos el fallo y nos salimos avisando que no hubo éxito
                    Console.WriteLine($"[Directions] Status: {status} | error_message: {errorDetail}");
                    // Retornamos el paquete indicando el fallo de la operación
                    return new RutaResultado(false, new(), status, errorDetail);
                }

                // Buscamos la sección donde vienen los caminos encontrados
                var routes = root.GetProperty("routes");
                // Si la lista de caminos viene completamente vacía, detenemos el proceso
                if (routes.GetArrayLength() == 0)
                    // Devolvemos un aviso diciendo que fue imposible encontrar un camino por tierra
                    return new RutaResultado(false, new(), "ZERO_RESULTS", "No se encontraron rutas.");

                // Extraemos el texto secreto y comprimido que contiene la línea del mapa
                var overviewPolyline = routes[0]
                    .GetProperty("overview_polyline")
                    .GetProperty("points")
                    .GetString() ?? string.Empty;

                // Mostramos cuánto espacio ocupa el texto secreto en memoria
                Console.WriteLine($"[Directions] Polyline length: {overviewPolyline.Length} chars");

                // Mandamos a traducir el texto secreto en puntos de mapas reales
                var puntos = DecodificarPolyline(overviewPolyline);
                // Registramos cuántos puntos logramos rescatar para dibujar en pantalla
                Console.WriteLine($"[Directions] Puntos decodificados: {puntos.Count}");

                // Entregamos con alegría el resultado exitoso junto a toda la lista de coordenadas
                return new RutaResultado(true, puntos, "OK", string.Empty);
            }
            // Si el internet falló por completo o la computadora tuvo un tropiezo
            catch (Exception ex)
            {
                // Dejamos constancia del tropiezo en los reportes internos de la app
                Console.WriteLine($"[Directions] EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
                // Devolvemos el paquete avisando que ocurrió una falla técnica grave
                return new RutaResultado(false, new(), "EXCEPTION", ex.Message);
            }
        }

        // Esta herramienta sirve como plan de emergencia para abrir directamente la aplicación de
        // mapas oficial que ya viene instalada en tu teléfono. Es muy útil cuando no podemos dibujar
        // la ruta directamente dentro de nuestra app, delegando todo el trabajo pesado a Google.
        public static async Task AbrirEnGoogleMapsNativoAsync(Location origen, Location destino)
        {
            // Preparamos las coordenadas de salida asegurándonos de usar puntos decimales
            var orLat = origen.Latitude.ToString("F6", CultureInfo.InvariantCulture);
            var orLon = origen.Longitude.ToString("F6", CultureInfo.InvariantCulture);
            // Preparamos las coordenadas de llegada de la misma manera limpia
            var deLat = destino.Latitude.ToString("F6", CultureInfo.InvariantCulture);
            var deLon = destino.Longitude.ToString("F6", CultureInfo.InvariantCulture);

            // Construimos el enlace de internet especial que los teléfonos usan para abrir sus mapas
            var uri = $"https://www.google.com/maps/dir/?api=1" +
                      $"&origin={orLat},{orLon}" +
                      $"&destination={deLat},{deLon}" +
                      $"&travelmode=driving";

            // Anotamos en los registros que vamos a saltar a la aplicación externa
            Console.WriteLine($"[Directions] Abriendo Google Maps nativo: {uri}");
            // Le pedimos al teléfono que abra el enlace usando su navegador o aplicación preferida
            await Launcher.Default.OpenAsync(uri);
        }

        // Este algoritmo traduce una cadena de texto muy larga y comprimida en una lista ordenada
        // de coordenadas geográficas reales. Google envía las rutas de esta forma compacta para ahorrar
        // datos móviles, y nosotros tenemos que "desarmar" ese texto para poder pintar la línea.
        private static List<Location> DecodificarPolyline(string encoded)
        {
            // Creamos una lista donde iremos guardando las coordenadas rescatadas
            var puntos = new List<Location>();
            // Llevamos la cuenta de en qué letra del texto secreto estamos posicionados
            int index = 0;
            // Preparamos variables para ir sumando los valores de latitud y longitud
            int lat = 0, lng = 0;

            // Repetimos el proceso mientras nos queden letras por analizar en el texto
            while (index < encoded.Length)
            {
                // Traducimos el siguiente pedazo de texto y se lo sumamos a la latitud anterior
                lat += DecodificarValor(encoded, ref index);
                // Traducimos el siguiente pedazo de texto y se lo sumamos a la longitud anterior
                lng += DecodificarValor(encoded, ref index);
                // Agregamos el nuevo punto geográfico a nuestra lista dividiendo entre 100,000
                puntos.Add(new Location(lat / 1e5, lng / 1e5));
            }

            // Entregamos la lista completa con todos los puntos listos para trazarse
            return puntos;
        }

        // Esta es una función interna que ayuda al traductor de rutas a interpretar los números
        // secretos que vienen escondidos en el texto comprimido de Google. Convierte pequeños trozos
        // de letras en números enteros que luego se transforman en puntos exactos del planeta.
        private static int DecodificarValor(string encoded, ref int index)
        {
            // Preparamos las matemáticas internas para hacer la traducción de letras a números
            int result = 0, shift = 0, b;
            // Iniciamos un ciclo repetitivo para ir leyendo el número secreto letra por letra
            do
            {
                // Obtenemos el código de la letra y le restamos 63 según las reglas de Google
                b = encoded[index++] - 63;
                // Hacemos operaciones binarias para ir reconstruyendo el número original
                result |= (b & 0x1F) << shift;
                // Aumentamos el desplazamiento para la siguiente posición numérica
                shift += 5;
            } while (b >= 0x20); // El ciclo continúa mientras el número siga incompleto

            // Devolvemos el número final aplicando una última corrección matemática requerida
            return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
        }
    }
}
